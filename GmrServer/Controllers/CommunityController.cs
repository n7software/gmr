using GmrLib;
using GmrLib.Models;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace GmrServer.Controllers
{
    public class CommunityController : SessionCheckController
    {
        private const int UsersPerPage = 15;
        private const int TurnsToDisplayOnPlayerProfile = 50;

        public ActionResult Index()
        {
            return View();
        }

        public ActionResult PlayerDetails(long id)
        {
            var gmrDb = GmrEntities.CreateContext();

            var user = gmrDb.Users.FirstOrDefault(u => u.UserId == id);
            if (user != null)
            {
                ViewBag.UserStats = PlayerStatsManager.Instance.GetStatsForPlayer(user.UserId);
            }

            return PartialView(user);
        }
        public ActionResult PlayerDetailGames(long id)
        {
            var gmrDb = GmrEntities.CreateContext();

            var games = new List<Game>();

            var user = gmrDb.Users.FirstOrDefault(u => u.UserId == id);
            if (user != null)
            {
                games.AddRange(user.GamePlayers.Select(gp => gp.Game).OrderByDescending(g => g.Created));
            }

            return PartialView(games);
        }
        public ActionResult PlayerDetailTurns(long id)
        {
            var gmrDb = GmrEntities.CreateContext();

            var turns = new List<Turn>();

            var user = gmrDb.Users.FirstOrDefault(u => u.UserId == id);
            if (user != null)
            {
                turns.AddRange(user.Turns.Where(t => t.Finished.HasValue)
                                   .OrderByDescending(t => t.Finished)
                                   .Take(TurnsToDisplayOnPlayerProfile)
                              );
            }

            return PartialView(turns);
        }

        public ActionResult Players()
        {
            var players = PlayerStatsManager.Instance.GetCurrentUserStats();

            ViewBag.TotalPages = CalculateTotalPages(players.Count);
            ViewBag.PlayersPerPage = UsersPerPage;

            return PartialView();
        }

        public ActionResult PlayersPage(int page, string columnName, bool sortAscending, string findPlayerName)
        {
            var players = PlayerStatsManager.Instance.GetCurrentUserStats();

            var query = GetSortedQuery(players, columnName, sortAscending, findPlayerName).ToList();

            var selectedStats = query.Skip(page * UsersPerPage)
                               .Take(UsersPerPage)
                               .ToList();

            int index = page * UsersPerPage;
            foreach (var userStat in selectedStats)
            {
                userStat.Index = ++index;
            }

            ViewBag.IndexLength = string.Format("{0:n0}", index).Length;

            ViewBag.TotalPages = CalculateTotalPages(query.Count);

            return PartialView("PlayerPage", selectedStats);
        }

        public ActionResult FindCurrentPlayerPageNumber(string columnName, bool sortAscending, string findPlayerName)
        {
            int pageNumber = 0;

            if (Global.UserSteamID > 0)
            {
                var players = PlayerStatsManager.Instance.GetCurrentUserStats();

                var query = GetSortedQuery(players, columnName, sortAscending, findPlayerName);

                var userIndex = query.Select((p, index) => new { p.UserId, index })
                                     .FirstOrDefault(ui => ui.UserId == Global.UserSteamID);

                if (userIndex != null)
                {
                    pageNumber = userIndex.index / UsersPerPage;
                }
            }

            return Content(pageNumber.ToString());
        }

        public ActionResult Turns()
        {
            var gmrDb = GmrEntities.CreateContext();

            var turns = gmrDb.Turns.Where(t => t.Finished.HasValue)
                             .OrderByDescending(t => t.Finished)
                             .Take(100);

            return PartialView(turns);
        }


        private IEnumerable<PackagedUserStats> GetSortedQuery(IEnumerable<PackagedUserStats> players, string columnName, bool sortAscending, string findPlayerName)
        {
            IEnumerable<PackagedUserStats> playersQuery = players.Where(p => p.UserId != 0);

            if (!string.IsNullOrWhiteSpace(findPlayerName))
            {
                findPlayerName = findPlayerName.ToLower();
                playersQuery = playersQuery.Where(p => p.UserName.ToLower().Contains(findPlayerName));
            }

            switch (columnName)
            {
                case "points":
                    goto default;

                case "games":
                    return sortAscending ? playersQuery.OrderBy(p => p.GameCount) :
                                           playersQuery.OrderByDescending(p => p.GameCount);

                case "turns":
                    return sortAscending ? playersQuery.OrderBy(p => p.TurnCount) :
                                           playersQuery.OrderByDescending(p => p.TurnCount);

                case "average-turn":
                    return sortAscending ? playersQuery.OrderBy(p => p.AverageTurnTime) :
                                           playersQuery.OrderByDescending(p => p.AverageTurnTime);

                case "total-skipped":
                    return sortAscending ? playersQuery.OrderBy(p => p.SkipCount) :
                                           playersQuery.OrderByDescending(p => p.SkipCount);

                case "total-vacation":
                    return sortAscending ? playersQuery.OrderBy(p => p.VacationCount) :
                                           playersQuery.OrderByDescending(p => p.VacationCount);

                default:
                    return sortAscending ? playersQuery.OrderBy(p => p.TotalPoints) :
                                           playersQuery.OrderByDescending(p => p.TotalPoints);
            }
        }

        private int CalculateTotalPages(int playerCount)
        {
            int totalPages = 0;

            totalPages = playerCount / UsersPerPage;

            if ((playerCount % UsersPerPage) > 0)
            {
                totalPages++;
            }

            return totalPages;
        }
    }
}
