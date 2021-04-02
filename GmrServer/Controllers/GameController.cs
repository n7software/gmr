using GmrLib;
using GmrLib.Models;
using GmrServer.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;

namespace GmrServer.Controllers
{
    using System.Data;

    public partial class GameController : SessionCheckController
    {
        private const int TurnsToDisplayOnGameProfile = 50;

        public ActionResult Index(bool gameCreationError = false, bool publicGames = false)
        {
            ViewBag.GameCreationError = gameCreationError;
            ViewBag.ShowPublic = publicGames;

            return View();
        }

        [Authorize]
        public ActionResult MyGames(bool gameCreationError = false)
        {
            IEnumerable<Game> games = new List<Game>();

            var gmrDb = GmrEntities.CreateContext();
            var user = Global.CurrentUser(gmrDb);
            if (user != null)
            {
                var userGames = user.GamePlayers.Where(gp => gp.Game.Finished == null).Select(gp => gp.Game).OrderBy(g => g, new CompareMyGames());
                games = userGames.Union(user.GamesHosted.Where(g => g.Finished == null));
            }

            InitGameSelector(gmrDb);

            ViewBag.GameCreationError = gameCreationError;

            return PartialView(games);
        }

        public ActionResult Browser()
        {
            return new RedirectResult(Url.Action("Index") + "#public");
        }

        public const int GamesPerPage = 10;
        public ActionResult PublicGames()
        {
            var games = GetPublicGamesQuery().Take(GamesPerPage);

            return PartialView(games);
        }

        public ActionResult BrowserPage(int page)
        {
            var games = GetPublicGamesQuery()
                            .Skip(page * GamesPerPage)
                            .Take(GamesPerPage);

            return PartialView(games);
        }

        private IQueryable<Game> GetPublicGamesQuery()
        {
            var gmrDb = GmrEntities.CreateContext();
            var query = from g in gmrDb.Games
                        where !g.Private
                           && (g.Started == null || g.Players.Count(p => p.UserID == 0) > 0)
                        select g;

            if (Global.IsUserAuthenticated())
            {
                dynamic prefs = PackagedPreferences.Get(Global.CurrentUser());

                if (!prefs.ShowPublicNewGames && !prefs.ShowPublicInProgressGames)
                {
                    query = new List<Game>().AsQueryable();
                }
                else if (!prefs.ShowPublicNewGames)
                {
                    query = query.Where(g => g.Started.HasValue);
                }
                else if (!prefs.ShowPublicInProgressGames)
                {
                    query = query.Where(g => !g.Started.HasValue);
                }


                switch ((PublicGameSortMethod)prefs.SortPublicGamesMethod)
                {
                    case PublicGameSortMethod.DateCreated:
                        query = query.OrderBy(g => g.Created);
                        goto default;

                    case PublicGameSortMethod.DateCreatedDesc:
                        goto default;

                    case PublicGameSortMethod.HumanPlayers:
                        query = query.OrderBy(g => g.Players.Count(p => p.UserID != 0));
                        break;

                    case PublicGameSortMethod.HumanPlayersCount:
                        query = query.OrderByDescending(g => g.Players.Count(p => p.UserID != 0));
                        break;

                    default:
                        query = query.OrderByDescending(g => g.Created);
                        break;
                }
            }
            else
            {
                query = query.OrderByDescending(g => g.Created);
            }

            return query;
        }

        [Authorize]
        public ActionResult Create(string name, int steamGameID, string description, GameType type = GameType.Standard, bool allowDlc = false)
        {
            string encodedDescription = HttpUtility.HtmlEncode(description);
            if (!Utilities.IsTextMarkupValid(encodedDescription))
            {
                return Content("The description entered contains invalid tags. Please check the formatting tags you entered and try again.");
            }

            var gmrDB = GmrEntities.CreateContext();
            var user = Global.CurrentUser(gmrDB);

            if (IsUserAtGameLimit(user))
                return UserAtGameLimit();

            var steamGame = gmrDB.SteamGames.FirstOrDefault(g => g.SteamGameID == steamGameID);
            if (steamGame == null)
                return SteamGameDoesntExist(steamGameID);

            var game = new Game
            {
                Name = name,
                Description = encodedDescription,
                Host = user,
                Private = true,
                Created = DateTime.UtcNow,
                SteamGame = steamGame,
                AllowDLC = allowDlc,
                Type = (int)type,
                MaxPlayers = 8
            };
            game.Players.Add(new GamePlayer { User = user, TurnOrder = 0, AllowVacation = true });
            gmrDB.Games.Add(game);
            gmrDB.SaveChanges();

            PlayerStatsManager.Instance.RemoveUserStats(user.UserId);

            return Content(game.GameID.ToString());
        }

        [Authorize]
        [HttpPost]
        public ActionResult CreateScenario(HttpPostedFileBase saveFileUpload, string scenarioName, int scenarioGame, string scenarioDescription)
        {
            try
            {
                if (saveFileUpload != null && saveFileUpload.ContentLength > 0)
                {
                    int steamGameID = scenarioGame;
                    var gmrDB = GmrEntities.CreateContext();
                    var user = Global.CurrentUser(gmrDB);

                    if (IsUserAtGameLimit(user))
                        return UserAtGameLimit();

                    var steamGame = gmrDB.SteamGames.FirstOrDefault(g => g.SteamGameID == steamGameID);
                    if (steamGame == null)
                        return SteamGameDoesntExist(steamGameID);

                    var saveFileBytes = new byte[saveFileUpload.ContentLength];

                    int bytesRead = 0;
                    int readSoFar = 0;

                    while ((bytesRead = saveFileUpload.InputStream.Read(saveFileBytes, readSoFar, saveFileBytes.Length)) > 0)
                    {
                        readSoFar += bytesRead;
                    }

                    saveFileBytes = GameManager.SetAllPlayerNamesToDefault(saveFileBytes);

                    saveFileBytes = GameManager.SetGameTypeInSaveFileBytes(saveFileBytes, GameManager.SaveFileType.HotSeat);

                    int playerCount = GameManager.GetPlayerCount(saveFileBytes);

                    var game = new Game
                    {
                        Name = scenarioName,
                        Description = HttpUtility.HtmlEncode(scenarioDescription),
                        Host = user,
                        Private = true,
                        Created = DateTime.UtcNow,
                        SteamGame = steamGame,
                        Type = (int)GameType.Scenario,
                        MaxPlayers = playerCount
                    };

                    bool dlc = false;
                    for (int i = 1; i <= playerCount; i++)
                    {
                        var civ = GameManager.GetPlayerCivilizationFromSaveFileBytes(saveFileBytes, i, gmrDB);

                        if (civ != null)
                        {
                            var civGame = civ.SteamGameCivilizations.SingleOrDefault(c => c.SteamGameID == steamGameID && c.CivID == civ.CivID);
                            if (civGame != null)
                                dlc |= civGame.IsDLC;
                        }

                        User player;
                        if (i == 1)
                            player = user;
                        else
                            player = gmrDB.Users.Single(u => u.UserId == 0);

                        game.Players.Add(
                            new GamePlayer
                            {
                                User = player,
                                TurnOrder = i - 1,
                                Civilization = civ
                            });
                    }
                    game.AllowDLC = dlc;

                    gmrDB.Games.Add(game);
                    var turn = new Turn
                    {
                        User = user,
                        Started = DateTime.UtcNow,
                        Number = 0,
                        GamePlayer = game.Players.OrderBy(gp => gp.TurnOrder).First()
                    };
                    game.Turns.Add(turn);
                    gmrDB.SaveChanges();

                    bool success = GameManager.SubmitTurn(turn, saveFileBytes, gmrDB, true);

                    if (success)
                    {
                        gmrDB.SaveChanges();

                        foreach (var gamePlayer in game.Players)
                        {
                            PlayerStatsManager.Instance.RemoveUserStats(gamePlayer.UserID);
                        }

                        return RedirectToAction("Details", new { id = game.GameID });
                    }
                    else
                    {
                        gmrDB.Games.Remove(game);
                        gmrDB.SaveChanges();
                    }
                }
            }
            catch (Exception exc)
            {
                DebugLogger.WriteException(exc);
            }

            return RedirectToAction("Index", new { gameCreationError = true });
        }

