using GmrLib;
using GmrLib.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;

namespace GmrWorker
{
    public class TurnTimerMonitor
    {
        private static TurnTimerMonitor _Instance;

        /// <summary>
        ///     How often the monitor will check turns, in seconds
        /// </summary>
        private readonly int IntervalSeconds =
            int.Parse(ConfigurationManager.AppSettings["TurnTimerIntervalSeconds"] ?? "300");

        private readonly bool TimerEnabled =
            bool.Parse(ConfigurationManager.AppSettings["TurnTimerEnabled"] ?? "true");

        private Thread MonitorThread;

        private TurnTimerMonitor()
        {
        }

        public static TurnTimerMonitor Instance
        {
            get
            {
                if (_Instance == null)
                    _Instance = new TurnTimerMonitor();
                return _Instance;
            }
        }

        public void Start()
        {
            Stop();

            if (TimerEnabled)
            {
                DebugLogger.WriteLine("TurnTimerMonitor.cs", "TurnTimerMonitor is being started");

                MonitorThread = new Thread(CheckTurnsAsync);
                MonitorThread.Name = "Turn Timer Monitor";
                MonitorThread.IsBackground = true;

                MonitorThread.Start();
            }
        }

        public void Stop()
        {
            if (MonitorThread != null)
            {
                if (MonitorThread.IsAlive)
                    MonitorThread.Abort();

                MonitorThread = null;
            }
        }

        private void CheckTurnsAsync()
        {
            while (true)
            {
                CheckTurns();
                Thread.Sleep(IntervalSeconds * 1000);
            }
        }

        public void CheckTurns()
        {
            try
            {
                var skippedGameIds = new List<int>();
                var skippedPlayerIds = new List<long>();

                var expiredTurnIds = new List<int>();

                using (GmrEntities gmrDB = GmrEntities.CreateContext())
                {
                    expiredTurnIds = (from turn in gmrDB.Turns
                                      where turn.ExpiresOn <= DateTime.UtcNow
                                            && turn.Finished == null
                                            && turn.Number > 1
                                            && turn.Game.TurnTimeLimit != null
                                            && turn.Number == turn.Game.Turns.Max(t => t.Number)
                                      select turn.TurnID).ToList();
                }



                foreach (int expiredTurnId in expiredTurnIds)
                {
                    using (var gmrDB = GmrEntities.CreateContext())
                    {
                        try
                        {
                            var expiredTurn = gmrDB.Turns.Include("User").First(t => t.TurnID == expiredTurnId);
                            var game = gmrDB.Games.First(g => g.GameID == expiredTurn.GameID);

                            if (GameManager.SkipCurrentPlayerOrCancelTurnTimer(game, gmrDB))
                            {
                                skippedPlayerIds.Add(expiredTurn.UserID);
                                skippedGameIds.Add(game.GameID);

                                if (expiredTurn.UserID > 0)
                                {
                                    var expiredUser = gmrDB.Users.FirstOrDefault(u => u.UserId == expiredTurn.UserID);
                                    if (expiredUser != null)
                                    {
                                        GameManager.SendSkippedEmail(game, expiredUser);
                                        GameManager.SendSkippedNotification(game, expiredUser);
                                    }
                                }

                                gmrDB.SaveChanges();
                            }
                        }
                        catch (ThreadAbortException)
                        {
                            return;
                        }
                        catch (Exception exc)
                        {
                            DebugLogger.WriteException(exc, string.Format("Skipping turn {0}", expiredTurnId));
                        }
                    }

                    GC.Collect(GC.MaxGeneration);
                    Thread.Sleep(100);
                }



                var vacationedTurnIds = new List<int>();

                using (GmrEntities gmrDB = GmrEntities.CreateContext())
                {
                    vacationedTurnIds =
                        (from turn in gmrDB.Turns
                         join pref in gmrDB.Preferences on turn.UserID equals pref.UserID
                         where pref.Key == "VacationMode"
                               && pref.Value == "true"
                               && turn.Finished == null
                               && turn.Game.Started != null
                               && turn.Game.Players.Count(p => p.UserID > 0) > 2
                               && turn.GamePlayer.AllowVacation
                               && turn.Number == turn.Game.Turns.Max(t => t.Number)
                         select turn.TurnID).ToList();
                }

                foreach (int vacationTurnId in vacationedTurnIds)
                {
                    using (GmrEntities gmrDB = GmrEntities.CreateContext())
                    {
                        try
                        {
                            var vacationTurn = gmrDB.Turns.First(t => t.TurnID == vacationTurnId);
                            var game = gmrDB.Games.First(g => g.GameID == vacationTurn.GameID);

                            IEnumerable<long> userIds = game.Players.Select(p => p.UserID);

                            int vacationModeCount =
                                (from pref in gmrDB.Preferences
                                 where pref.Key == "VacationMode"
                                       && pref.Value == "true"
                                       && userIds.Contains(pref.UserID)
                                 select pref).Count();

                            if (vacationModeCount < game.NumberOfHumanPlayers - 1)
                            {
                                if (GameManager.SkipCurrentPlayerOrCancelTurnTimer(game, gmrDB))
                                {
                                    skippedPlayerIds.Add(vacationTurn.UserID);
                                    skippedGameIds.Add(game.GameID);

                                    gmrDB.SaveChanges();
                                }
                            }
                        }
                        catch (ThreadAbortException)
                        {
                            return;
                        }
                        catch (Exception exc)
                        {
                            DebugLogger.WriteException(exc, string.Format("Vacation skip for Turn {0}", vacationTurnId));
                        }
                    }

                    Thread.Sleep(100);
                    GC.Collect(GC.MaxGeneration);
                }

                PlayerStatsManager.Instance.RemoveUserStats(skippedPlayerIds);

                GameManager.RemoveGamesFromCache(skippedGameIds);
            }
            catch (ThreadAbortException)
            {
            }
            catch (Exception e)
            {
                DebugLogger.WriteException(e, "Skip thread outter");
            }
        }
    }
}