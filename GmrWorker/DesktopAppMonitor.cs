using GmrLib;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GmrWorker
{
    public class DesktopAppMonitor
    {
        #region Properties

        //private SharedMemory<ActiveAuthTokensCache> _activeTokensMemory;

        private const int CleanupAgeSeconds = 30;
        private const int CleanupThreadSleepSeconds = 15;

        private readonly Task _cleanupThread;
        private readonly object _lockActiveAuthTokens = new object();
        public Dictionary<string, DateTime> ActiveAuthTokens = new Dictionary<string, DateTime>();

        #endregion

        #region Constructor

        private DesktopAppMonitor()
        {
            if (_cleanupThread == null)
            {
                _cleanupThread = Task.Factory.StartNew(CleanUpThread);
            }
        }

        #endregion

        #region Methods

        private void CleanUpThread()
        {
            while (true)
            {
                try
                {
                    lock (_lockActiveAuthTokens)
                    {
                        var doomed = new List<string>();

                        foreach (var token in ActiveAuthTokens)
                        {
                            if ((DateTime.UtcNow - token.Value).TotalSeconds >= CleanupAgeSeconds)
                            {
                                doomed.Add(token.Key);
                            }
                        }

                        if (doomed.Count > 0)
                        {
                            doomed.ForEach(token => ActiveAuthTokens.Remove(token));
                        }
                    }

                    Thread.Sleep(CleanupThreadSleepSeconds * 1000);
                }
                catch (ThreadAbortException)
                {
                    return;
                }
                catch (Exception exc)
                {
                    DebugLogger.WriteException(exc, "Desktop Monitor Cleanup");
                }
            }
        }


        public int GetTotalActiveAuthTokens()
        {
            int result = -1;

            lock (_lockActiveAuthTokens)
            {
                result = ActiveAuthTokens.Count;
            }

            return result;
        }

        public void OnMessageFromAuthTokenReceived(string authToken)
        {
            Task.Factory.StartNew(() =>
            {
                lock (_lockActiveAuthTokens)
                {
                    try
                    {
                        ActiveAuthTokens[authToken.ToUpper()] = DateTime.UtcNow;
                    }
                    catch (Exception exc)
                    {
                        DebugLogger.WriteException(exc, "Desktop App Monitor: Updating token time");
                    }
                }
            });
        }

        #endregion

        #region Singleton

        private static DesktopAppMonitor _instance;

        public static DesktopAppMonitor Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new DesktopAppMonitor();
                }

                return _instance;
            }
        }

        #endregion
    }
}