        private bool IsUserAtGameLimit(User user)
        {
            int gameLimit = Global.CurrentUserGameLimit();
            return (gameLimit > 0 && user.GamePlayers.Count >= gameLimit);
        }

        private ActionResult UserAtGameLimit()
        {
            return View("CantJoin", (object)("Sorry, but you've reached your max limit of " + Global.CurrentUserGameLimit() +
                " games for your account. If you'd like to increase your limit, please " +
                "<a href=\"" + Url.Action("Index", "SupportUs") + "\">support us</a>."));
        }

        private ActionResult SteamGameDoesntExist(int gameId)
        {
            return View("CantJoin", (object)("Sorry, but we currently don't support the Steam Game ID: " + gameId));
        }

        private int GameIdBeingJoined
        {
            get { return (int)Session["GameIdBeingJoined"]; }
            set { Session["GameIdBeingJoined"] = value; }
        }

        private int GameIdBeingSubmitted
        {
            get { return (int)Session["GameIdBeingSubmitted"]; }
            set { Session["GameIdBeingSubmitted"] = value; }
        }

        [Authorize]
        public ActionResult Join(int id, Guid? token)
        {
            Game game;
            User user;
            InitCivSelector(id, out game, out user);

            if (game != null && user != null)
            {
                var initialCiv = (ViewBag.AllCivs.Count == 1) ? ViewBag.AllCivs[0] : Civilization.Unknown;

                return ViewBasedOnPlayerValidity(id, game, user, View(initialCiv), token);
            }

            return RedirectToAction("Index");
        }

        private void InitCivSelector(int id, out Game game, out User user)
        {
            var gmrDb = GmrEntities.CreateContext();
            game = gmrDb.Games.FirstOrDefault(g => g.GameID == id);
            if (game != null)
            {
                user = Global.CurrentUser(gmrDb);
                ViewBag.AllCivs = GetCivList(null, game, gmrDb);
                GameIdBeingJoined = game.GameID;
                ViewBag.GameName = game.Name;
            }
            else
            {
                user = null;
            }
        }

        private void InitGameSelector(GmrEntities gmrDb)
        {
            ViewBag.AllSteamGames = GetGameList(1, gmrDb);
        }

        [Authorize]
        [HttpPost]
        public ActionResult Join(Civilization civ, Guid? token)
        {
            int id;
            var gmrDb = GmrEntities.CreateContext();
            Game game;
            var user = Global.CurrentUser(gmrDb);

            if (IsUserAtGameLimit(user))
                return UserAtGameLimit();

            civ = InitCivSelectorPost(civ, out id, gmrDb, out game);

            if (PlayerIsAllowedInGame(game, user, token) &&
                !PlayerIsAlreadyInGame(game, user) &&
                !IsFull(game))
            {
                if (TokenValid(game, token))
                    game.InviteTokens.Remove(game.InviteTokens.Single(t => t.Token == token.Value));

                var invite = user.ReceivedNotifications.FirstOrDefault(n => n.NotificationType == (int)NotificationType.GameInvite && n.Game == game);
                if (invite != null)
                {
                    user.ReceivedNotifications.Remove(invite);
                    gmrDb.Notifications.Remove(invite);
                }

                GameManager.JoinPlayer(game, user, civ);
                gmrDb.SaveChanges();

                PlayerStatsManager.Instance.RemoveUserStats(user.UserId);
            }

            return ViewBasedOnPlayerValidity(id, game, user, RedirectToAction("Details", new { id = id }), token);
        }

        private Civilization InitCivSelectorPost(Civilization civ, out int id, GmrEntities gmrDb, out Game game)
        {
            id = GameIdBeingJoined;
            int idCopy = id;
            game = gmrDb.Games.Single(g => g.GameID == idCopy);
            if (civ.CivID != -1)
                civ = gmrDb.Civilizations.Single(c => c.CivID == civ.CivID);
            else civ = Civilization.Unknown;
            return civ;
        }

        private ActionResult ViewBasedOnPlayerValidity(int id, Game game, User user, ActionResult defaultResult, Guid? token)
        {
            if (PlayerIsAlreadyInGame(game, user))
                return RedirectToAction("Details", new { id = id });
            else if (IsFull(game))
                return View("CantJoin", (object)"This game is full. :(");
            else if (PlayerIsAllowedInGame(game, user, token))
                return defaultResult;
            else return View("CantJoin", (object)"It looks like this game is private, and you weren't invited. :(");
        }

        private static bool IsFull(Game game)
        {
            bool hasNoAiPlayers = game.Players.Count(p => p.UserID == 0) == 0;

            if (game.GameType == GameType.Scenario)
            {
                return hasNoAiPlayers;
            }
            else
            {
                return game.Started.HasValue ? hasNoAiPlayers :
                                               game.Players.Count() >= game.MaxPlayers;
            }
        }

        private static bool PlayerIsAllowedInGame(Game game, User user, Guid? token)
        {
            return (!game.Private ||
                    game.Host == user ||
                    user.ReceivedNotifications.Where(n => n.NotificationType == (int)NotificationType.GameInvite && n.Game == game).Count() > 0 ||
                    TokenValid(game, token));
        }

        private static bool TokenValid(Game game, Guid? token)
        {
            return (token != null && game.InviteTokens.FirstOrDefault(t => t.Token == token.Value) != null);
        }

