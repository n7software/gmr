using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace GmrLib.SteamAPI
{
    public delegate void SteamApiRefreshEventHandler(IEnumerable<SteamPlayer> players);

    public class SteamApiThreaderPool : IDisposable
    {
        private const int CleanupIntervalSeconds = 5;
        private readonly bool AutomaticCleanup;
        private readonly int ClientExpireTime;
        private readonly int IntervalSeconds;
        private readonly Dictionary<long, SteamPlayer> Players;

        private readonly List<SteamApiThreader> Threaders = new List<SteamApiThreader>();
        private readonly object _lockPlayers = new object();
        private readonly object _lockThreaders = new object();
        private Thread CleanUpThread;


        /// <summary>
        ///     Initializes a SteamApiThreaderPool.
        /// </summary>
        /// <param name="intervalSeconds">How often to refresh client data, in seconds</param>
        /// <param name="automaticCleanup">
        ///     Whether or not to clean up clients that haven't been requested as long as
        ///     clientExpireTime
        /// </param>
        /// <param name="clientExpireTime">
        ///     Once this many seconds have expired since a client's data was last requested,
        ///     they will be removed from the pool.
        /// </param>
        public SteamApiThreaderPool(int intervalSeconds, bool automaticCleanup = false, int clientExpireTime = 900)
        {
            Players = new Dictionary<long, SteamPlayer>();

            AutomaticCleanup = automaticCleanup;
            IntervalSeconds = intervalSeconds;
            Threaders = new List<SteamApiThreader>();
            ClientExpireTime = clientExpireTime;

            StartCleanUpThread();
        }

        public int TotalClients
        {
            get
            {
                int totalClients = 0;

                lock (_lockThreaders)
                {
                    foreach (SteamApiThreader t in Threaders)
                        totalClients += t.TotalClients;
                }

                return totalClients;
            }
        }

        public int TotalThreads
        {
            get { return Threaders.Count(); }
        }

        public void Dispose()
        {
            foreach (SteamApiThreader t in Threaders)
                t.Dispose();

            StopCleanUpThread();
        }

        /// <summary>
        ///     Fires every time the player data is refreshed.
        /// </summary>
        public event SteamApiRefreshEventHandler DataRefreshed = a => { };

        private void StartCleanUpThread()
        {
            StopCleanUpThread();

            CleanUpThread = new Thread(CleanUp);
            CleanUpThread.Name = "ThreadPool Cleanup";
            CleanUpThread.IsBackground = true;
            CleanUpThread.Start();
        }

        private void StopCleanUpThread()
        {
            if (CleanUpThread != null)
            {
                CleanUpThread.Abort();
                CleanUpThread = null;
            }
        }

        private void CleanUp()
        {
            while (true)
            {
                Consolidate();

                Thread.Sleep(CleanupIntervalSeconds * 1000);
            }
        }

        public void RequestUserPolling(List<long> ids)
        {
            lock (_lockThreaders)
            {
                foreach (long id in ids)
                {
                    ThreaderForPlayer(id).RequestUserPolling(id);
                }
            }
        }

        private SteamApiThreader ThreaderForPlayer(long steamID)
        {
            foreach (SteamApiThreader threader in Threaders)
            {
                if (threader.Contains(steamID))
                {
                    return threader;
                }
            }

            return GetNextAvailableThreader();
        }

        private SteamApiThreader GetNextAvailableThreader()
        {
            int i;
            for (i = 0; i < Threaders.Count; i++)
            {
                if (Threaders[i].TotalClients < 100)
                    break;
            }

            if (i >= Threaders.Count)
            {
                Threaders.Add(new SteamApiThreader(IntervalSeconds, AutomaticCleanup, ClientExpireTime));
                Threaders[i].DataRefreshed += SteamApiThreaderPool_DataRefreshed;
                Threaders[i].Start();
            }

            return Threaders[i];
        }

        public void RemoveUserPolling(long id)
        {
            try
            {
                var threadersToRemove = new List<SteamApiThreader>();

                lock (_lockThreaders)
                {
                    foreach (SteamApiThreader t in Threaders)
                    {
                        t.RemoveSteamUserFromPolling(id);
                        if (t.TotalClients == 0)
                        {
                            t.Dispose();
                            threadersToRemove.Add(t);
                        }
                    }

                    foreach (SteamApiThreader t in threadersToRemove)
                    {
                        Threaders.Remove(t);
                    }
                }

                lock (_lockPlayers)
                {
                    Players.Remove(id);
                }
            }
            catch (ThreadAbortException)
            {
            }
            catch (Exception exc)
            {
                DebugLogger.WriteException(exc, "Removing user from Steam polling");
            }
        }

        private void Consolidate()
        {
            lock (_lockThreaders)
            {
                try
                {
                    for (int left = 0, right = Threaders.Count - 1; left < right; left++)
                    {
                        long? id = Threaders[right].Last;
                        while (Threaders[left].TotalClients < 100 && left < right)
                        {
                            while (!id.HasValue && left < right)
                            {
                                Threaders[right].Dispose();
                                Threaders.RemoveAt(right);
                                right--;

                                id = Threaders[right].Last;
                            }

                            if (left < right)
                            {
                                Threaders[right].RemoveSteamUserFromPolling(id.Value);
                                Threaders[left].RequestUserPolling(id.Value);
                                id = Threaders[right].Last;
                            }
                        }
                    }
                }
                catch (ThreadAbortException)
                {
                }
                catch (Exception exc)
                {
                    DebugLogger.WriteException(exc, "Consolidating Steam threads");
                }
            }
        }

        public void PerformSteamPlayerCheck()
        {
            lock (_lockThreaders)
            {
                Threaders.ForEach(t => t.PerformSteamPlayerCheck());
            }
        }


        private void SteamApiThreaderPool_DataRefreshed(IEnumerable<SteamPlayer> players)
        {
            lock (_lockPlayers)
            {
                foreach (SteamPlayer sp in players)
                {
                    if (Players.ContainsKey(sp.SteamID))
                    {
                        UpdatePlayerInfo(Players[sp.SteamID], sp);
                    }
                    else
                    {
                        Players.Add(sp.SteamID, sp);
                    }
                }

                if (DataRefreshed != null)
                    DataRefreshed(Players.Values.ToList());
            }
        }

        private static void UpdatePlayerInfo(SteamPlayer playerToUpdate, SteamPlayer playerWithNewInfo)
        {
            playerToUpdate.Avatar = playerWithNewInfo.Avatar;
            playerToUpdate.AvatarFull = playerWithNewInfo.AvatarFull;
            playerToUpdate.AvatarMedium = playerWithNewInfo.AvatarMedium;
            playerToUpdate.CommunityVisibilityState = playerWithNewInfo.CommunityVisibilityState;
            playerToUpdate.GameExtraInfo = playerWithNewInfo.GameExtraInfo;
            playerToUpdate.GameID = playerWithNewInfo.GameID;
            playerToUpdate.GameServerIp = playerWithNewInfo.GameServerIp;
            playerToUpdate.GameServerSteamID = playerWithNewInfo.GameServerSteamID;
            playerToUpdate.LastLogOff = playerWithNewInfo.LastLogOff;
            playerToUpdate.LocCityID = playerWithNewInfo.LocCityID;
            playerToUpdate.LocCountryCode = playerWithNewInfo.LocCountryCode;
            playerToUpdate.LocStateCode = playerWithNewInfo.LocStateCode;
            playerToUpdate.PersonaName = playerWithNewInfo.PersonaName;
            playerToUpdate.PersonaState = playerWithNewInfo.PersonaState;
            playerToUpdate.PrimaryClanId = playerWithNewInfo.PrimaryClanId;
            playerToUpdate.ProfileState = playerWithNewInfo.ProfileState;
            playerToUpdate.RealName = playerWithNewInfo.RealName;
        }
    }
}