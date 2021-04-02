using GmrLib.SteamAPI;
using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Configuration;

namespace GmrServer
{
    public class SteamApiThreaderWeb : IDisposable
    {
        private HttpApplicationState Application;
        private SteamApiThreaderPool ThreadPool;
        private int ClientExpireTime = int.Parse(WebConfigurationManager.AppSettings["SteamApiCleanupInterval"] ?? "900");

        public SteamApiThreaderWeb(int intervalSeconds, HttpApplicationState application)
        {
            Application = application;
            ThreadPool = new SteamApiThreaderPool(intervalSeconds, true, ClientExpireTime);
            ThreadPool.DataRefreshed += new SteamApiRefreshEventHandler(SteamApiRefreshed);
        }

        public void SteamApiRefreshed(IEnumerable<SteamPlayer> players)
        {
            Global.UpdateCachedPlayers(players);
        }

        public List<SteamPlayer> GetAllCachedPlayers(List<long> playerIds)
        {
            return Global.GetCachedPlayersById(playerIds);
        }

        public void RequestUserPolling(long id)
        {
            RequestUserPolling(new List<long> { id });
        }
        public void RequestUserPolling(List<long> ids)
        {
            ThreadPool.RequestUserPolling(ids);
        }

        public void RemoveUserPolling(long id)
        {
            ThreadPool.RemoveUserPolling(id);
        }

        public void Dispose()
        {
            ThreadPool.Dispose();
        }

        public int TotalClients
        {
            get { return ThreadPool.TotalClients; }
        }

        public int TotalThreads
        {
            get { return ThreadPool.TotalThreads; }
        }
    }
}