        private static bool PlayerIsAlreadyInGame(Game game, User user)
        {
            return game.Players.Where(p => p.UserID == user.UserId).Count() > 0;
        }

        [Authorize]
        public ActionResult AddAIPlayer(int id)
        {
            Game game;
            User user;
            InitCivSelector(id, out game, out user);

            if (!UserCanModifyGame(game, user))
                return View("CantJoin", (object)"You don't have permission to modify this game");
            if (game.Players.Count() >= game.MaxPlayers)
                return View("CantJoin", (object)"This game is full. :(");

            return View(Civilization.Unknown);
        }

        [HttpPost]
        [Authorize]
        public ActionResult AddAIPlayer(Civilization civ)
        {
            int id;
            var gmrDb = GmrEntities.CreateContext();
            Game game;
            User user = Global.CurrentUser(gmrDb);
            civ = InitCivSelectorPost(civ, out id, gmrDb, out game);

            if (!UserCanModifyGame(game, user))
                return View("CantJoin", (object)"You don't have permission to modify this game");
            if (game.Players.Count() >= game.MaxPlayers)
                return View("CantJoin", (object)"This game is full. :(");

            int numberOfPlayers = game.Players.Count();

            game.Players.Add(new GamePlayer
            {
                Civilization = civ.CivID == Civilization.Unknown.CivID ? null : civ,
                User = gmrDb.Users.Single(u => u.UserId == 0),
                TurnOrder = numberOfPlayers
            });

            gmrDb.SaveChanges();

            return RedirectToAction("Details", new { id = id });
        }

        [Authorize]
        public ActionResult ChangeCiv(int id)
        {
            Game game;
            User user;
            InitCivSelector(id, out game, out user);
            if (game.Players.Where(p => p.User == user).Count() == 0)
                return View("CantJoin", (object)"You're not in this game");
            else return View(Civilization.Unknown);
        }

        [HttpPost]
        [Authorize]
        public ActionResult ChangeCiv(Civilization civ)
        {
            int id;
            GmrEntities gmrDb = GmrEntities.CreateContext();
            Game game;
            User user = Global.CurrentUser(gmrDb);
            civ = InitCivSelectorPost(civ, out id, gmrDb, out game);

            if (game.Players.Where(p => p.User == user).Count() == 0)
                return View("CantJoin", (object)"You're not in this game");

            var player = game.Players.Single(p => p.User == user);
            if (game.GameType == GameType.Scenario)
            {
                GameManager.ReplaceAIWithPlayer(game, player.User, civ, gmrDb);
            }
            else
            {
                player.Civilization = civ.CivID == Civilization.Unknown.CivID ? null : civ;
            }
            gmrDb.SaveChanges();

            return RedirectToAction("Details", new { id = game.GameID });
        }

        public ActionResult Details(int id)
        {
            return new RedirectResult(Url.Action("Index") + string.Format("#{0}", id));
        }

        [HttpPost]
        public ActionResult Details(int? id, bool commentError = false, string errorMessage = null)
        {
            if (id.HasValue)
            {
                var gmrDB = GmrEntities.CreateContext();

                var game = gmrDB.Games.SingleOrDefault(g => g.GameID == id.Value);
                if (game != null)
                {

                    if (Request.IsAuthenticated)
                    {
                        User user = Global.CurrentUser(gmrDB);
                        ViewBag.AllowCivChange =
                            game.Started == null &&
                            PlayerIsInGame(game);
                        if (game.GameType == GameType.Scenario && game.Host == user)
                            ViewBag.AllowCivChange = false;

                        ViewBag.IsHost = (game.Host == user) || user.IsAdmin;
                    }

                    foreach (var player in game.Players)
                    {
                        if (!player.AverageTurnTime.HasValue)
                            player.RecalculateAverageTurnTime();
                    }
                    gmrDB.SaveChanges();

                    bool isAdmin = Global.IsAdminAuthenticated(gmrDB);
                    var lastTurn = game.Turns.OrderByDescending(t => t.Number).FirstOrDefault();

                    ViewBag.PlayerCanJoinGame = !game.Private &&
                                                (!game.HasStarted || (game.AllowPublicJoin && game.HasAiPlayers)) &&
                                                game.Players.Count(u => u.UserID == Global.UserSteamID) == 0;

                    ViewBag.PlayerCanLeaveGame = game.Started == null &&
                                                 game.Players.Any(u => u.UserID == Global.UserSteamID);

                    ViewBag.PlayerCanSurrenderGame = game.HasStarted &&
                                                     game.Players.Any(u => u.UserID == Global.UserSteamID);

                    ViewBag.PlayerCanRevertTurn = game.HasStarted &&
                                                  (game.Host.UserId == Global.UserSteamID
                                                   || isAdmin
                                                   || (lastTurn ?? new Turn()).UserID == Global.UserSteamID) &&
                                                  game.Turns.Count > 1;

                    ViewBag.PlayerCanSkip = game.HasStarted
                                            && (isAdmin
                                                || (lastTurn ?? new Turn()).UserID == Global.UserSteamID)
                                            && game.Turns.Count > 1;

                    var latestTurn = GameManager.GetLatestTurn(game);
                    ViewBag.IsPlayersTurn = (latestTurn != null &&
                                            latestTurn.UserID == Global.UserSteamID) ||
                                            isAdmin;

                    ViewBag.IsSaveAvailable = game.IsSaveAvailable();

                    ViewBag.TurnID = (latestTurn != null) ? latestTurn.TurnID : -1;

                    ViewBag.CommentError = commentError;

                    ViewBag.CommentError = errorMessage;

                    return PartialView(game);
                }
            }

            return PartialView("NotFound");
        }

        private static bool PlayerIsInGame(Game game)
        {
            return game.Players.Select(p => p.UserID).Where(u => u == Global.UserSteamID).Count() > 0;
        }

        public ActionResult GameDetailTurns(int? id)
        {
            var turns = new List<Turn>();

            if (id.HasValue)
            {
                var gmrDb = GmrEntities.CreateContext();

                var game = gmrDb.Games.FirstOrDefault(g => g.GameID == id.Value);
                if (game != null)
                {
                    turns.AddRange(game.Turns.Where(t => t.Finished.HasValue)
                                       .OrderByDescending(t => t.Finished)
                                       .Take(TurnsToDisplayOnGameProfile)
                                  );
                }
            }

            return PartialView(turns);
        }

        public ActionResult GameDetailComments(int id)
        {
            var gmrDb = GmrEntities.CreateContext();

            var game = gmrDb.Games.FirstOrDefault(g => g.GameID == id);

            return PartialView(game);
        }

