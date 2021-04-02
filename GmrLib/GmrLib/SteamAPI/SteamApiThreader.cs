using System;
using System.Collections.Generic;
using System.Threading;

namespace GmrLib.SteamAPI
{
    internal class SteamApiThreader : IDisposable
    {
        //How old an entry must be before it gets cleaned up, in seconds
        private readonly SteamApiClient Client;
        private readonly int ClientExpireTime;

        private readonly Dictionary<long, DateTime> UserRequestTimes = new Dictionary<long, DateTime>();
        private readonly object lockUserRequestTimes = new object();
        private int CleanupIntervalSeconds = 300;

        private Thread ThreadCheck;
        private Thread ThreadCleanup;

        private int _checkIntervalSecondsSeconds = 5;

        /// <summary>
        ///     Instantiates the threader
        /// </summary>
        /// <param name="interval">How often to refresh the client data, in seconds</param>
        /// \
        /// <param name="autoCleanup">
        ///     Indicates whether or not ids that aren't continually
        ///     requested should be automatically cleaned from the threader
        /// </param>
        /// <param name="clientExpireTime">How often to clean up the threads, in seconds</param>
        public SteamApiThreader(int interval, bool autoCleanup = false, int clientExpireTime = 900,
            int httpRequestTimeout = 10000)
        {
            Client = new SteamApiClient(null, httpRequestTimeout);

            _checkIntervalSecondsSeconds = interval;

            if (autoCleanup)
            {
                ClientExpireTime = clientExpireTime;
                StartCleanupThread();
            }
        }

        /// <summary>
        ///     Update interval, in seconds
        /// </summary>
        public int IntervalSeconds
        {
            get { return _checkIntervalSecondsSeconds; }
            set { _checkIntervalSecondsSeconds = value; }
        }

        public int TotalClients
        {
            get { return Client.TotalClients; }
        }

        public long? Last
        {
            get
            {
                if (TotalClients == 0)
                    return null;

                return Client.Last;
            }
        }

        public void Dispose()
        {
            StopCheckThread();
            StopCleanupThread();
        }

        /// <summary>
        ///     Fires every time the player data is refreshed.
        /// </summary>
        public event SteamApiRefreshEventHandler DataRefreshed = a => { };

        private void StartCheckThread()
        {
            StopCheckThread();

            ThreadCheck = new Thread(CheckThread);
            ThreadCheck.Name = "Steam API Check";
            ThreadCheck.IsBackground = true;
            ThreadCheck.Start();
        }

        private void StopCheckThread()
        {
            if (ThreadCheck != null)
            {
                if (ThreadCheck.IsAlive)
                    ThreadCheck.Abort();

                ThreadCheck = null;
            }
        }

        private void StartCleanupThread()
        {
            StopCleanupThread();

            ThreadCleanup = new Thread(CleanupThread);
            ThreadCleanup.Name = "Steam API Cleanup";
            ThreadCleanup.IsBackground = true;
            ThreadCleanup.Start();
        }

        private void StopCleanupThread()
        {
            if (ThreadCleanup != null)
            {
                if (ThreadCleanup.IsAlive)
                    ThreadCleanup.Abort();

                ThreadCleanup = null;
            }
        }


        private void CleanupThread()
        {
            while (true)
            {
                CleanUpOld();
                Thread.Sleep(CleanupIntervalSeconds * 1000);
            }
        }

        private void CleanUpOld()
        {
            lock (lockUserRequestTimes)
            {
                try
                {
                    var itemsToRemove = new List<long>();
                    foreach (var kvp in UserRequestTimes)
                    {
                        if (kvp.Value.AddSeconds(ClientExpireTime) < DateTime.Now)
                            itemsToRemove.Add(kvp.Key);
                    }
                    foreach (long l in itemsToRemove)
                    {
                        UserRequestTimes.Remove(l);
                        Client.RemoveUser(l);
                    }
                }
                catch
                {
                }
            }
        }

        /// <summary>
        ///     Starts the threader.
        /// </summary>
        public void Start()
        {
            StartCheckThread();
        }

        /// <summary>
        ///     Stops the threader. If a task is currently running, it will finish.
        /// </summary>
        public void Stop()
        {
            StopCheckThread();
        }

        /// <summary>
        ///     Adds a SteamID to the list of users to query.
        ///     If the SteamID is already on the list, the last requested time is updated
        ///     in regards to automatic cleanup.
        /// </summary>
        /// <param name="steamID">The SteamID for the user to add</param>
        public void RequestUserPolling(long steamID)
        {
            lock (lockUserRequestTimes)
            {
                if (UserRequestTimes.ContainsKey(steamID))
                    UserRequestTimes[steamID] = DateTime.Now;
                else UserRequestTimes.Add(steamID, DateTime.Now);
            }

            if (!Client.Contains(steamID))
                Client.AddUser(steamID);
        }

        public bool Contains(long steamID)
        {
            return Client.Contains(steamID);
        }

        /// <summary>
        ///     Removes a SteamID from the list of users to query
        ///     Users will be automatically removed after a timeout period determined by
        ///     the app setting SteamApiCleanupInterval (default: 30 minutes)
        /// </summary>
        /// <param name="steamID">The SteamID for the user to remove</param>
        public void RemoveSteamUserFromPolling(long steamID)
        {
            lock (lockUserRequestTimes)
            {
                UserRequestTimes.Remove(steamID);
            }

            Client.RemoveUser(steamID);
        }

        private void CheckThread()
        {
            while (true)
            {
                PerformSteamPlayerCheck();

                Thread.Sleep(_checkIntervalSecondsSeconds * 1000);
            }
        }

        public void PerformSteamPlayerCheck()
        {
            try
            {
                ISet<SteamPlayer> cachedPlayers = new HashSet<SteamPlayer>(Client.GetPlayerSummaries());

                if (DataRefreshed != null)
                    DataRefreshed(cachedPlayers);
            }
            catch
            {
            }
        }
    }
}