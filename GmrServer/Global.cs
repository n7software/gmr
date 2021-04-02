using GmrLib;
using GmrLib.Models;
using GmrLib.SteamAPI;
using GmrWorker;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Configuration;

namespace GmrServer
{
    public static class Global
    {
        public static readonly int ClientRefreshInterval = (int.Parse(WebConfigurationManager.AppSettings["ClientRefreshIntervalSeconds"] ?? "10") * 1000);

        public const string UserSteamIDKey = "SteamID";
        public const string ApiThreaderKey = "ApiThreader";

        public const string SaveFileDownloadName = "(GMR) Play this one!.Civ5Save";

        public static long UserSteamID
        {
            get
            {
                if (HttpContext.Current.Session[UserSteamIDKey] == null)
                    HttpContext.Current.Session[UserSteamIDKey] = -1L;

                return (long)HttpContext.Current.Session[UserSteamIDKey];
            }
            set
            {
                HttpContext.Current.Session[UserSteamIDKey] = value;
            }
        }

        public const int CivVGameID = 8930;

        private static int totalTurns = -1,
                           totalUsers = -1,
                           totalGames = -1;

        private static DateTime turnCountLastUpdated = DateTime.MinValue,
                                userCountLastUpdated = DateTime.MinValue,
                                gameCountLastUpdated = DateTime.MinValue;

        private static object lockTurnCount = new object(),
                              lockUserCount = new object(),
                              lockGameCount = new object();

        private const int UpdateCountsIntervalInSeconds = 30;


        public static Dictionary<UserAccountType, int> GameLimitByAccountType =
            new Dictionary<UserAccountType, int>
            {
                {UserAccountType.Free, 2},
                {UserAccountType.Tier1, 5},
                {UserAccountType.Tier2, 10},
                {UserAccountType.Unlimited, -1}
            };

        public static Dictionary<UserAccountType, decimal> SupportAmountByAccountType =
            new Dictionary<UserAccountType, decimal>
            {
                {UserAccountType.Free, 0.0m},
                {UserAccountType.Tier1, 5.0m},
                {UserAccountType.Tier2, 10.0m},
                {UserAccountType.Unlimited, 15.0m}
            };

        public static Dictionary<int, string> PointRanks =
            new Dictionary<int, string>
                {
                    {15000, "Deity"},
                    {9000, "Immortal"},
                    {4000, "Emperor"},
                    {2000, "King"},
                    {1000, "Prince"},
                    {500, "Warlord"},
                    {100, "Chieftain"},
                    {0, "Settler"},
                    {-1, "Barbarian"}
                };

        public static int CurrentUserGameLimit()
        {
            int limit = GameLimitByAccountType[UserAccountType.Free];
            User currentUser = CurrentUser();

            if (currentUser != null)
            {
                if (GameLimitByAccountType.Keys.Contains(currentUser.AccountType))
                {
                    limit = GameLimitByAccountType[currentUser.AccountType];
                }
            }

            return limit;
        }

        public static string PremiumColor(UserAccountType accountType)
        {
            if (accountType != UserAccountType.Free)
            {
                //return "#FFCC00";
                //return "#D6A028";
            }

            return string.Empty;
        }

        public static string GetSupportAmountText(KeyValuePair<UserAccountType, decimal> typeAmount, decimal totalPaymentSoFar)
        {
            if (typeAmount.Value == 0.0m)
            {
                return "Free";
            }
            else
            {
                decimal amountRequired = Math.Max(typeAmount.Value - totalPaymentSoFar, 0.0m);
                return amountRequired.ToString("C0");
            }
        }
        public static string GetSupportAmountGameLimitText(KeyValuePair<UserAccountType, decimal> typeAmount)
        {
            int gameLimit = GameLimitByAccountType[typeAmount.Key];
            string result = string.Empty;

            if (gameLimit > 0)
            {
                result = string.Format("{0} games", gameLimit);
            }
            else
            {
                result = "Unlimited";
            }

            return result;
        }

        public static string[] AllowedCommentTags = new string[] { "i", "b", "a", "p" };

        public static System.Threading.Thread GameCacheThread { get; set; }

        public static SteamApiThreaderWeb ApiThreader
        {
            get { return (SteamApiThreaderWeb)HttpContext.Current.Application[ApiThreaderKey]; }
            set { HttpContext.Current.Application[ApiThreaderKey] = value; }
        }

        public static TurnTimerMonitor TurnTimer { get; set; }

        private static Dictionary<long, SteamPlayer> _cachedPlayers = new Dictionary<long, SteamPlayer>();
        private static object _lockCachedPlayers = new object();

        public static SteamPlayer GetCachedPlayer(long steamId)
        {
            SteamPlayer player = null;

            lock (_lockCachedPlayers)
            {
                if (_cachedPlayers.ContainsKey(steamId))
                {
                    player = _cachedPlayers[steamId];
                }
            }

            return player;
        }
        public static void AddPlayerToCache(SteamPlayer player)
        {
            lock (_lockCachedPlayers)
            {
                _cachedPlayers[player.SteamID] = player;
            }
        }
        public static List<SteamPlayer> GetCachedPlayersById(List<long> playerIds)
        {
            var players = new List<SteamPlayer>();

            lock (_lockCachedPlayers)
            {
                foreach (var playerId in playerIds)
                {
                    if (_cachedPlayers.ContainsKey(playerId))
                    {
                        players.Add(_cachedPlayers[playerId]);
                    }
                }
            }

            return players;
        }
        public static void UpdateCachedPlayers(IEnumerable<SteamPlayer> players)
        {
            lock (_lockCachedPlayers)
            {
                _cachedPlayers.Clear();

                foreach (var steamPlayer in players)
                {
                    _cachedPlayers[steamPlayer.SteamID] = steamPlayer;
                }
            }
        }