        [HttpPost]
        [Authorize]
        public ActionResult StartGame(int id)
        {
            IEnumerable<long> playerUserIds = null;

            using (var gmrDB = GmrEntities.CreateContext())
            {
                var game = gmrDB.Games.FirstOrDefault(g => g.GameID == id);
                if (game != null && game.Started == null)
                {
                    if (game.Host.UserId == GmrServer.Global.UserSteamID || GmrServer.Global.IsAdminAuthenticated())
                    {
                        var humans = game.Players.Where(p => p.UserID != 0);
                        if (humans.Count() < 2)
                        {
                            return Content("You need at least two human players to start this game");
                        }

                        game.Started = DateTime.UtcNow;
                        game.Private = true;

                        var erroneousTurns = game.Turns.Where(t => t.Save == null);
                        foreach (var turn in erroneousTurns)
                            game.Turns.Remove(turn);

                        game.Turns.Add(new Turn
                        {
                            Number =
                                game.GameType == GameType.Mod || game.GameType == GameType.ModTotalConversion ? 0 : 1,
                            Started = DateTime.UtcNow,
                            User = game.Host,
                            GamePlayer = game.Players.OrderBy(gp => gp.TurnOrder).First()
                        });
                    }

                    game.InviteTokens.Clear();
                    gmrDB.SaveChanges();

                    playerUserIds = game.Players.Select(gp => gp.UserID).ToList();
                }
            }

            if (playerUserIds != null)
            {
                GameManager.ClearStartedGameIdsForUsers(playerUserIds);
            }

            return Content(string.Empty);
        }

        private IEnumerable<Civilization> GetCivList(int? id, Game game, GmrEntities gmrDB)
        {
            if (game.GameType == GameType.ModTotalConversion)
                return new List<Civilization> { Civilization.Unknown };
            else if (game.GameType == GameType.Scenario || game.Started.HasValue)
            {
                return game.Players.Where(p => p.UserID == 0).Select(p => p.Civilization).Distinct().Where(c => c != null).ToList();
            }

            var civQuery = from c in game.SteamGame.SteamGameCivilizations
                           select c;

            if (!game.AllowDLC)
            {
                civQuery = civQuery.Where(c => !c.IsDLC);
            }

            var civs = civQuery.Select(c => c.Civilization).OrderBy(c => c.Name).ToList();

            //var inUse = game.Players.Select(g => g.Civilization).Where(c => c != null).Select(c => c.CivID);
            //civs.RemoveAll(c => inUse.Contains(c.CivID));

            if (id != null && id >= 0)
            {
                var civList = civs.Where(c => c.CivID != id.Value).ToList();
                civList.Add(Civilization.Unknown);
                return civList;
            }
            else return civs;
        }

        private static Civilization SelectCivilization(int? id, GmrEntities gmrDB)
        {
            var selected = Civilization.Unknown;
            if (id != null && id >= 0)
                selected = gmrDB.Civilizations.Single(c => c.CivID == id.Value);
            return selected;
        }

        private IEnumerable<SteamGame> GetGameList(int? id, GmrEntities gmrDB)
        {
            var games = gmrDB.SteamGames.ToList();

            if (id != null && id >= 0)
            {
                var gameList = games.Where(g => g.SteamGameID != id.Value).ToList();
                return gameList;
            }
            else return games;
        }

        private static SteamGame SelectGame(int? id, GmrEntities gmrDB)
        {
            var selected = SteamGame.Civ5;
            if (id != null && id >= 0)
                selected = gmrDB.SteamGames.Single(g => g.SteamGameID == id.Value);
            return selected;
        }

        public ActionResult CivSelector(int? id)
        {
            var gmrDB = GmrEntities.CreateContext();
            var selected = SelectCivilization(id, gmrDB);
            return PartialView("~/Views/Shared/DisplayTemplates/Civilization.cshtml", selected);
        }

        public ActionResult SelectorPopup(int? id)
        {
            var gmrDB = GmrEntities.CreateContext();
            var game = gmrDB.Games.First(g => g.GameID == GameIdBeingJoined);
            ViewBag.AllCivs = GetCivList(id, game, gmrDB);
            return PartialView("~/Views/Shared/SelectorPopup.cshtml");
        }

        public ActionResult GameSelector(int? id)
        {
            var gmrDB = GmrEntities.CreateContext();
            var selected = SelectGame(id, gmrDB);
            return PartialView("~/Views/Shared/DisplayTemplates/SteamGame.cshtml", selected);
        }

        public ActionResult GameSelectorPopup(int? id)
        {
            var gmrDB = GmrEntities.CreateContext();
            ViewBag.AllSteamGames = GetGameList(id, gmrDB);
            return PartialView("~/Views/Shared/SelectorPopupGame.cshtml");
        }

        [HttpPost]
        [Authorize]
        public ActionResult SendInviteEmail(string email, int id)
        {
            if (!string.IsNullOrWhiteSpace(email))
            {
                using (GmrEntities gmrDb = GmrEntities.CreateContext())
                {
                    var user = Global.CurrentUser(gmrDb);
                    if (user != null)
                    {
                        var game = gmrDb.Games.FirstOrDefault(g => g.GameID == id);
                        if (game != null)
                        {
                            if (UserCanModifyGame(game, user))
                            {
                                GameManager.SendGameEmailInvitation(game, email);
                                gmrDb.SaveChanges();
                            }
                        }
                    }
                }
            }

            return Content(string.Empty);
        }

        [HttpPost]
        [Authorize]
        public ActionResult GenerateInviteLink(int id)
        {
            var gmrDb = GmrEntities.CreateContext();
            var game = gmrDb.Games.Single(g => g.GameID == id);
            var user = Global.CurrentUser(gmrDb);

            if (UserCanModifyGame(game, user, true))
            {
                var token = game.GenerateInviteToken().ToString();
                gmrDb.SaveChanges();
                return Content("http://multiplayerrobot.com/Game/Join/" + id + "?token=" + token);
            }
            else return Content(String.Empty);
        }

        [HttpPost]
        [Authorize]
        public ActionResult ReorderGamePlayers(int id, string newOrder)
        {
            using (GmrEntities gmrDb = GmrEntities.CreateContext())
            {
                var game = gmrDb.Games.Single(g => g.GameID == id);
                var user = Global.CurrentUser(gmrDb);
                if (UserCanModifyGame(game, user))
                {
                    string[] rawValues = newOrder.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    Dictionary<int, int> remapping = new Dictionary<int, int>();
                    foreach (string s in rawValues)
                    {
                        string[] kvp = s.Split(':');
                        int old = int.Parse(kvp[0]);
                        int @new = int.Parse(kvp[1]);
                        remapping.Add(old, @new);
                    }
                    var players = game.Players;
                    foreach (var p in players.ToArray())
                    {
                        if (remapping.ContainsKey(p.TurnOrder))
                        {
                            p.TurnOrder = remapping[p.TurnOrder];
                        }
                    }

                    gmrDb.SaveChanges();
                }
            }

            return Content(String.Empty);
        }

