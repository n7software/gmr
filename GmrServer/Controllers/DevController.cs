using GmrLib;
using GmrLib.Models;
using GmrLib.SteamAPI;
using System;
using System.Linq;
using System.Web.Mvc;

namespace GmrServer.Controllers
{
    public class DevController : Controller
    {
        //
        // GET: /Dev/

        [Authorize]
        public ActionResult Index()
        {
            if (!Global.IsAdminAuthenticated())
                return RedirectToAction("Index", "Home");
            else return View();
        }

        [Authorize]
        public ActionResult SurrenderPlayer(int gameId, long playerId)
        {
            if (Global.IsAdminAuthenticated())
            {
                var gmrDb = GmrEntities.CreateContext();

                var game = gmrDb.Games.FirstOrDefault(g => g.GameID == gameId);
                if (game != null && game.HasStarted)
                {
                    var player = game.Players.FirstOrDefault(p => p.UserID == playerId);
                    if (player != null)
                    {
                        GameManager.SurrenderPlayer(gmrDb, game, player);

                        gmrDb.SaveChanges();

                        PlayerStatsManager.Instance.RemoveUserStats(playerId);
                        GameManager.RemoveGameFromCache(game.GameID);
                    }
                }
            }

            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        public ActionResult TimeZone(SteamIdWrapper model)
        {
            if (Global.IsAdminAuthenticated())
            {
                if (model.SteamId != 0)
                {
                    var steamApi = new SteamApiClient(model.SteamId);
                    var steamPlayer = steamApi.GetPlayerSummaries().First();

                    var timezone = 0;

                    ViewBag.Result = "UTC" + (timezone >= 0 ? "+" : String.Empty) + timezone;
                }
                return View();
            }
            else return Content(String.Empty);
        }

        [Authorize]
        public ActionResult ClearGameCache()
        {
            if (!Global.IsAdminAuthenticated())
                return RedirectToAction("Index", "Home");

            GameManager.ClearAllGamesFromCache();
            GameManager.ClearStartedGameIds();

            return RedirectToAction("Index");
        }

        public ActionResult RemoveGamesFromCache(string gameIds)
        {
            var parsedIds = gameIds.Split(',').Select(s => int.Parse(s));
            GameManager.RemoveGamesFromCache(parsedIds);
            return Content("Removed " + parsedIds.Count() + " games from cache");
        }

        public class SteamIdWrapper
        { public long SteamId { get; set; } }
    }

}
