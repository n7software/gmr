using GmrLib.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace GmrLib
{
    using System.Data.Entity.Core.Objects;
    using System.Threading.Tasks;

    public class PlayerStatsManager
    {
        #region Properties

        private const int StatsExpireInSeconds = 86400; // 24 hours
        private readonly Dictionary<long, PackagedUserStats> UserStatCache = new Dictionary<long, PackagedUserStats>();
        private readonly object _lockUserStatCache = new object();

        private Thread _trimThread;
        private Task _loadUsersWithoutStatsTask = null;
        private object _lockUsersWithoutStatsTask = new object();

        private HashSet<long> _userIdsToRemoveStats = new HashSet<long>();
        private object _lockUserIdsToRemoveStats = new object();
        #endregion

        #region Constructor & Instance

        private static readonly PlayerStatsManager _instance = new PlayerStatsManager();

        private PlayerStatsManager()
        {
        }

        public static PlayerStatsManager Instance
        {
            get { return _instance; }
        }

        #endregion

        #region Methods

        public List<PackagedUserStats> GetCurrentUserStats()
        {
            LoadUsersWithoutStats();
            return GetAllUserStats();
        }

        public List<PackagedUserStats> GetAllUserStats()
        {
            var result = new List<PackagedUserStats>();

            lock (_lockUserStatCache)
            {
                result.AddRange(UserStatCache.Values);
            }

            return result;
        }

        public void LoadUsersWithoutStats()
        {
            Task task = null;

            lock (_lockUsersWithoutStatsTask)
            {
                if (_loadUsersWithoutStatsTask == null)
                {
                    task = _loadUsersWithoutStatsTask = Task.Run(new Action(LoadUsersWithoutStatsTask))
                        .ContinueWith(t =>
                        {
                            lock (_lockUsersWithoutStatsTask)
                            {
                                _loadUsersWithoutStatsTask = null;
                            }
                        });
                }
                else
                {
                    task = _loadUsersWithoutStatsTask;
                }
            }

            if (task != null)
            {
                task.Wait();
            }
        }

        private void LoadUsersWithoutStatsTask()
        {
            var missingUserIds = new List<long>();
            using (GmrEntities gmrDb = GmrEntities.CreateContext())
            {
                missingUserIds = gmrDb.Users.Select(u => u.UserId).ToList();
                missingUserIds.Remove(0);

                lock (_lockUserStatCache)
                {
                    missingUserIds = missingUserIds.Where(id => !UserStatCache.ContainsKey(id)).ToList();
                }

                foreach (long userId in missingUserIds)
                {
                    bool getStat = false;

                    lock (_lockUserStatCache)
                    {
                        getStat = !UserStatCache.ContainsKey(userId);
                    }

                    if (getStat)
                    {
                        var userStat = GetUserStats(userId, gmrDb);

                        lock (_lockUserStatCache)
                        {
                            UserStatCache[userId] = userStat;
                        }
                    }
                }
            }
        }

        public void LoadStatsFromDb()
        {
            using (GmrEntities gmrDb = GmrEntities.CreateContext())
            {
                lock (_lockUserStatCache)
                {
                    foreach (UserStat userStat in gmrDb.UserStats)
                    {
                        var stat = new PackagedUserStats
                        {
                            UserId = userStat.UserId,
                            UserName = userStat.UserName,
                            AvatarUrl = userStat.AvatarUrl,
                            AverageTurnTime = new TimeSpan(0, 0, userStat.AverageTurnTime),
                            GameCount = userStat.GameCount,
                            TurnCount = userStat.TurnCount,
                            SkipCount = userStat.SkipCount,
                            VacationCount = userStat.VacationCount,
                            LastUpdated = userStat.LastUpdated,
                            TotalPoints = userStat.TotalPoints
                        };

                        UserStatCache[stat.UserId] = stat;
                    }
                }
            }
        }

        public void RemoveUserStats(long userId)
        {
            lock (_lockUserIdsToRemoveStats)
            {
                if (!_userIdsToRemoveStats.Contains(userId))
                {
                    _userIdsToRemoveStats.Add(userId);
                }
            }
        }

        public void RemoveUserStats(IEnumerable<long> userIds)
        {
            lock (_lockUserIdsToRemoveStats)
            {
                foreach (var userId in userIds)
                {
                    if (!_userIdsToRemoveStats.Contains(userId))
                    {
                        _userIdsToRemoveStats.Add(userId);
                    }
                }
            }
        }

        public PackagedUserStats GetStatsForPlayer(long userId)
        {
            using (GmrEntities gmrDb = GmrEntities.CreateContext())
            {
                return VerifyUserHasStats(userId, gmrDb);
            }
        }

        public void StartTrimThread()
        {
            StopTrimThread();

#if RELEASE || true
            _trimThread = new Thread(RemoveExpiredStatsThread)
            {
                IsBackground = true,
                Name = "Remove Expired Stats"
            };
            _trimThread.Start();
#endif
        }

        public void StopTrimThread()
        {
            if (_trimThread != null && _trimThread.IsAlive)
            {
                _trimThread.Abort();
            }

            _trimThread = null;
        }

        private PackagedUserStats VerifyUserHasStats(long userId, GmrEntities gmrDb)
        {
            PackagedUserStats stats = null;

            lock (_lockUserStatCache)
            {
                if (UserStatCache.ContainsKey(userId))
                {
                    stats = UserStatCache[userId];
                }
            }

            if (stats == null)
            {
                stats = GetUserStats(userId, gmrDb);

                lock (_lockUserStatCache)
                {
                    UserStatCache[userId] = stats;
                }
            }

            return stats;
        }

        private PackagedUserStats GetUserStats(long userId, GmrEntities gmrDb)
        {
            var stats = new PackagedUserStats();

            var userName = new ObjectParameter("userName", typeof(string));
            var avatarUrl = new ObjectParameter("avatarUrl", typeof(string));
            var totalPoints = new ObjectParameter("totalPoints", typeof(int));
            var gameCount = new ObjectParameter("gameCount", typeof(int));
            var turnCount = new ObjectParameter("turnCount", typeof(int));
            var skipCount = new ObjectParameter("skipCount", typeof(int));
            var vacationCount = new ObjectParameter("vacationCount", typeof(int));
            var averageTurnTime = new ObjectParameter("averageTurnTime", typeof(int));
            var lastUpdated = new ObjectParameter("lastUpdated", typeof(DateTime));

            gmrDb.GetUserStats(userId,
                userName,
                avatarUrl,
                totalPoints,
                gameCount,
                turnCount,
                skipCount,
                vacationCount,
                averageTurnTime,
                lastUpdated);

            stats.UserId = userId;
            stats.UserName = userName.Value as string;
            stats.AvatarUrl = avatarUrl.Value as string;
            stats.TotalPoints = (int)totalPoints.Value;
            stats.GameCount = (int)gameCount.Value;
            stats.TurnCount = (int)turnCount.Value;
            stats.SkipCount = (int)skipCount.Value;
            stats.VacationCount = (int)vacationCount.Value;
            stats.AverageTurnTime = new TimeSpan(0, 0, (int)averageTurnTime.Value);

            stats.LastUpdated = DateTime.UtcNow;

            return stats;
        }

        private void ProcessUserStatsToRemove()
        {
            lock (_lockUserIdsToRemoveStats)
            {
                lock (_lockUserStatCache)
                {
                    foreach (long userId in _userIdsToRemoveStats)
                    {
                        UserStatCache.Remove(userId);
                    }
                }

                using (GmrEntities gmrDb = GmrEntities.CreateContext())
                {
                    gmrDb.UserStats.RemoveRange(gmrDb.UserStats.Where(u => _userIdsToRemoveStats.Contains(u.UserId)));
                }
            }
        }

        private void RemoveExpiredStatsThread()
        {
            while (true)
            {
                Thread.Sleep(60000);

                try
                {
                    var removeIds = new List<long>();

                    lock (_lockUserStatCache)
                    {
                        foreach (var userScore in UserStatCache)
                        {
                            if ((DateTime.UtcNow - userScore.Value.LastUpdated).TotalSeconds >= StatsExpireInSeconds)
                            {
                                removeIds.Add(userScore.Key);
                            }
                        }
                    }

                    if (removeIds.Count > 0)
                    {
                        lock (_lockUserStatCache)
                        {
                            foreach (long userId in removeIds)
                            {
                                UserStatCache.Remove(userId);
                            }
                        }

                        using (GmrEntities gmrDb = GmrEntities.CreateContext())
                        {
                            foreach (long userId in removeIds)
                            {
                                gmrDb.DeleteUserStats(userId);
                            }
                        }
                    }

                    ProcessUserStatsToRemove();
                    LoadUsersWithoutStats();
                }
                catch (Exception exc)
                {
                    DebugLogger.WriteException(exc, "Removing expired stats");
                }
            }
        }

        #endregion
    }
}