        private static bool UserCanModifyInProgressGame(Game game, User user)
        {
            return game.Host == user || user.IsAdmin;
        }

        private static bool UserCanModifyGame(Game game, User user, bool ignoreStarted = false)
        {
            return UserCanModifyInProgressGame(game, user) &&
                   (game.Started == null || ignoreStarted);
        }

        [Authorize]
        public ActionResult DownloadSave(int id, bool original = false)
        {
            using (GmrEntities gmrDb = GmrEntities.CreateContext())
            {
                var game = gmrDb.Games.FirstOrDefault(g => g.GameID == id);
                if (game != null)
                {
                    if (original && Global.IsAdminAuthenticated(gmrDb) && game.UnalteredSaveFileBytes != null)
                    {
                        return File(game.UnalteredSaveFileBytes, "application/octet-stream", game.Name + ".Civ5Save");
                    }
                    else
                    {
                        var turn = GameManager.GetLatestTurn(game);
                        if (turn != null &&
                            (turn.UserID == Global.UserSteamID || Global.IsAdminAuthenticated(gmrDb)))
                        {
                            var saveFileBytes = GameManager.GetLatestSaveFileBytes(game);

                            if (saveFileBytes != null)
                            {
                                return File(saveFileBytes, "application/octet-stream", Global.SaveFileDownloadName);
                            }
                        }
                    }
                }
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public ActionResult UploadSaveClient(HttpPostedFileBase saveFileUpload, int turnId, bool isCompressed, string authKey)
        {
            using (GmrEntities gmrDb = GmrEntities.CreateContext())
            {
                var user = gmrDb.Users.SingleOrDefault(u => u.AuthKey == authKey);
                if (user != null)
                {
                    var result = new SubmitTurnResult();

                    return ReceiveFile(gmrDb, saveFileUpload, turnId, user.UserId, isCompressed, false, result) ??
                           new JsonResult() { Data = result };
                }
                else throw new HttpException(401, "Invalid Auth Key");
            }
        }

        [HttpPost]
        [Authorize]
        public ActionResult UploadSave(HttpPostedFileBase saveFileUpload, int turnId)
        {
            using (GmrEntities gmrDb = GmrEntities.CreateContext())
            {
                return ReceiveFile(gmrDb, saveFileUpload, turnId, Global.UserSteamID) ?? RedirectToAction("Index");
            }
        }

        private ActionResult ReceiveFile(GmrEntities gmrDb, HttpPostedFileBase saveFileUpload, int turnId, long currentUserId, bool isCompressed = false, bool validateSaveFile = true, SubmitTurnResult result = null)
        {
            if (saveFileUpload != null && saveFileUpload.ContentLength > 0)
            {
                try
                {
                    int gameId = 0;

                    var turn = gmrDb.Turns.FirstOrDefault(t => t.TurnID == turnId);
                    if (turn != null &&
                        (turn.UserID == currentUserId || Global.IsAdminAuthenticated(gmrDb)))
                    {
                        var saveFileBytes = new byte[saveFileUpload.ContentLength];

                        int bytesRead = 0;
                        int readSoFar = 0;

                        while (
                            (bytesRead = saveFileUpload.InputStream.Read(saveFileBytes, readSoFar, saveFileBytes.Length)) >
                            0)
                        {
                            readSoFar += bytesRead;
                        }

                        if (isCompressed)
                        {
                            saveFileBytes = CivSaveLib.Compresion.DecompressBytes(saveFileBytes);
                        }

                        if (validateSaveFile)
                        {
                            var lastSaveBytes = GameManager.GetLatestSaveFileBytes(turn.Game);

                            //                            if (lastSaveBytes != null &&
                            //                                CivSavefile.IsNewFileBytesBloated(lastSaveBytes.Length, saveFileUpload.ContentLength))
                            //                            {
                            //                                GameIdBeingSubmitted = turn.GameID;
                            //                                return View("InvalidTurnSubmit",
                            //                                    (object)
                            //                                        @"It appears that the save file you're trying to submit has become 'bloated' due to a bug in Civ V. 
                            //                                                                        You can find more information on this bug and its effects on GMR games here: 
                            //                                                                        <a href=""http://multiplayerrobot.com/About/FAQ#civ-crashes-loading-save"" target=""_blank"">http://multiplayerrobot.com/About/FAQ#civ-crashes-loading-save</a>
                            //                                                                        <br/><br/>
                            //                                                                        Please restart Civ V, replay your turn and submit it again. If you continue to have this problem please contact us!");
                            //                            }

                            if (!GameManager.AreBytesCivSaveFile(saveFileBytes))
                            {
                                GameIdBeingSubmitted = turn.GameID;
                                return View("InvalidTurnSubmit",
                                    (object)
                                        "The file selected for submission was not a valid Civilization V save file.");
                            }

                            try
                            {
                                int lastPlayerIndex = GameManager.GetLatestPlayerIndex(turn.Game, lastSaveBytes);
                                int newPlayerIndex = GameManager.GetCurrentPlayerInSaveFileBytes(saveFileBytes);

                                if (turn.Number > 0 && newPlayerIndex == lastPlayerIndex)
                                {
                                    GameIdBeingSubmitted = turn.GameID;
                                    return View("InvalidTurnSubmit",
                                        (object)
                                            "It appears that you didn't completely end your turn before saving this game. Please replay your turn and be sure to only save when you are prompted for the next player's turn.");
                                }
                            }
                            catch (Exception exc)
                            {
                                DebugLogger.WriteException(exc, "Error getting current player number");
                            }
                        }

                        int pointsEarned = 0;

                        if (GameManager.SubmitTurn(turn, saveFileBytes, gmrDb, out pointsEarned))
                        {
                            gameId = turn.GameID;

                            gmrDb.SaveChanges();

                            if (result != null)
                            {
                                result.ResultType = SubmitTurnResultType.OK;
                                result.PointsEarned = pointsEarned;
                            }

                            PlayerStatsManager.Instance.RemoveUserStats(turn.UserID);
                        }

                        GameManager.RemoveGameFromCache(turn.GameID);
                    }

                    if (gameId > 0)
                    {
                        GameManager.TrimOldTurnsFromGameOnThread(gameId);
                    }
                }
                catch (Exception exc)
                {
                    DebugLogger.WriteException(exc,
                        string.Format("Uploading save file from website for turnId: {0}", turnId));

                    return View("InvalidTurnSubmit",
                        (object)
                            "There was an unexpected error submiting your turn. Please contact support@multiplayerrobot.com");
                }
                finally
                {
                    GC.Collect(GC.MaxGeneration);
                }
            }

            return null;
        }

        [HttpPost]
        [Authorize]
        public ActionResult CancelGame(int id)
        {
            try
            {
                var playerIdsRemoved = new List<long>();

                using (GmrEntities gmrDb = GmrEntities.CreateContext())
                {
                    var user = Global.CurrentUser(gmrDb);
                    if (user != null)
                    {
                        var game = gmrDb.Games.FirstOrDefault(g => g.GameID == id);
                        if (game != null)
                        {
                            if (UserCanModifyGame(game, user, true))
                            {
                                // Remove any notifications
                                gmrDb.Notifications.RemoveRange(game.Notifications);

                                // Set all players to AI
                                foreach (var player in game.Players)
                                {
                                    playerIdsRemoved.Add(player.UserID);
                                    player.UserID = 0;
                                }

                                // Set the host to AI
                                game.HostUserId = 0;
                                game.Private = true;

                                // Remove any save files
                                foreach (var turn in game.Turns.ToList())
                                {
                                    if (turn.Save != null)
                                    {
                                        gmrDb.Saves.Remove(turn.Save);
                                        turn.Save = null;
                                    }
                                }

                                gmrDb.SaveChanges();

                                playerIdsRemoved.ForEach(pid => PlayerStatsManager.Instance.RemoveUserStats(pid));

                                // Send the cancellation emails and notifications
                                GameManager.SendGameCancelledEmail(game);
                                GameManager.SendGameCancelledNotifications(game);
                            }
                        }
                    }
                }

                GameManager.ClearStartedGameIdsForUsers(playerIdsRemoved);
                GameManager.RemoveGameFromCache(id);
            }
            catch (Exception exc)
            {
                DebugLogger.WriteException(exc, string.Format("Cancelling Game {0}", id));
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [Authorize]
        public ActionResult LeaveGame(int id)
        {
            using (GmrEntities gmrDb = GmrEntities.CreateContext())
            {
                var game = gmrDb.Games.FirstOrDefault(g => g.GameID == id);
                if (game != null && game.Started == null)
                {
                    var player = game.Players.FirstOrDefault(p => p.UserID == Global.UserSteamID);
                    if (player != null)
                    {
                        long userId = player.UserID;

                        GameManager.LeavePlayer(gmrDb, game, player);

                        gmrDb.SaveChanges();

                        PlayerStatsManager.Instance.RemoveUserStats(userId);
                    }
                }
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [Authorize]
        public ActionResult SurrenderGame(int id)
        {
            try
            {
                using (GmrEntities gmrDb = GmrEntities.CreateContext())
                {
                    var game = gmrDb.Games.FirstOrDefault(g => g.GameID == id);
                    if (game != null && game.HasStarted)
                    {
                        var player = game.Players.FirstOrDefault(p => p.UserID == Global.UserSteamID);
                        if (player != null)
                        {
                            long userId = Global.UserSteamID;

                            GameManager.SurrenderPlayer(gmrDb, game, player);

                            gmrDb.SaveChanges();

                            PlayerStatsManager.Instance.RemoveUserStats(userId);
                            GameManager.RemoveGameFromCache(game.GameID);
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                DebugLogger.WriteException(exc, string.Format("User {0} surrendering from Game {1}", Global.UserSteamID, id));
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [Authorize]
        public ActionResult RevertTurn(int id)
        {
            try
            {
                long userId = -1;

                using (GmrEntities gmrDb = GmrEntities.CreateContext())
                {
                    var game = gmrDb.Games.FirstOrDefault(g => g.GameID == id);
                    if (game != null &&
                        game.HasStarted &&
                        (game.HostUserId == Global.UserSteamID
                         || Global.IsAdminAuthenticated(gmrDb)
                         || game.Turns.OrderByDescending(t => t.Number).First().UserID == Global.UserSteamID))
                    {
                        userId = game.Turns.OrderByDescending(t => t.Number).First().UserID;

                        if (GameManager.RevertTurn(gmrDb, game))
                        {
                            gmrDb.SaveChanges();
                        }
                    }
                }

                if (userId > 0)
                {
                    PlayerStatsManager.Instance.RemoveUserStats(userId);
                    GameManager.RemoveGameFromCache(id);
                }
            }
            catch (Exception exc)
            {
                DebugLogger.WriteException(exc, "Reverting Turn");
            }

            return Content(string.Empty);
        }

        [HttpPost]
        [Authorize]
        public ActionResult SkipTurn(int id)
        {
            using (GmrEntities gmrDb = GmrEntities.CreateContext())
            {

                var game = gmrDb.Games.FirstOrDefault(g => g.GameID == id);
                if (game != null &&
                    game.HasStarted &&
                    (Global.IsAdminAuthenticated(gmrDb)
                     || game.Turns.OrderByDescending(t => t.Number).First().UserID == Global.UserSteamID))
                {
                    long userId = game.Turns.OrderByDescending(t => t.Number).First().UserID;

                    if (GameManager.SkipCurrentPlayerOrCancelTurnTimer(game, gmrDb))
                    {
                        gmrDb.SaveChanges();
                    }

                    PlayerStatsManager.Instance.RemoveUserStats(userId);
                    GameManager.RemoveGameFromCache(game.GameID);
                }
            }

            return Content(string.Empty);
        }

        [HttpPost]
        [Authorize]
        public ActionResult RemoveGamePlayer(int id, int? playerTurnIndex, long? playerID)
        {
            using (GmrEntities gmrDb = GmrEntities.CreateContext())
            {
                var game = gmrDb.Games.Single(g => g.GameID == id);
                var user = Global.CurrentUser(gmrDb);
                if (playerTurnIndex != null &&
                    UserCanModifyGame(game, user))
                {
                    var gamePlayer = game.Players.Single(p => p.TurnOrder == playerTurnIndex);
                    if (game.GameType == GameType.Scenario)
                    {
                        gamePlayer.UserID = 0;
                    }
                    else
                    {
                        game.Players.Remove(gamePlayer);
                        gmrDb.GamePlayers.Remove(gamePlayer);

                        int i = 0;
                        foreach (var player in game.Players.OrderBy(p => p.TurnOrder).ToList())
                        {
                            player.TurnOrder = i++;
                        }
                    }
                }

                if (playerID != null &&
                    UserCanModifyGame(game, user, true))
                {
                    var invites = game.Notifications.Where(g => g.ReceivingUser.UserId == playerID).ToList();
                    gmrDb.Notifications.RemoveRange(invites);
                }

                gmrDb.SaveChanges();

                if (playerID.HasValue)
                {
                    PlayerStatsManager.Instance.RemoveUserStats(playerID.Value);
                }
            }

            return Content(String.Empty);
        }

        [HttpPost]
        [Authorize]
        public ActionResult InvitePlayer(int id, long playerId)
        {
            using (GmrEntities gmrDb = GmrEntities.CreateContext())
            {
                var game = gmrDb.Games.Single(g => g.GameID == id);
                var user = Global.CurrentUser(gmrDb);
                if (UserCanModifyGame(game, user, true))
                {
                    var receiving = gmrDb.Users.Single(u => u.UserId == playerId);
                    GameManager.InvitePlayer(game, receiving, user);
                    gmrDb.SaveChanges();
                }
            }

            return Content(String.Empty);
        }

        [HttpPost]
        [Authorize]
        public ActionResult UpdateMaxPlayers(int id, int players)
        {
            if (players >= 2 && players <= 12)
            {
                using (GmrEntities gmrDb = GmrEntities.CreateContext())
                {
                    var game = gmrDb.Games.Single(g => g.GameID == id);
                    if (game.Players.Count() <= players)
                        game.MaxPlayers = players;
                    gmrDb.SaveChanges();
                }
            }

            return Content(String.Empty);
        }

        [HttpPost]
        [Authorize]
        public ActionResult UpdateVisibility(int id, bool gamePrivate)
        {
            using (GmrEntities gmrDb = GmrEntities.CreateContext())
            {
                var currentUser = Global.CurrentUser(gmrDb);
                var game = gmrDb.Games.FirstOrDefault(g => g.GameID == id);
                if (game != null &&
                    (game.Host == currentUser || currentUser.IsAdmin))
                {
                    game.Private = gamePrivate;
                    gmrDb.SaveChanges();
                }
            }

            return Content(string.Empty);
        }

        [HttpPost]
        [Authorize]
        public ActionResult UpdateDlcAllowed(int id, bool dlcAllowed)
        {
            using (GmrEntities gmrDb = GmrEntities.CreateContext())
            {
                var currentUser = Global.CurrentUser(gmrDb);
                var game = gmrDb.Games.FirstOrDefault(g => g.GameID == id);
                if (game != null &&
                    (game.Host == currentUser || currentUser.IsAdmin))
                {
                    game.AllowDLC = dlcAllowed;
                    gmrDb.SaveChanges();
                }
            }

            return Content(string.Empty);
        }

        [HttpPost]
        [Authorize]
        public ActionResult UpdateShowInPublic(int id, bool showPublic)
        {
            using (GmrEntities gmrDb = GmrEntities.CreateContext())
            {
                var currentUser = Global.CurrentUser(gmrDb);
                var game = gmrDb.Games.FirstOrDefault(g => g.GameID == id);
                if (game != null
                    && (game.Host == currentUser || currentUser.IsAdmin))
                {
                    game.Private = !showPublic;
                    gmrDb.SaveChanges();
                }
            }

            return Content(string.Empty);
        }

        [HttpPost]
        [Authorize]
        public ActionResult UpdateAllowPublicJoin(int id, bool allowJoin)
        {
            using (GmrEntities gmrDb = GmrEntities.CreateContext())
            {
                var currentUser = Global.CurrentUser(gmrDb);
                var game = gmrDb.Games.FirstOrDefault(g => g.GameID == id);
                if (game != null
                    && (game.Host == currentUser || currentUser.IsAdmin))
                {
                    game.AllowPublicJoin = allowJoin;
                    gmrDb.SaveChanges();
                }
            }

            return Content(string.Empty);
        }

        [HttpPost]
        [Authorize]
        public ActionResult UpdateDescription(int id, string description)
        {
            string encodedDescription = HttpUtility.HtmlEncode(description);
            if (!Utilities.IsTextMarkupValid(encodedDescription))
            {
                return Content("The description entered contains invalid tags. Please check the formatting tags you entered and try again.");
            }

            using (GmrEntities gmrDb = GmrEntities.CreateContext())
            {
                var currentUser = Global.CurrentUser(gmrDb);
                var game = gmrDb.Games.FirstOrDefault(g => g.GameID == id);
                if (game != null
                    && (game.Host == currentUser || currentUser.IsAdmin))
                {
                    game.Description = encodedDescription;
                    gmrDb.SaveChanges();
                }
            }

            return Content(string.Empty);
        }

        [HttpPost]
        [Authorize]
        public ActionResult UpdateEncryptPassword(int id, bool encryptPassword)
        {
            using (var gmrDb = GmrEntities.CreateContext())
            {
                var gamePlayer = gmrDb.GamePlayers.FirstOrDefault(p => p.GamePlayerID == id);
                if (gamePlayer != null)
                {
                    gamePlayer.DisablePasswordEncrypt = !encryptPassword;

                    gmrDb.SaveChanges();
                }
            }

            return Content(string.Empty);
        }

        [HttpPost]
        [Authorize]
        public ActionResult UpdateAllowVacation(int id, bool allowVacation)
        {
            using (var gmrDb = GmrEntities.CreateContext())
            {
                var gamePlayer = gmrDb.GamePlayers.FirstOrDefault(p => p.GamePlayerID == id);
                if (gamePlayer != null)
                {
                    bool skipped = false;

                    gamePlayer.AllowVacation = allowVacation;
                    if (allowVacation)
                    {
                        var game = gamePlayer.Game;
                        if (game.Started != null)
                        {
                            var latestTurn = GameManager.GetLatestTurn(game);
                            if (latestTurn != null && latestTurn.UserID == gamePlayer.UserID)
                            {
                                var currentPlayer = latestTurn.User;
                                var preferences = PackagedPreferences.Get(currentPlayer);
                                if (preferences.VacationMode)
                                {
                                    skipped = GameManager.SkipCurrentPlayerOrCancelTurnTimer(game, gmrDb);
                                }
                            }
                        }
                    }

                    gmrDb.SaveChanges();

                    if (skipped)
                    {
                        GameManager.RemoveGameFromCache(gamePlayer.GameID);
                    }
                }
            }

            return Content(string.Empty);
        }

        public ActionResult Pie(int id)
        {
            var gmrDB = GmrEntities.CreateContext();
            var game = gmrDB.Games.Single(g => g.GameID == id);

            float percentage;
            if (game.Turns.Count > 0)
            {
                var lastTurn = game.Turns.OrderByDescending(t => t.Number).First();

                long turnStart = lastTurn.Started.Value.Ticks;
                long turnEnd = lastTurn.ExpiresOn.Ticks;
                long turnDuration = turnEnd - turnStart;
                long turnRemaining = lastTurn.TimeRemaining.Ticks;
                percentage = (float)turnRemaining / turnDuration;
                if (percentage <= 0)
                    percentage = 0.0f;
            }
            else percentage = 1;

            Image pieChart = Util.Pie.Generate(150, Color.FromArgb(204, 204, 204), Color.FromArgb(78, 108, 142), percentage, Util.Pie.Direction.Clockwise);
            using (MemoryStream mStream = new MemoryStream())
            {
                pieChart.Save(mStream, ImageFormat.Png);
                return File(mStream.ToArray(), "image/png");
            }

        }

        public ActionResult DisplayTurntimer(int id)
        {
            var gmrDb = GmrEntities.CreateContext();
            var game = gmrDb.Games.Single(g => g.GameID == id);
            if (game.TurnTimeLimit == null)
                return Content(String.Empty);

            return PartialView("~/Views/Shared/DisplayTemplates/TurnTimer.cshtml", new TurnTimer(game) { Editable = true });
        }

        [Authorize]
        public ActionResult EditTurnTimer(int id)
        {
            var gmrDb = GmrEntities.CreateContext();
            var game = gmrDb.Games.Single(g => g.GameID == id);
            var user = Global.CurrentUser(gmrDb);

            if (UserCanModifyInProgressGame(game, user))
            {
                return PartialView("~/Views/Shared/EditorTemplates/TurnTimer.cshtml", new TurnTimer(game) { Editable = true });
            }

            return Content(String.Empty);
        }

        const string TimeRegex = @"^((([0]?[1-9]|1[0-2])(:)[0-5][0-9]((:)[0-5][0-9])?( )?(AM|am|aM|Am|PM|pm|pM|Pm))|(([0]?[0-9]|1[0-9]|2[0-3])(:)[0-5][0-9]((:)[0-5][0-9])?))$";

        [HttpPost]
        [Authorize]
        public ActionResult RemoveTurnTimer(int id)
        {
            var gmrDb = GmrEntities.CreateContext();
            var game = gmrDb.Games.Single(g => g.GameID == id);
            var user = Global.CurrentUser(gmrDb);

            if (UserCanModifyInProgressGame(game, user))
            {
                game.TurnTimeLimit = null;
                game.SkipLimit = null;
                game.TurnTimerDays = null;
                game.TimeZone = 0;
                game.TurnTimerStart = null;
                game.TurnTimerStop = null;

                if (game.HasStarted)
                {
                    GameManager.ResetLatestTurnExpiration(game);
                    GameManager.SendTurnTimerOffEmails(game, user);
                    GameManager.SendTimerOffNotifications(game, user);
                }

                gmrDb.SaveChanges();
            }

            return Content(String.Empty);
        }

        [HttpPost]
        [Authorize]
        public ActionResult SetTurnTimer(int id, TurnTimer turnTimer, string StartStr, string StopStr)
        {
            bool timerIsNew = false;

            var gmrDb = GmrEntities.CreateContext();
            var game = gmrDb.Games.Single(g => g.GameID == id);
            var user = Global.CurrentUser(gmrDb);

            bool timesValid = !String.IsNullOrWhiteSpace(StartStr) && !String.IsNullOrWhiteSpace(StopStr);
            if (!String.IsNullOrWhiteSpace(StartStr) && !Regex.IsMatch(StartStr, TimeRegex))
            {
                ModelState.AddModelError("Start", "The value \"" + StartStr + "\' is not valid for Start.");
                timesValid = false;
            }
            if (!String.IsNullOrWhiteSpace(StopStr) && !Regex.IsMatch(StopStr, TimeRegex))
            {
                ModelState.AddModelError("Stop", "The value \"" + StopStr + "\' is not valid for Start.");
                timesValid = false;
            }

            TimeSpan? start = null;
            TimeSpan? stop = null;
            if (timesValid)
            {
                start = DateTime.Parse(StartStr).TimeOfDay;
                stop = DateTime.Parse(StopStr).TimeOfDay;
                if (start >= stop)
                    ModelState.AddModelError("StartStop", "Start must be earlier than Stop.");
            }

            if (!(turnTimer.Sunday || turnTimer.Monday || turnTimer.Tuesday || turnTimer.Wednesday || turnTimer.Thursday || turnTimer.Friday || turnTimer.Saturday))
            {
                ModelState.AddModelError("DaySelection", "Turn timer must be set to run on at least one day.");
            }

            if ((turnTimer.Days ?? 0) == 0 && (turnTimer.Hours ?? 0) == 0)
            {
                ModelState.AddModelError("InvalidTimespan", "Turn timer can't be set to zero");
            }

            if (ModelState.IsValid && UserCanModifyInProgressGame(game, user))
            {
                turnTimer.Start = start;
                turnTimer.Stop = stop;

                if (game.TurnTimeLimit == null)
                    timerIsNew = true;

                game.TurnTimeLimit = turnTimer.Days == null && turnTimer.Hours == null ? null : (int?)((turnTimer.Days ?? 0) * 24) + (turnTimer.Hours ?? 0);
                game.SkipLimit = turnTimer.SkipLimit;
                game.TurnTimerDays = turnTimer.RunsOnTheseDays(false);
                game.TimeZone = turnTimer.TimeZone;
                game.TurnTimerStart = turnTimer.Start;
                game.TurnTimerStop = turnTimer.Stop;

                if (game.HasStarted)
                {
                    if (timerIsNew)
                    {
                        GameManager.SendTurnTimerOnEmails(game, user);
                        GameManager.SendTimerOnNotifications(game, user);
                    }
                    else
                    {
                        GameManager.SendTurnTimerModifiedEmails(game, user);
                        GameManager.SendTimerModifiedNotifications(game, user);
                    }

                    GameManager.ResetLatestTurnExpiration(game);
                }

                gmrDb.SaveChanges();

                return Content(String.Empty);
            }
            else
            {
                var modelErrors = ModelState.Select(ms => ms.Value.Errors);
                string errors = "<ul style=\"padding-left: 0px\">";
                foreach (var error in modelErrors)
                {
                    foreach (var e in error)
                        errors += "<li>" + e.ErrorMessage + "</li>";
                }
                errors += "</ul>";
                return Content(errors);
            }
        }

        [Authorize]
        public ActionResult FindGames(string searchValue = null)
        {
            if (!Global.IsAdminAuthenticated())
                return RedirectToAction("Index", "Home");

            if (string.IsNullOrWhiteSpace(searchValue))
                return View();

            ViewBag.SearchValue = searchValue;

            var searchTokens = new List<string>(searchValue.ToLower().Split(' '));

            var gmrDb = GmrEntities.CreateContext();

            var games = from g in gmrDb.Games
                        where searchTokens.All(token => g.Name.ToLower().Contains(token))
                        select g;

            return View(games);
        }

    }

    class CompareMyGames : IComparer<Game>
    {
        #region IComparer<Game> Members

        public int Compare(Game g1, Game g2)
        {
            if (g1.GameID == g2.GameID)
                return 0;

            var lastTurn1 = g1.Turns.OrderByDescending(t => t.Number).FirstOrDefault();
            var lastTurn2 = g2.Turns.OrderByDescending(t => t.Number).FirstOrDefault();

            if (lastTurn1 != null && lastTurn1.UserID == Global.UserSteamID)
            {
                if (lastTurn2 != null && lastTurn2.UserID != Global.UserSteamID)
                    return -1;
            }
            else if (lastTurn2 != null && lastTurn2.UserID == Global.UserSteamID)
            {
                return 1;
            }

            return g2.Created.CompareTo(g1.Created);
        }

        #endregion
    }
}
