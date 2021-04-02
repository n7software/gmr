using CivSaveLib;
using GmrLib.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GmrLib
{
    using System.Data.Entity;
    using System.Data.Entity.Core.Objects;
    using System.Data.Entity.Infrastructure;

    public class GameManager : CivSavefile
    {
        #region Game Actions

        #region GetGamesForPlayer

        public static List<PackagedGame> GetGamesForPlayer(long userId)
        {
            try
            {
                using (GmrEntities gmrDB = GmrEntities.CreateContext())
                {
                    return GetGamesForPlayer(userId, gmrDB);
                }
            }
            catch
            {
            }

            return new List<PackagedGame>();
        }

        public static List<PackagedGame> GetGamesForPlayer(long userId, GmrEntities gmrDb)
        {
            var games = new List<PackagedGame>();

            try
            {
                if (userId > 0)
                {
                    var gameIds = GetStartedGameIdsForUserId(userId, gmrDb);

                    games.AddRange(GetGamesFromCache(gameIds));
                    games.ForEach(g => gameIds.Remove(g.GameId));

                    foreach (var game in gmrDb.Games.Where(g => gameIds.Contains(g.GameID)).Include(g => g.Players).ToList())
                    {
                        if (!IsGameInCache(game.GameID)
                            && ((game.GameType != GameType.Mod && game.GameType != GameType.ModTotalConversion)
                                || game.Turns.Count() > 1))
                        {
                            AddGamesToCache(new PackagedGame(game));
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                DebugLogger.WriteException(exc, string.Format("Getting games for player: {0}", userId));
            }

            return games;
        }

        #endregion

        #region SubmitTurn

        public static bool SubmitTurn(Turn turn, byte[] saveFileBytes, GmrEntities gmrDb, bool setHumanPlayers = true)
        {
            int ignored;
            return SubmitTurn(turn, saveFileBytes, gmrDb, out ignored, setHumanPlayers);
        }
        public static bool SubmitTurn(Turn turn, byte[] saveFileBytes, GmrEntities gmrDb, out int points, bool setHumanPlayers = true)
        {
            bool worked = SubmitTurn(turn, gmrDb, out points);
            worked &= FinishSubmitTurn(turn, gmrDb, saveFileBytes, setHumanPlayers);

            return worked;
        }

        public static bool SubmitTurn(Turn turn, GmrEntities gmrDb, out int pointsEarned)
        {
            pointsEarned = 0;

            try
            {
                if (turn != null)
                {
                    turn.Finished = DateTime.UtcNow;

                    if (turn.SkipStatus != SkipStatus.TurnTimerSkip
                        && turn.SkipStatus != SkipStatus.VacationModeSkip)
                    {
                        var outputParameter = new ObjectParameter("points", typeof(int));

                        gmrDb.CalculatePointsForTurn(turn.TurnID, turn.GameID, outputParameter);

                        pointsEarned = (int)outputParameter.Value;
                    }

                    return true;
                }
            }
            catch (Exception exc)
            {
                DebugLogger.WriteException(exc, "Submitting Turn");
            }

            return false;
        }

        public static bool FinishSubmitTurn(Turn turn, GmrEntities gmrDb, byte[] saveFileBytes, bool setHumanPlayers)
        {
            try
            {
                return FinishSubmitTurnInternal(saveFileBytes, setHumanPlayers, turn, gmrDb);
            }
            catch (Exception exc)
            {
                DebugLogger.WriteException(exc, "Finishing Turn Submit");
            }

            return false;
        }

        public static bool FinishSubmitTurnThread(object arg)
        {
            bool worked;

            try
            {
                var args = (FinishTurnArgs)arg;

                int gameId = 0;

                using (GmrEntities gmrDb = GmrEntities.CreateContext())
                {
                    Turn turn = gmrDb.Turns.FirstOrDefault(t => t.TurnID == args.TurnId);

                    worked = FinishSubmitTurnInternal(args.SaveFileBytes, args.SetHumanPlayers, turn, gmrDb);

                    if (worked)
                    {
                        gameId = turn.GameID;

                        gmrDb.SaveChanges();
                    }
                    else if (turn != null)
                    {
                        turn.Finished = null;
                        turn.Save = null;
                        turn.Points = 0;
                        turn.Skipped = 0;

                        gmrDb.SaveChanges();
                    }
                }

                if (gameId > 0)
                {
                    TrimOldTurnsFromGameOnThread(gameId);
                }
            }
            catch (Exception exc)
            {
                DebugLogger.WriteException(exc, "Finishing Turn Thread Outer");
                worked = false;
            }

            return worked;
        }

        private static bool FinishSubmitTurnInternal(byte[] saveFileBytes, bool setHumanPlayers, Turn turn, GmrEntities gmrDb)
        {
            bool worked = false;

            if (turn != null)
            {
                try
                {
                    if (turn.SkipStatus != SkipStatus.VacationModeSkip)
                    {
                        GamePlayer player = turn.GamePlayer;
                        if (player.AverageTurnTimeSpan.HasValue)
                        {
                            int turnCount = player.Game.Turns.Count(t => t.User == player.User) - 1;
                            int totalTimeSoFar = turnCount * (int)player.AverageTurnTimeSpan.Value.TotalSeconds;
                            int newTotalTime = totalTimeSoFar +
                                               (int)(turn.Finished.Value - turn.Started.Value).TotalSeconds;
                            int average = newTotalTime / (turnCount + 1);
                            player.AverageTurnTime = average;
                        }

                        if (turn.SkipStatus != SkipStatus.TurnTimerSkip)
                        {
                            gmrDb.UpdateUserPointsTotal(turn.UserID);
                        }
                    }

                    turn.Game.UnalteredSaveFileBytes = saveFileBytes;
                    byte[] modifiedBytes = (saveFileBytes != null)
                        ? EnsureSaveFileContainsCorrectPlayerInfo(turn.Game, saveFileBytes, gmrDb, setHumanPlayers)
                        : new byte[0];

                    turn.Save = new Save
                    {
                        Data = modifiedBytes,
                        Created = DateTime.UtcNow,
                        Modified = DateTime.UtcNow
                    };

                    foreach (Notification notification in turn.Notifications)
                        notification.Checked = DateTime.UtcNow;

                    worked = true;

                    if (turn.Game.GameType != GameType.Scenario || turn.Game.HasStarted)
                    {
                        worked = StartNextTurn(turn, gmrDb, modifiedBytes);
                    }
                }
                catch (Exception exc)
                {
                    worked = false;
                    DebugLogger.WriteException(exc, "Finishing Turn Thread Inner");
                }
            }

            return worked;
        }

        #endregion

        #region StartNextTurn

        public static bool StartNextTurn(Turn lastTurn, GmrEntities gmrDb, byte[] saveDataForLastTurn)
        {
            try
            {
                if (lastTurn != null)
                {
                    var game = lastTurn.Game;
                    GamePlayer nextPlayer = GetNextPlayer(game, lastTurn);

                    if (nextPlayer != null && nextPlayer.User != null)
                    {
                        var newTurn = new Turn
                        {
                            User = nextPlayer.User,
                            Number = lastTurn.Number + 1,
                            Started = DateTime.UtcNow,
                            GamePlayer = nextPlayer,
                            Game = game
                        };

                        gmrDb.Turns.Add(newTurn);

                        newTurn.ExpiresOn = newTurn.CalculateExpiration();

                        if (PackagedPreferences.Get(nextPlayer.User).VacationMode && nextPlayer.AllowVacation)
                        {
                            return SkipCurrentPlayerDueToVacation(game, gmrDb, saveDataForLastTurn);
                        }

                        SendNewTurnEmail(newTurn, lastTurn);
                        SendNewTurnNotification(newTurn);

                        return true;
                    }
                }
            }
            catch (Exception exc)
            {
                DebugLogger.WriteException(exc, "Starting next turn");
            }

            return false;
        }

        #endregion

        #region GetLatestSaveFile

        public static byte[] GetLatestSaveFileBytes(int gameId)
        {
            try
            {
                using (GmrEntities gmrDb = GmrEntities.CreateContext())
                {
                    Game game = gmrDb.Games.FirstOrDefault(g => g.GameID == gameId);
                    if (game != null)
                    {
                        return GetLatestSaveFileBytes(game);
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        public static byte[] GetLatestSaveFileBytes(Game game, bool protectPasswords = true)
        {
            byte[] resultBytes = null;

            try
            {
                Save lastSave = GetLatestSave(game);
                if (lastSave != null)
                {
                    resultBytes = lastSave.Data;

                    if (protectPasswords)
                    {
                        ProtectPlayerPasswords(game, ref resultBytes);
                    }
                }
            }
            catch
            {
            }

            return resultBytes;
        }

        #endregion

        #region GetLatestPlayerIndex

        public static int GetLatestPlayerIndex(int gameId)
        {
            byte[] lastSaveBytes = GetLatestSaveFileBytes(gameId);
            if (lastSaveBytes != null)
            {
                return GetCurrentPlayerInSaveFileBytes(lastSaveBytes);
            }

            return -1;
        }

        public static int GetLatestPlayerIndex(Game game)
        {
            byte[] lastSaveBytes = GetLatestSaveFileBytes(game);
            return GetLatestPlayerIndex(game, lastSaveBytes);
        }

        public static int GetLatestPlayerIndex(Game game, byte[] lastSaveBytes)
        {
            if (lastSaveBytes != null)
            {
                return GetCurrentPlayerInSaveFileBytes(lastSaveBytes);
            }

            return 0;
        }

        #endregion

        #region GetLatestSaveModified

        public static DateTime GetLatestSaveModified(int gameId)
        {
            try
            {
                using (GmrEntities gmrDb = GmrEntities.CreateContext())
                {
                    Game game = gmrDb.Games.FirstOrDefault(g => g.GameID == gameId);
                    if (game != null)
                    {
                        return GetLatestSaveModified(game);
                    }
                }
            }
            catch
            {
            }

            return DateTime.UtcNow;
        }

        public static DateTime GetLatestSaveModified(Game game)
        {
            DateTime resultDate = DateTime.UtcNow;

            try
            {
                if (game != null)
                {
                    Save lastSave = GetLatestSave(game);
                    if (lastSave != null)
                    {
                        resultDate = lastSave.Modified;
                    }
                }
            }
            catch
            {
            }

            return resultDate;
        }

        #endregion

        #region InvitePlayer

        public static void InvitePlayer(Game game, User playerToInvite, User sendingPlayer)
        {
            SendNewGameInviteNotification(game, playerToInvite, sendingPlayer);
            SendNewGameInviteEmail(game, playerToInvite, sendingPlayer);
        }

        #endregion

        #region JoinPlayer

        public static void JoinPlayer(Game game, User playerJoining, Civilization civ)
        {
            if (game.GameType == GameType.Scenario ||
                game.Started.HasValue)
            {
                ReplaceAIWithPlayer(game, playerJoining, civ, null);
            }
            else
            {
                int numberOfPlayers = game.Players.Count();

                game.Players.Add(new GamePlayer
                {
                    Civilization = civ.CivID == Civilization.Unknown.CivID ? null : civ,
                    User = playerJoining,
                    TurnOrder = numberOfPlayers,
                    AllowVacation = true
                });
            }

            SendPlayerJoinedNotification(game, playerJoining);
            SendPlayerJoinedEmail(game, playerJoining);
        }

        public static void ReplaceAIWithPlayer(Game game, User playerJoining, Civilization civ, GmrEntities gmrDb)
        {
            GamePlayer previousSlot = game.Players.SingleOrDefault(p => p.User == playerJoining);
            if (previousSlot != null)
                previousSlot.User = gmrDb.Users.Single(u => u.UserId == 0);

            GamePlayer player;
            if (civ.CivID == Civilization.Unknown.CivID)
                player = game.Players.First(p => p.UserID == 0);
            else
                player = game.Players.Where(p => p.UserID == 0).First(p => p.Civilization == civ);
            player.User = playerJoining;
            player.TimesSkipped = 0;
            player.AverageTurnTime = null;

            Save save = null;

            if (game.GameType == GameType.Scenario && !game.Started.HasValue)
            {
                save = game.Turns.OrderBy(t => t.Number).First().Save;
            }
            else
            {
                Turn turn = game.Turns.LastOrDefault(t => t.Save != null);
                if (turn != null)
                    save = turn.Save;
            }

            if (save != null)
            {
                byte[] saveData = save.Data;
                saveData = SetPlayerTypeInSaveFileBytes(saveData, player.TurnOrder + 1, PlayerType.Human);
                saveData = SetPlayerDifficultyInSaveFileBytes(saveData, player.TurnOrder + 1, CivDifficulty.Prince);

                if (previousSlot != null)
                {
                    saveData = SetPlayerTypeInSaveFileBytes(saveData, previousSlot.TurnOrder + 1, PlayerType.AI);
                    saveData = SetPlayerDifficultyInSaveFileBytes(saveData, previousSlot.TurnOrder + 1,
                        CivDifficulty.AiDefault);
                }

                save.Data = saveData;
            }

            RemoveGameFromCache(game.GameID);
        }

        #endregion

        #region LeavePlayer

        public static void LeavePlayer(GmrEntities gmrDb, Game game, GamePlayer playerLeaving)
        {
            User leavingUser = playerLeaving.User;
            int leavingPlayerTurnOrder = playerLeaving.TurnOrder;

            if (game.GameType == GameType.Scenario)
            {
                playerLeaving.User = gmrDb.Users.Single(u => u.UserId == 0);
            }
            else
            {
                game.Players.Remove(playerLeaving);
                gmrDb.GamePlayers.Remove(playerLeaving);

                foreach (GamePlayer player in game.Players.ToArray())
                {
                    if (player.TurnOrder > leavingPlayerTurnOrder)
                    {
                        UpdatePlayerTurnOrder(gmrDb, player, player.TurnOrder - 1);
                    }
                }
            }

            SendPlayerLeftNotification(game, leavingUser);
            SendPlayerLeftEmail(game, leavingUser);
        }

        #endregion

        #region SurrenderPlayer

        public static void SurrenderPlayer(GmrEntities gmrDb, Game game, GamePlayer playerSurrendering)
        {
            User userSurrendered = gmrDb.Users.First(u => u.UserId == playerSurrendering.UserID);

            // Set the player as AI in the database
            playerSurrendering.UserID = 0;

            int playerNumber = playerSurrendering.TurnOrder + 1;

            // Update all previous save files
            foreach (Save save in game.Turns.Where(t => t.Save != null).Select(t => t.Save))
            {
                if (save != null && save.Data != null)
                {
                    byte[] saveBytes = save.Data;

                    // Clear the password for this player in the save file
                    saveBytes = SetPlayerPasswordInSaveFileBytes(saveBytes, playerNumber, string.Empty);

                    // Set the player to be controlled by the AI
                    saveBytes = SetPlayerTypeInSaveFileBytes(saveBytes, playerNumber, PlayerType.AI);

                    // Set the difficulty for the new AI player
                    saveBytes = SetPlayerDifficultyInSaveFileBytes(saveBytes, playerNumber, CivDifficulty.AiDefault);


                    // Update the save
                    save.Data = saveBytes;
                    save.Modified = DateTime.UtcNow;
                }
            }

            // If this player is not the last human player in the game
            if (game.Players.Any(p => p.UserID != 0))
            {
                // If it's currently the surrendering player's turn
                Turn lastTurn = GetLatestTurn(game);
                if (lastTurn != null && lastTurn.User == userSurrendered)
                {
                    // If this is not the first turn of the game
                    if (lastTurn.Number > 1)
                    {
                        // Finish and go to the next player
                        Save lastSave = GetLatestSave(game);

                        lastTurn.Finished = DateTime.UtcNow;
                        byte[] saveData = (lastSave != null) ? lastSave.Data : new byte[0];
                        lastTurn.Save = new Save
                        {
                            Data = saveData,
                            Created = DateTime.UtcNow,
                            Modified = DateTime.UtcNow
                        };

                        StartNextTurn(lastTurn, gmrDb, saveData);
                    }
                    else
                    {
                        // The game is still waiting on the initial turn and hasn't been created
                        // in Civ V yet. So set this turn to the next player, who will then be 
                        // responsible for creating the game.
                        GamePlayer nextPlayer = GetNextPlayer(game, lastTurn);
                        if (nextPlayer != null)
                        {
                            lastTurn.User = nextPlayer.User;
                        }
                    }
                }

                // If this player was the game's host
                if (userSurrendered.UserId == game.HostUserId)
                {
                    // Set the next available human player as the host
                    GamePlayer nextPlayer = game.Players.FirstOrDefault(p => p.UserID != 0);
                    game.HostUserId = (nextPlayer != null) ? nextPlayer.UserID : 0;
                }

                // Notify all players in the game that the player has surrendered
                foreach (GamePlayer player in game.Players)
                {
                    if (player.UserID != 0)
                    {
                        SendPlayerSurrenderedEmail(game, userSurrendered, player.User);
                        SendPlayerSurrenderedNotification(game, userSurrendered, player.User);
                    }
                }
            }
            else
            {
                // This is the last player, so mark the game as finished
                game.Finished = DateTime.UtcNow;

                game.HostUserId = 0;

                // Delete all saves for this game
                foreach (Turn turn in game.Turns)
                {
                    Save s = turn.Save;
                    if (s != null)
                    {
                        turn.Save = null;
                        gmrDb.Saves.Remove(s);
                    }
                }
            }
        }

        #endregion

        #region TrimOldTurnsFromGame

        public static void TrimOldTurnsFromGameOnThread(int gameId)
        {
            Task.Factory.StartNew(() => TrimOldTurnsFromGame(gameId));
        }

        public static void TrimOldTurnsFromGame(int gameId)
        {
            try
            {
                using (GmrEntities gmrDb = GmrEntities.CreateContext())
                {
                    Game game = gmrDb.Games.FirstOrDefault(g => g.GameID == gameId);
                    if (game != null)
                    {
                        int humanPlayerCount = game.Players.Count(p => p.UserID != 0);
                        int amountOfTurnsToSave = humanPlayerCount * 3;

                        Turn latestFinishedTurn = game.Turns.LastOrDefault(t => t.Finished != null);
                        if (latestFinishedTurn != null)
                        {
                            if (latestFinishedTurn.Number > amountOfTurnsToSave)
                            {
                                int oldestTurnNumberToKeep = latestFinishedTurn.Number - amountOfTurnsToSave;

                                IQueryable<Save> savesToDelete =
                                    from t in gmrDb.Turns
                                    where
                                        t.GameID == game.GameID && t.Number < oldestTurnNumberToKeep &&
                                        t.Save != null
                                    select t.Save;

                                foreach (Save save in savesToDelete.ToList())
                                {
                                    gmrDb.Saves.Remove(save);
                                }

                                gmrDb.SaveChanges();
                            }
                        }
                    }
                }
            }
            catch (DbUpdateException)
            {
            }
            catch (Exception exc)
            {
                DebugLogger.WriteException(exc, "Trimming old saves");
            }
        }

        #endregion

        #region RevertTurn

        public static bool RevertTurn(GmrEntities gmrDb, Game game)
        {
            if (game != null)
            {
                Turn latestTurn = GetLatestTurn(game);
                if (latestTurn != null && latestTurn.Number > 1)
                {
                    // Make sure there will be a turn with a save file at the end of this
                    int targetTurnNumber = latestTurn.Number - 2;
                    Turn targetTurn = game.Turns.FirstOrDefault(t => t.Number == targetTurnNumber);

                    // If we've encounterd a bugged submit, check the next oldest turn until we reach the begginning
                    while (targetTurn != null && targetTurn.Save == null)
                    {
                        targetTurn = game.Turns.FirstOrDefault(t => t.Number == targetTurn.Number - 1);
                    }

                    if ((targetTurn != null && targetTurn.Save != null) ||
                        targetTurnNumber < 1)
                    {
                        do
                        {
                            if (latestTurn.Save != null)
                            {
                                gmrDb.Saves.Remove(latestTurn.Save);
                                latestTurn.Save = null;
                            }

                            foreach (Notification notification in latestTurn.Notifications.ToArray())
                            {
                                gmrDb.Notifications.Remove(notification);
                            }

                            gmrDb.Turns.Remove(latestTurn);

                            latestTurn = GetLatestTurn(game);

                        } while ((latestTurn.Number - 1) > ((targetTurn != null) ? targetTurn.Number : targetTurnNumber));

                        if (latestTurn.Save != null)
                        {
                            gmrDb.Saves.Remove(latestTurn.Save);
                            latestTurn.Save = null;
                        }

                        latestTurn.Started = DateTime.UtcNow;
                        latestTurn.Finished = null;
                        latestTurn.ExpiresOn = latestTurn.CalculateExpiration();

                        GamePlayer gamePlayer =
                            game.Players.SingleOrDefault(p => p.UserID == latestTurn.UserID);

                        if (gamePlayer == null && game.Players.Any(p => p.UserID != 0))
                        {
                            DebugLogger.WriteLine("RevertTurn", String.Format("Entering recursive revert. GameID: {0}. Missing GamePlayer: {1}", game.GameID, latestTurn.UserID));
                            return RevertTurn(gmrDb, game);
                        }

                        if (latestTurn.SkipStatus != SkipStatus.NotSkipped)
                        {
                            gamePlayer.TimesSkipped--;
                            latestTurn.SkipStatus = SkipStatus.NotSkipped;
                        }

                        return true;
                    }
                }
            }

            return false;
        }

        #endregion

        #region ResetLatestTurnExpiration

        public static void ResetLatestTurnExpiration(Game game)
        {
            var latestTurn = GetLatestTurn(game);
            if (latestTurn != null)
            {
                latestTurn.ExpiresOn = latestTurn.CalculateExpiration();
            }
        }
        #endregion

        #region Helper Methods

        private static byte[] EnsureSaveFileContainsCorrectPlayerInfo(Game game, byte[] bytes, GmrEntities gmrDb,
            bool verifyHumanity = true)
        {
            try
            {
                byte[] newBytes = bytes;

                if (game != null)
                {
                    bool isFirstSaveFile = game.Turns.Count <= 1;
                    bool isFirstInModGame = game.Turns.Count == 0;

                    foreach (GamePlayer player in game.Players)
                    {
                        int playerNumber = player.TurnOrder + 1;

                        if (player.UserID != 0)
                        {
                            newBytes = SetPlayerNameInSaveFileBytes(newBytes, playerNumber, player.User.UserName);

                            if (!game.Private)
                            {
                                newBytes = SetPlayerDifficultyInSaveFileBytes(newBytes, playerNumber,
                                    CivDifficulty.Prince);
                            }
                        }

                        if (verifyHumanity)
                            newBytes = SetPlayerTypeInSaveFileBytes(newBytes, playerNumber,
                                (player.UserID != 0) ? PlayerType.Human : PlayerType.AI);

                        if (isFirstSaveFile)
                        {
                            if (game.GameType != GameType.ModTotalConversion && game.GameType != GameType.Scenario)
                            {
                                if (player.Civilization != null)
                                {
                                    newBytes = SetPlayerCivilizationInSaveFileBytes(newBytes, playerNumber,
                                        player.Civilization);
                                }
                                else
                                {
                                    Civilization detectedCivilization = GetPlayerCivilizationFromSaveFileBytes(newBytes,
                                        playerNumber, gmrDb);
                                    if (detectedCivilization != null)
                                    {
                                        player.Civilization = detectedCivilization;
                                    }
                                }
                            }
                        }
                    }


                    if (isFirstSaveFile)
                    {
                        newBytes = SetGameTypeInSaveFileBytes(newBytes, SaveFileType.HotSeat);
                    }

                    if (!isFirstInModGame)
                        newBytes = SetCurrentPlayerInSaveFileBytes(newBytes, GetNextPlayer(game).TurnOrder);
                }

                return newBytes;
            }
            catch (Exception exc)
            {
                DebugLogger.WriteException(exc, "Fixing up save file");

                return bytes;
            }
        }

        public static void UpdateUserCurrentTurnsPassword(long userId)
        {
            using (var gmrDb = GmrEntities.CreateContext())
            {
                var user = gmrDb.Users.FirstOrDefault(u => u.UserId == userId);
                if (user != null)
                {
                    dynamic playerPreferences = PackagedPreferences.Get(user);
                    string playerPassword = playerPreferences.GamePassword;

                    IEnumerable<Game> currentGames = from t in user.Turns
                                                     where t.Finished == null
                                                     select t.Game;

                    foreach (Game game in currentGames)
                    {
                        int playerNumber = (from gp in game.Players
                                            where gp.UserID == user.UserId
                                            select gp.TurnOrder)
                            .FirstOrDefault() + 1;

                        Turn lastSubmittedTurn = game.Turns.Where(t => t.Save != null)
                            .OrderByDescending(t => t.Number)
                            .FirstOrDefault();

                        if (lastSubmittedTurn != null)
                        {
                            lastSubmittedTurn.Save.Data = SetPlayerPasswordInSaveFileBytes(lastSubmittedTurn.Save.Data,
                                playerNumber, playerPassword);
                            lastSubmittedTurn.Save.Modified = DateTime.UtcNow;
                        }
                    }

                    gmrDb.SaveChanges();
                }
            }
        }

        public static void SkipPlayerInVacationedGames(long userId)
        {
            using (var gmrDb = GmrEntities.CreateContext())
            {
                var gamePlayers = gmrDb.GamePlayers.Where(g => g.UserID == userId).ToList();

                if (gamePlayers.Count > 0)
                {
                    var gameIds = new List<int>();

                    foreach (var gp in gamePlayers)
                    {
                        if (gp.AllowVacation && gp.Game.Started != null)
                        {
                            var turn = GameManager.GetLatestTurn(gp.Game);
                            if (turn != null && turn.GamePlayer == gp)
                            {
                                GameManager.SkipCurrentPlayerOrCancelTurnTimer(gp.Game, gmrDb);

                                gameIds.Add(gp.GameID);
                            }
                        }

                    }

                    gmrDb.SaveChanges();

                    if (gameIds.Any())
                    {
                        RemoveGamesFromCache(gameIds);
                    }
                }
            }
        }

        public static Turn GetLatestTurn(Game game)
        {
            Turn lastTurn = null;

            if (game != null)
            {
                lastTurn = game.Turns.OrderByDescending(t => t.Number)
                    .FirstOrDefault();

                if (lastTurn != null)
                {
                    lastTurn.Game = game;
                }
            }

            return lastTurn;
        }

        public static Save GetLatestSave(Game game)
        {
            Save result = null;
            if (game != null)
            {
                Turn lastTurn = game.Turns.Where(t => t.Save != null)
                    .OrderByDescending(t => t.Number)
                    .FirstOrDefault();
                if (lastTurn != null)
                {
                    result = lastTurn.Save;
                }
            }

            return result;
        }

        public static GamePlayer GetNextPlayer(Game game, Turn lastTurn = null)
        {
            int nextPlayerTurnOrder = 0;
            GamePlayer nextPlayer = null;

            if (lastTurn == null)
            {
                lastTurn = GetLatestTurn(game);
            }

            if (lastTurn != null)
            {
                if (lastTurn.Number == 0)
                    return lastTurn.GamePlayer;

                nextPlayerTurnOrder = lastTurn.GamePlayer.TurnOrder;
            }

            var gamePlayers = game.Players.OrderBy(gp => gp.TurnOrder).ToList();

            do
            {
                nextPlayerTurnOrder++;

                if (nextPlayerTurnOrder > gamePlayers.OrderBy(gp => gp.TurnOrder).Last().TurnOrder)
                {
                    nextPlayerTurnOrder = gamePlayers.OrderBy(gp => gp.TurnOrder).First().TurnOrder;
                }

                nextPlayer = gamePlayers.FirstOrDefault(gp => gp.TurnOrder == nextPlayerTurnOrder);
            } while (nextPlayer == null || nextPlayer.UserID == 0);

            return nextPlayer;
        }

        public static void UpdatePlayerTurnOrder(GmrEntities gmrDb, GamePlayer p, int newIndex)
        {
            var updated = new GamePlayer();
            updated.User = p.User;
            updated.TurnOrder = newIndex;
            updated.Game = p.Game;
            updated.Civilization = p.Civilization;
            updated.AllowVacation = p.AllowVacation;

            gmrDb.GamePlayers.Remove(p);
            gmrDb.GamePlayers.Add(updated);
        }

        private static void ProtectPlayerPasswords(Game game, ref byte[] saveFileBytes)
        {
            if (game != null && saveFileBytes != null)
            {
                Turn latestTurn = GetLatestTurn(game);
                if (latestTurn != null)
                {
                    User currentPlayer = latestTurn.User;
                    if (currentPlayer != null)
                    {
                        IEnumerable<int> nonPrintableChars = Enumerable.Range(1, 31);
                        var rnd = new Random();

                        foreach (GamePlayer player in game.Players)
                        {
                            PlayerType playerType = GetPlayerTypeFromSaveFileBytes(saveFileBytes, player.TurnOrder + 1);

                            if (playerType == PlayerType.AI)
                            {
                                saveFileBytes = SetPlayerPasswordInSaveFileBytes(saveFileBytes, player.TurnOrder + 1,
                                    String.Empty);
                            }
                            else if (player.User != currentPlayer && !player.DisablePasswordEncrypt)
                            {
                                byte[] pwdBytes = nonPrintableChars.OrderBy(x => rnd.Next())
                                    .Take(rnd.Next(2, 7))
                                    .Select(i => (byte)i)
                                    .ToArray();

                                string pwdString = Encoding.ASCII.GetString(pwdBytes);

                                saveFileBytes = SetPlayerPasswordInSaveFileBytes(saveFileBytes, player.TurnOrder + 1,
                                    pwdString);
                            }
                            else
                            {
                                dynamic playerPreferences = PackagedPreferences.Get(player.User);
                                string playerPassword = playerPreferences.GamePassword;

                                saveFileBytes = SetPlayerPasswordInSaveFileBytes(saveFileBytes, player.TurnOrder + 1,
                                    playerPassword);
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #endregion

        #region Packaged Game Cache

        private static readonly Dictionary<int, PackagedGame> GameCache = new Dictionary<int, PackagedGame>();
        private static readonly object _lockGameCache = new object();

        public static int GetNumberOfGamesInCache()
        {
            int count = 0;

            lock (_lockGameCache)
            {
                count = GameCache.Count;
            }

            return count;
        }

        private static void AddGamesToCache(PackagedGame game)
        {
            AddGamesToCache(new List<PackagedGame> { game });
        }

        private static void AddGamesToCache(List<PackagedGame> games)
        {
            lock (_lockGameCache)
            {
                //var gameCache = GameCache;

                foreach (PackagedGame game in games)
                {
                    GameCache[game.GameId] = game;
                }

                //GameCache = gameCache;
            }
        }

        private static bool IsGameInCache(int gameId)
        {
            lock (_lockGameCache)
            {
                return GameCache.ContainsKey(gameId);
            }
        }

        private static List<PackagedGame> GetGamesFromCache(List<int> gameIds)
        {
            var games = new List<PackagedGame>();

            lock (_lockGameCache)
            {
                //var gameCache = GameCache;

                foreach (int gameId in gameIds)
                {
                    if (GameCache.ContainsKey(gameId))
                    {
                        games.Add(GameCache[gameId]);
                    }
                }
            }

            return games;
        }

        public static void RemoveGameFromCache(int gameId)
        {
            lock (_lockGameCache)
            {
                GameCache.Remove(gameId);
            }
        }

        public static void RemoveGamesFromCache(IEnumerable<int> gameIds)
        {
            lock (_lockGameCache)
            {
                foreach (int id in gameIds)
                {
                    GameCache.Remove(id);
                }
            }
        }


        static readonly Uri OtherInstanceUri = ConfigurationManager.AppSettings["OtherInstance"] != null
            ? new Uri(ConfigurationManager.AppSettings["OtherInstance"])
            : null;
        private static void ReportRemovedGamesToOtherInstance(IEnumerable<int> gameIds)
        {
            // Deprecated

            //if (OtherInstanceUri != null && gameIds.Any())
            //{
            //    var gameIdsCopy = new List<int>(gameIds);
            //    await Task.Run(() =>
            //    {
            //        var requestUri = new Uri(OtherInstanceUri, "./dev/RemoveGamesFromCache?gameIds=" + string.Join(",", gameIdsCopy));
            //        using (var client = new WebClient())
            //        {
            //            try
            //            { client.DownloadData(requestUri); }
            //            catch { }
            //        }
            //    });
            //}
        }

        public static void ClearAllGamesFromCache()
        {
            lock (_lockGameCache)
            {
                GameCache.Clear();
            }
        }

        #endregion

        #region User AuthKey Cache
        private static Dictionary<string, long> _authKeys = new Dictionary<string, long>();
        private static object _lockAuthKeys = new object();

        public static long? GetUserIdFromAuthKey(string authKey, GmrEntities gmrDb)
        {
            lock (_lockAuthKeys)
            {
                if (_authKeys.ContainsKey(authKey))
                {
                    return _authKeys[authKey];
                }

                var userId = gmrDb.Users.Where(u => u.AuthKey == authKey).Select(u => u.UserId).FirstOrDefault();
                if (userId != 0)
                {
                    lock (_lockAuthKeys)
                    {
                        _authKeys[authKey] = userId;
                    }
                }

                return userId != 0 ? (long?)userId : null;
            }
        }
        #endregion

        #region Started GameID Cache
        private static Dictionary<long, List<int>> _startedGameIds = new Dictionary<long, List<int>>();
        private static object _lockStartedGameIds = new object();

        public static List<int> GetStartedGameIdsForUserId(long userId, GmrEntities gmrDb)
        {
            lock (_lockStartedGameIds)
            {
                if (_startedGameIds.ContainsKey(userId))
                {
                    return new List<int>(_startedGameIds[userId]);
                }
            }

            var gameIds = gmrDb.UsersGamesStarteds.Where(i => i.UserId == userId).Select(i => i.GameID).ToList();

            lock (_lockStartedGameIds)
            {
                _startedGameIds[userId] = new List<int>(gameIds);
            }

            return gameIds;
        }

        public static void ClearStartedGameIdsForUsers(IEnumerable<long> userIds)
        {
            lock (_lockStartedGameIds)
            {
                foreach (var userId in userIds)
                {
                    _startedGameIds.Remove(userId);
                }
            }
        }

        public static void ClearStartedGameIds()
        {
            lock (_lockStartedGameIds)
            {
                _startedGameIds.Clear();
            }
        }
        #endregion

        #region Player Skipping

        public static bool SkipCurrentPlayerOrCancelTurnTimer(Game game, GmrEntities gmrDB, byte[] saveData)
        {
            int numberOfHumans = game.NumberOfHumanPlayers;

            int currentSkipStreak =
                game.Turns.Where(t => t.Finished != null)
                    .OrderByDescending(t => t.Number)
                    .TakeWhile(t => t.Skipped != (int)SkipStatus.NotSkipped)
                    .Count();

            if (currentSkipStreak >= numberOfHumans - 2)
            {
                game.TurnTimeLimit = null;

                for (int i = 0; i < currentSkipStreak; i++)
                {
                    RevertTurn(gmrDB, game);
                }

                SendTurnTimerOffEmails(game, null);

                return true;
            }
            else
            {
                return SkipCurrentPlayer(game, gmrDB, saveData);
            }
        }

        public static bool SkipCurrentPlayerOrCancelTurnTimer(Game game, GmrEntities gmrDB)
        {
            Turn lastTurnWithSave = game.Turns.OrderByDescending(t => t.Number)
                                              .FirstOrDefault(t => t.Save != null);

            if (lastTurnWithSave != null)
            {
                if (lastTurnWithSave.Save.Data != null)
                {
                    return SkipCurrentPlayerOrCancelTurnTimer(game, gmrDB, lastTurnWithSave.Save.Data);
                }
                else
                {
                    game.TurnTimeLimit = null;
                    return RevertTurn(gmrDB, game);
                }
            }

            return false;
        }

        public static bool SkipCurrentPlayerDueToVacation(Game game, GmrEntities gmrDB, byte[] saveData)
        {
            return SkipCurrentPlayer(game, gmrDB, saveData);
        }

        private static bool SkipCurrentPlayer(Game game, GmrEntities gmrDB, byte[] saveData)
        {
            Turn pendingTurn = game.Turns.OrderByDescending(t => t.Number).First();
            GamePlayer skippedUser = pendingTurn.GamePlayer;
            int skippedUserTurnIndex = skippedUser.TurnOrder + 1;

            byte[] modifiedSave = SetPlayerTypeInSaveFileBytes(saveData, skippedUserTurnIndex, PlayerType.AI);

            bool vacationMode = PackagedPreferences.Get(skippedUser.User).VacationMode;
            if (vacationMode && skippedUser.AllowVacation)
            {
                pendingTurn.SkipStatus = SkipStatus.VacationModeSkip;
            }
            else
            {
                pendingTurn.SkipStatus = SkipStatus.TurnTimerSkip;
                skippedUser.TimesSkipped++;
            }

            if (SubmitTurn(pendingTurn, modifiedSave, gmrDB, false))
            {
                if (skippedUser.TimesSkipped >= game.SkipLimit
                    || (pendingTurn.SkipStatus == SkipStatus.VacationModeSkip
                        && skippedUser.User.LastLoggedIn < DateTime.UtcNow.AddDays(-20)))
                {
                    SurrenderPlayer(gmrDB, game, skippedUser);
                }

                return true;
            }

            pendingTurn.SkipStatus = SkipStatus.NotSkipped;

            if (!vacationMode)
            {
                skippedUser.TimesSkipped--;
            }

            return false;
        }

        #endregion

        #region Save File Manipulation

        #region Get/Set Civilization

        public static Civilization GetPlayerCivilizationFromSaveFileBytes(byte[] saveFileBytes, int playerNumber,
            GmrEntities gmrDb)
        {
            string civStringId = GetStringFromSaveFileBytes(saveFileBytes, playerNumber,
                CivilizationSectionNumber);
            return gmrDb.Civilizations.FirstOrDefault(c => c.InGameKey == civStringId);
        }

        private static byte[] SetPlayerCivilizationInSaveFileBytes(byte[] saveFileBytes, int playerNumber,
            Civilization civ)
        {
            saveFileBytes = SetStringInSaveFileBytes(saveFileBytes, playerNumber, civ.InGameKey,
                CivilizationSectionNumber);
            saveFileBytes = SetStringInSaveFileBytes(saveFileBytes, playerNumber, civ.InGameLeaderKey,
                LeaderSectionNumber);

            return saveFileBytes;
        }

        #endregion

        #endregion

        #region Email and Notification Actions

        private static readonly Dictionary<string, string> EmailTagReplacements = new Dictionary<string, string>
        {
            {
                "<h1>",
                @"<h1 style=""margin-top: 0px; padding-top: 5px; margin-bottom: 5px; color: #7BB036; font-size:200%;"">"
            },
            {
                "<h2>",
                @"<h2 style=""margin-top: 0px; padding-top: 5px; margin-bottom: 5px; color: #7BB036; font-size:200%; font-size:175%;"">"
            },
            {"<h3>", @"<h3 style=""color: #7BB036; margin-top:0px; margin-bottom:5px; font-size:150%;"">"},
            {"<a ", @"<a style=""color: #419EF3;"""}
        };

        public static void SendSkippedNotification(Game game, User skippedUser)
        {
            skippedUser.ReceivedNotifications.Add(
                new Notification
                {
                    NotificationType = (int)NotificationType.Skipped,
                    Sent = DateTime.UtcNow,
                    Game = game
                });
        }

        public static void SendTimerOnNotifications(Game game, User sendingUser)
        {
            foreach (User user in game.Players.Select(p => p.User))
            {
                if (user != sendingUser)
                {
                    user.ReceivedNotifications.Add(
                        new Notification
                        {
                            NotificationType = (int)NotificationType.TurnTimerOn,
                            SendingUser = sendingUser,
                            Sent = DateTime.UtcNow,
                            Game = game
                        });
                }
            }
        }

        public static void SendTimerOffNotifications(Game game, User sendingUser)
        {
            foreach (User user in game.Players.Select(p => p.User))
            {
                if (user != sendingUser)
                {
                    user.ReceivedNotifications.Add(
                        new Notification
                        {
                            NotificationType = (int)NotificationType.TurnTimerOff,
                            SendingUser = sendingUser,
                            Sent = DateTime.UtcNow,
                            Game = game
                        });
                }
            }
        }

        public static void SendTimerModifiedNotifications(Game game, User sendingUser)
        {
            foreach (User user in game.Players.Select(p => p.User))
            {
                if (user != sendingUser)
                {
                    user.ReceivedNotifications.Add(
                        new Notification
                        {
                            NotificationType = (int)NotificationType.TurnTimerChanged,
                            SendingUser = sendingUser,
                            Sent = DateTime.UtcNow,
                            Game = game
                        });
                }
            }
        }

        public static void SendSkippedEmail(Game game, User skippedUser)
        {
            if (PackagedPreferences.Get(skippedUser).EmailSkipped && !String.IsNullOrWhiteSpace(skippedUser.Email))
            {
                GamePlayer gamePlayer = game.Players.SingleOrDefault(p => p.UserID == skippedUser.UserId);
                if (gamePlayer != null)
                {
                    int skipsRemaining = (game.SkipLimit ?? 0) - gamePlayer.TimesSkipped;
                    string timeOrTimes = skipsRemaining == 1 ? "time" : "times";
                    string skipWarning = game.SkipLimit == null
                        ? String.Empty
                        : "<p>If you get skipped " + skipsRemaining + " more " + timeOrTimes +
                          ", you'll be kicked, so be careful!</p>";
                    string emailHtml = CreateEmail(
                        String.Format(
                            @"<h3>You have been skipped in your game <a href=""http://multiplayerrobot.com/Game/Details/{0}"" class=""ah3"">{1}</a></h3>" +
                            "<p>The turn timer for your turn has expired and you have been skipped. The AI will take your turn. Sorry!</p>" +
                            skipWarning
                            , game.GameID, game.Name));

                    Task.Factory.StartNew(
                        () => WebHelpers.SendEmail(skippedUser.Email, "You've been skipped", emailHtml));
                }
            }
        }

        public static void SendTurnTimerOnEmails(Game game, User sendingUser)
        {
            SendTurnTimerOnOrModifiedEmails(game, sendingUser, true);
        }

        public static void SendTurnTimerModifiedEmails(Game game, User sendingUser)
        {
            SendTurnTimerOnOrModifiedEmails(game, sendingUser, false);
        }

        public static void SendTurnTimerOffEmails(Game game, User sendingUser)
        {
            var turnTimer = new TurnTimer(game);
            SendTurnTimerEmailToGamePlayers(game, sendingUser, "Turn timer disabled",
                String.Format(
                    @"<h3>The turn timer has been disabled in your game <a href=""http://multiplayerrobot.com/Game/Details/{0}"" class=""ah3"">{1}</a></h3>" +
                    (sendingUser != null
                        ? "<p>But don't take too long to take your turns now!</p>"
                        : ("<p>This happened because everyone but one player got skipped. " +
                           "This would have caused the Civ V to get stuck on that player in-game. To keep this from happening, the turn timer is now off, and all skipped turns in the" +
                           " current streak have been reverted.</p>"))
                    , game.GameID, game.Name));
        }

        private static void SendTurnTimerOnOrModifiedEmails(Game game, User sendingUser, bool newTimer)
        {
            var turnTimer = new TurnTimer(game);
            string timeRangeBlock = game.TurnTimerStart == null
                ? String.Empty
                : "Additionally, turns can't expire before {6} or after {7}. ";
            string skipLimitBlock = game.SkipLimit == null
                ? String.Empty
                : "But, be careful: if you get skipped {8} times, you'll be kicked!";
            string action = newTimer ? "enabled" : "modified";
            SendTurnTimerEmailToGamePlayers(game, sendingUser, "Turn timer " + action,
                String.Format(
                    "<h3>The turn timer has been " + action +
                    @" in your game <a href=""http://multiplayerrobot.com/Game/Details/{0}"" class=""ah3"">{1}</a></h3>" +
                    "<p>It has been set to {2} days and {3} hours. It'll run on {4} ({5}).</p>" +
                    "<p>" + timeRangeBlock + skipLimitBlock + "</p>"
                    , game.GameID, game.Name, turnTimer.Days, turnTimer.Hours, turnTimer.RunsOnTheseDays(true),
                    turnTimer.FriendlyTimeZone, turnTimer.FriendlyStart,
                    turnTimer.FriendlyStop, turnTimer.SkipLimit
                    ));
        }

        private static void SendTurnTimerEmailToGamePlayers(Game game, User sendingUser, string emailSubject,
            string emailBody)
        {
            foreach (User user in game.Players.Select(p => p.User))
            {
                if (user != sendingUser)
                {
                    string email = user.Email;
                    if (PackagedPreferences.Get(user).EmailTurnTimerChanges &&
                        !String.IsNullOrWhiteSpace(email))
                    {
                        string emailHtmlBody =
                            CreateEmail(emailBody);

                        Task.Factory.StartNew(() => WebHelpers.SendEmail(email, emailSubject, emailHtmlBody));
                    }
                }
            }
        }

        public static void SendNewTurnEmail(Turn newTurn, Turn lastTurn)
        {
            if (newTurn != null && newTurn.User.GetPackagedPreferences().EmailTurnNotify)
            {
                string playerEmail = newTurn.User.Email;
                if (!string.IsNullOrWhiteSpace(playerEmail))
                {
                    string lastPlayerName = (lastTurn != null)
                        ? lastTurn.User.UserName
                        : "The previous user";


                    string emailHtmlBody =
                        CreateEmail(
                            string.Format(
                                @"<h3>It is now your turn in <a href=""http://multiplayerrobot.com/Game/Details/{0}"" class=""ah3"">{1}</a></h3>
                                <p><a href=""http://multiplayerrobot.com/Game/Details/{0}"">{2}</a> just finished their turn and submitted the save file. You may now either download the save file from the link above or simply launch your turn from the <a href=""http://multiplayerrobot.com/Download"">GMR Desktop Client</a>.</p>",
                                newTurn.GameID,
                                newTurn.Game.Name,
                                lastPlayerName
                                ));

                    Task.Factory.StartNew(() => WebHelpers.SendEmail(playerEmail, "It's your turn", emailHtmlBody));
                }
            }
        }

        public static void SendGameCancelledNotifications(Game game)
        {
            if (game != null)
            {
                foreach (User user in game.Players.Where(p => p.UserID > 0).Select(p => p.User))
                {
                    if (user != game.Host)
                    {
                        user.ReceivedNotifications.Add(
                            new Notification
                            {
                                ReceivingUser = user,
                                SendingUser = game.Host,
                                NotificationType = (int)NotificationType.GameCancelled,
                                Sent = DateTime.UtcNow
                            }
                            );
                    }
                }
            }
        }

        public static void SendGameCancelledEmail(Game game)
        {
            if (game != null)
            {
                foreach (GamePlayer player in game.Players.Where(p => p.UserID > 0))
                {
                    string playerEmail = player.User.Email;

                    if (!string.IsNullOrWhiteSpace(playerEmail) &&
                        player.User != game.Host &&
                        player.User.GetPackagedPreferences().EmailGameEnds)
                    {
                        string emailHtmlBody =
                            CreateEmail(
                                string.Format(
                                    @"<h3><span>{0}</span> has been cancelled</h3>
                                                  <p>{1}, who was the host of {0}, has cancelled the game before it started. Sorry about that :(.</p>",
                                    game.Name,
                                    game.Host.UserName
                                    ));

                        Task.Factory.StartNew(
                            () => WebHelpers.SendEmail(playerEmail, "A game was cancelled", emailHtmlBody));
                    }
                }
            }
        }

        public static void SendGameEmailInvitation(Game game, string emailAddress)
        {
            if (game != null && !string.IsNullOrWhiteSpace(emailAddress))
            {
                Guid inviteToken = game.GenerateInviteToken();

                string emailHtmlBody =
                    CreateEmail(
                        string.Format(
                            @"<h3><a href=""http://multiplayerrobot.com/Game/Details/{1}"" class=""ah3"">{0} has invited you to {2}</a></h3>
                                    <p><span style=""color: #7BB036;"">{0}</span> has invited you to a game of Civilization V on Giant Multiplayer Robot! <a href=""http://multiplayerrobot.com/Game/Join/{1}?token=" +
                            inviteToken +
                            @""">Click here to join.</a></p> <p>If you're new to GMR don't worry, you'll be able to quickly sign-in using your Steam account and be ready to play. Have fun!</p>",
                            game.Host.UserName,
                            game.GameID,
                            game.Name
                            ));

                Task.Factory.StartNew(
                    () =>
                        WebHelpers.SendEmail(emailAddress, "You've been invited to the Giant Multiplayer Robot",
                            emailHtmlBody));
            }
        }

        private static void SendNewTurnNotification(Turn newTurn)
        {
            if (newTurn != null)
            {
                newTurn.Notifications.Add(new Notification
                {
                    Game = newTurn.Game,
                    ReceivingUser = newTurn.User,
                    NotificationType = (int)NotificationType.YourTurn,
                    Sent = DateTime.UtcNow
                });
            }
        }

        public static void SendNewGameCommentNotificationAndEmail(Game game, User sendingUser, string commentBody)
        {
            SendNewGameCommentNotification(game, sendingUser);
            SendNewGameCommentEmail(game, sendingUser, commentBody);
        }

        private static void SendNewGameCommentNotification(Game game, User sendingUser)
        {
            if (game != null)
            {
                foreach (GamePlayer player in game.Players)
                {
                    if (player.User != sendingUser && player.UserID > 0)
                    {
                        game.Notifications.Add(new Notification
                        {
                            Game = game,
                            ReceivingUser = player.User,
                            SendingUser = sendingUser,
                            NotificationType = (int)NotificationType.GameComment,
                            Sent = DateTime.UtcNow
                        });
                    }
                }
            }
        }

        private static void SendNewGameCommentEmail(Game game, User sendingUser, string commentBody)
        {
            if (game != null)
            {
                foreach (GamePlayer player in game.Players)
                {
                    if (player.User != sendingUser && player.UserID > 0)
                    {
                        if (player.User.GetPackagedPreferences().EmailComment &&
                            !string.IsNullOrWhiteSpace(player.User.Email))
                        {
                            string email = CreateEmail(
                                String.Format(
                                    @"<h3><a href=""http://multiplayerrobot.com/Game/Details/{1}"" class=""ah3"">{0} has commented on {2}</a></h3>
                                <p>{0} has commented on your game of Civilization V on Giant Multiplayer Robot! <a href=""http://multiplayerrobot.com/Game/Details/{1}"">Click here to view details.</a></p>
                                <br /><br /><h3>Comment</h3><p>{3}</p>"
                                    , sendingUser.UserName,
                                    game.GameID,
                                    game.Name,
                                    commentBody));

                            string playerEmail = player.User.Email;
                            Task.Factory.StartNew(
                                () =>
                                    WebHelpers.SendEmail(playerEmail, string.Format("Comment on {0}", game.Name), email));
                        }
                    }
                }
            }
        }

        public static void SendMessageCommentNotificationAndEmail(Message message, User sendingUser, string commentBody)
        {
            // I removed the notification, it seemed redundant since we already highlight the messages notification icon
            SendMessageCommentEmail(message, sendingUser, commentBody);
        }

        private static void SendMessageCommentEmail(Message message, User sendingUser, string commentBody)
        {
            if (message != null)
            {
                foreach (User recipient in message.Recipients.Select(r => r.Recipient))
                {
                    if (recipient != sendingUser && recipient.UserId > 0)
                    {
                        if (recipient.GetPackagedPreferences().EmailNewMessage &&
                            !string.IsNullOrWhiteSpace(recipient.Email))
                        {
                            string email = CreateEmail(
                                String.Format(
                                    @"<h3><a href=""http://multiplayerrobot.com/User/Messages#{1}"" class=""ah3"">{0} has sent you a new message</a></h3>
                                <p><a href=""http://multiplayerrobot.com/Community#{2}"">{0}</a> has sent you a new private message on Giant Multiplayer Robot! <a href=""http://multiplayerrobot.com/User/Messages#{1}"">Click here to view it.</a></p>
                                <br /><br /><h3>Comment</h3><p>{3}</p>"
                                    , sendingUser.UserName,
                                    message.Id,
                                    sendingUser.UserId,
                                    commentBody));

                            string playerEmail = recipient.Email;
                            Task.Factory.StartNew(
                                () =>
                                    WebHelpers.SendEmail(playerEmail,
                                        string.Format("Message from {0}", sendingUser.UserName), email));
                        }
                    }
                }
            }
        }

        public static void SendSupportThankYou(User user, User giftUser, bool limitIncreased)
        {
            if (user != null && !string.IsNullOrWhiteSpace(user.Email))
            {
                string email = CreateEmail(
                    string.Format(@"<h3>Thank you for supporting Giant Multiplayer Robot!</h3>
                                <p>{0}</p>
                                <p>We're glad that you've found enough value in the service we provide to support us or that you're having so much
                                   fun you want to play more GMR games! Here at GMR we appreciate all the support we get. When you support us it 
                                   enables us to maintain our costs and continue to develop fixes and enhancements for the whole GMR community.
                                   Again, thank you so much!</p>",
                        limitIncreased
                            ? (giftUser == null)
                                ? "Your game limit has been increased!"
                                : string.Format("You have increased the game limit for {0}!", giftUser.UserName)
                            : string.Empty
                        ));

                Task.Factory.StartNew(() => WebHelpers.SendEmail(user.Email, "Thank you for your support", email));
            }

            if (giftUser != null && !string.IsNullOrWhiteSpace(giftUser.Email))
            {
                string email = CreateEmail(
                    string.Format(@"<h3>{0}</h3>
                                <p>{1} has supported GMR on your behalf.{2} Be sure to send them a big thank you!</p>",
                        limitIncreased
                            ? "Your game limit at Giant Multiplayer Robot has been increased!"
                            : "Support has been given to Giant Multiplayer Robot on your behalf!",
                        user.UserName,
                        limitIncreased
                            ? " This has effectively increased the amount of games you can play on GMR."
                            : string.Empty
                        ));

                Task.Factory.StartNew(
                    () =>
                        WebHelpers.SendEmail(giftUser.Email,
                            limitIncreased ? "Increased game limit" : "Thank you for your support", email));
            }
        }


        private static void SendNewGameInviteNotification(Game game, User receivingUser, User sendingUser)
        {
            game.Notifications.Add(new Notification
            {
                ReceivingUser = receivingUser,
                SendingUser = sendingUser,
                NotificationType = (int)NotificationType.GameInvite,
                Sent = DateTime.UtcNow
            });
        }

        private static void SendNewGameInviteEmail(Game game, User receivingUser, User sendingUser)
        {
            if (receivingUser.GetPackagedPreferences().EmailGameInvites &&
                !String.IsNullOrWhiteSpace(receivingUser.Email))
            {
                string email = CreateEmail(
                    String.Format(
                        @"<h3><a href=""http://multiplayerrobot.com/Game/Details/{1}"" class=""ah3"">{0} has invited you to {2}</a></h3>
                    <p>You have been invited to a game of Civilization V on Giant Multiplayer Robot! <a href=""http://multiplayerrobot.com/Game/Join/{1}"">Click here to join.</a></p>"
                        , sendingUser.UserName,
                        game.GameID,
                        game.Name));

                Task.Factory.StartNew(
                    () =>
                        WebHelpers.SendEmail(receivingUser.Email, "You have been invited to a Civ V game on GMR", email));
            }
        }

        private static void SendPlayerJoinedNotification(Game game, User playerJoining)
        {
            game.Notifications.Add(new Notification
            {
                ReceivingUser = game.Host,
                SendingUser = playerJoining,
                NotificationType = (int)NotificationType.PlayerJoinedGame,
                Sent = DateTime.UtcNow
            });
        }

        private static void SendPlayerLeftNotification(Game game, User playerLeaving)
        {
            game.Notifications.Add(new Notification
            {
                ReceivingUser = game.Host,
                SendingUser = playerLeaving,
                NotificationType = (int)NotificationType.PlayerLeftGame,
                Sent = DateTime.UtcNow
            });
        }

        private static void SendPlayerSurrenderedNotification(Game game, User playerSurrendering, User playerReceiving)
        {
            game.Notifications.Add(new Notification
            {
                ReceivingUser = playerReceiving,
                SendingUser = playerSurrendering,
                NotificationType = (int)NotificationType.PlayerLeftGame,
                Sent = DateTime.UtcNow
            });
        }

        private static void SendPlayerJoinedEmail(Game game, User playerJoining)
        {
            if (game.Host.GetPackagedPreferences().EmailPlayerJoinsGame && !String.IsNullOrWhiteSpace(game.Host.Email))
            {
                string email = CreateEmail(
                    String.Format(
                        @"<h3><a href=""http://multiplayerrobot.com/Game/Details/{1}"" class=""ah3"">{0} has joined {2}</a></h3>
                    <p>{0} has joined your game of Civilization V on Giant Multiplayer Robot! <a href=""http://multiplayerrobot.com/Game/Details/{1}"">Click here to view details.</a></p>"
                        , playerJoining.UserName,
                        game.GameID,
                        game.Name));

                Task.Factory.StartNew(
                    () => WebHelpers.SendEmail(game.Host.Email, "A player has joined your Civ V game on GMR", email));
            }
        }

        private static void SendPlayerLeftEmail(Game game, User playerLeaving)
        {
            if (game.Host.GetPackagedPreferences().EmailPlayerJoinsGame && !String.IsNullOrWhiteSpace(game.Host.Email))
            {
                string email = CreateEmail(
                    String.Format(
                        @"<h3><a href=""http://multiplayerrobot.com/Game/Details/{1}"" class=""ah3"">{0} has left {2}</a></h3>
                    <p>{0} has left your game of Civilization V on Giant Multiplayer Robot! <a href=""http://multiplayerrobot.com/Game/Details/{1}"">Click here to view details.</a></p>"
                        , playerLeaving.UserName,
                        game.GameID,
                        game.Name));

                Task.Factory.StartNew(
                    () => WebHelpers.SendEmail(game.Host.Email, "A player has left your Civ V game on GMR", email));
            }
        }

        private static void SendPlayerSurrenderedEmail(Game game, User playerSurrendering, User playerReceiving)
        {
            if (playerReceiving.GetPackagedPreferences().EmailPlayerJoinsGame &&
                !String.IsNullOrWhiteSpace(playerReceiving.Email))
            {
                string email = CreateEmail(
                    String.Format(
                        @"<h3><a href=""http://multiplayerrobot.com/Game/Details/{1}"" class=""ah3"">{0} has surrendered {2}</a></h3>
                    <p>{0} has surrendered your game of Civilization V on Giant Multiplayer Robot! <a href=""http://multiplayerrobot.com/Game/Details/{1}"">Click here to view details.</a></p>"
                        , playerSurrendering.UserName,
                        game.GameID,
                        game.Name));

                Task.Factory.StartNew(
                    () =>
                        WebHelpers.SendEmail(playerReceiving.Email, "A player has surrendered your Civ V game on GMR",
                            email));
            }
        }

        private static string CreateEmail(string mainContentHtml)
        {
            foreach (var tagReplace in EmailTagReplacements)
            {
                mainContentHtml = mainContentHtml.Replace(tagReplace.Key, tagReplace.Value);
            }

            return string.Concat(
                @"<html>
    <body style="" font-family: Segoe UI, Tahoma, Arial, Verdana, sans-serif; background-color: #222222; padding: 0px 0px 0px 0px; margin: 0px 0px 0px 0px; background-repeat:repeat-x; color: #C7C7C7;"">
        <div style=""float: left; position: relative; top: 45px; left: 20; width: 550px; z-index: 0; background-color: #565656; padding: 15px; font-size: 125%; "">
            ", mainContentHtml,
                @"<br />
            <p>If you would prefer not to receive these types of notifications in the future you can update your preferences on the <a style=""color:#419EF3;"" href=""http://multiplayerrobot.com"">website</a>.</p>
        </div>
    </body>
</html>"
                );
        }

        #endregion
    }

    public class FinishTurnArgs
    {
        public int TurnId { get; set; }
        public byte[] SaveFileBytes { get; set; }
        public bool SetHumanPlayers { get; set; }
    }
}