        private static int _steamApiRefreshIntervalSeconds = int.Parse(WebConfigurationManager.AppSettings["ClientRefreshIntervalSeconds"] ?? "10");
        private static int SteamApiRefreshIntervalSeconds
        {
            get { return _steamApiRefreshIntervalSeconds; }
            set { _steamApiRefreshIntervalSeconds = value; }
        }

        private static SteamApiThreaderWeb _SteamApiInstanceWeb;
        public static SteamApiThreaderWeb SteamApiInstance
        {
            get
            {
                if (_SteamApiInstanceWeb == null)
                {
                    _SteamApiInstanceWeb = new SteamApiThreaderWeb(SteamApiRefreshIntervalSeconds, HttpContext.Current.Application);
                }
                return _SteamApiInstanceWeb;
            }
        }

        public static string AvatarUrl
        {
            get { return (string)HttpContext.Current.Session["AvatarUrl"]; }
            set { HttpContext.Current.Session["AvatarUrl"] = value; }
        }

        public static int TotalPoints()
        {
            if (UserSteamID > 0)
            {
                return CurrentUser().TotalPoints;
            }

            return 0;
        }

        public static string GetRankFromPoints(int points, long userId)
        {
            foreach (var pointRank in PointRanks)
            {
                if (points >= pointRank.Key)
                {
                    return pointRank.Value;
                }
            }

            return PointRanks.Last().Value;
        }

        public static string FriendlyTimeDiff(this TimeSpan time, string postfix = " ago", bool addNewLines = false)
        {
            string separator = addNewLines ? "<br />" : " ";

            string min = time.Minutes == 1 ? "minute" : "minutes";
            string hour = time.Hours == 1 ? "hour" : "hours";

            string diff = time.Minutes + " " + min + postfix;
            if (time.Days > 0)
                diff = FriendlyTimeDiffDaysOnly(time) + separator + time.Hours + " " + hour + postfix;
            else if (time.Hours > 0)
                diff = time.Hours + " " + hour + separator + diff;
            return diff;
        }

        public static string FriendlyTimeDiffDaysOnly(this TimeSpan time)
        {
            if (time.Days == 0)
                return "less than a day";
            string day = time.Days == 1 ? "day" : "days";
            return time.Days + " " + day;
        }

        public static string GetPlayerDetailsUrl(long userId)
        {
            return string.Format("/Community#{0}", userId);
        }

        public static User CurrentUser(GmrEntities db = null)
        {
            var gmrDb = db == null ? GmrEntities.CreateContext() : db;

            try
            {
                if (Global.UserSteamID > -1)
                {
                    var user = gmrDb.Users.Single(u => u.UserId == Global.UserSteamID);
                    return user;
                }
            }
            catch (Exception exc)
            {
                DebugLogger.WriteException(exc, string.Format("Getting current user from database: {0}", Global.UserSteamID));
            }

            return null;
        }

        public static bool IsUserAuthenticated()
        {
            return Global.UserSteamID > -1;
        }

        public static string GetCurentUserGameLimitText()
        {
            int gameLimit = CurrentUserGameLimit();

            return (gameLimit > 0) ? gameLimit.ToString() : "Unlimited";
        }

        public static bool IsAdminAuthenticated(GmrEntities gmrDb = null)
        {
            User currentUser = CurrentUser(gmrDb);
            return currentUser != null && currentUser.IsAdmin;
        }

        public static int GetTotalTurns()
        {
            return GetCount(ref totalTurns, ref turnCountLastUpdated, ref lockTurnCount,
                            () =>
                            {
                                using (var gmrDb = GmrEntities.CreateContext())
                                {
                                    return gmrDb.Turns.Count();
                                }
                            },
                            "Getting total amount of turns in system");
        }

        public static int GetTotalGames()
        {
            return GetCount(ref totalGames, ref gameCountLastUpdated, ref lockGameCount,
                            () =>
                            {
                                using (var gmrDb = GmrEntities.CreateContext())
                                {
                                    return gmrDb.Games.Count();
                                }
                            },
                            "Getting total amount of games in system");
        }

        public static int GetTotalUsers()
        {
            return GetCount(ref totalUsers, ref userCountLastUpdated, ref lockUserCount,
                            () =>
                            {
                                using (var gmrDb = GmrEntities.CreateContext())
                                {
                                    return gmrDb.Users.Count();
                                }
                            },
                            "Getting total amount of games in system");
        }

        private static int GetCount(ref int totalCount, ref DateTime lastUpdated, ref object lockObject, Func<int> getCount, string errorLogMessage)
        {
            if ((DateTime.Now - lastUpdated).TotalSeconds >= UpdateCountsIntervalInSeconds)
            {
                bool countLock = Monitor.TryEnter(lockObject, 200);

                try
                {
                    if (countLock)
                    {
                        totalCount = getCount();

                        lastUpdated = DateTime.Now;
                    }
                }
                catch (Exception exc)
                {
                    DebugLogger.WriteException(exc, errorLogMessage);
                }
                finally
                {
                    if (countLock)
                    {
                        Monitor.Exit(lockObject);
                    }
                }
            }

            return totalCount;
        }
    }
}