using GmrLib;
using GmrWorker;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Hosting;
using System.Web.Mvc;
using System.Web.Routing;

namespace GmrServer
{
    public class MvcApplication : System.Web.HttpApplication
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }

        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            routes.IgnoreRoute("{resource}.xml/{*pathInfo}");
            routes.IgnoreRoute("GmrServer/{*pathInfo}");
            routes.IgnoreRoute("Content/{*pathInfo}");

            routes.MapRoute(
                "Default", // Route name
                "{controller}/{action}/{id}", // URL with parameters
                new { controller = "Home", action = "Index", id = UrlParameter.Optional } // Parameter defaults
            );
        }

        protected void Application_Start()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            AreaRegistration.RegisterAllAreas();

            WebApiConfig.Register(GlobalConfiguration.Configuration);

            RegisterGlobalFilters(GlobalFilters.Filters);
            RegisterRoutes(RouteTable.Routes);

            GlobalConfiguration.Configuration.Services.Replace(typeof(IHostBufferPolicySelector), new NoBufferPolicySelector());

            Global.GameCacheThread = StartGameCacheThread();
            Global.ApiThreader = Global.SteamApiInstance;

            Task.Factory.StartNew(() => PlayerStatsManager.Instance.LoadStatsFromDb())
                        .ContinueWith(t => PlayerStatsManager.Instance.LoadUsersWithoutStats())
                        .ContinueWith(t => PlayerStatsManager.Instance.StartTrimThread());
#if !DEBUG
            Global.TurnTimer = TurnTimerMonitor.Instance;
            Global.TurnTimer.Start();
#endif
            //RssRefresher.Instance.Start();
        }

        protected void Application_End()
        {
            try
            {
                Global.SteamApiInstance.Dispose();
            }
            catch (Exception exc)
            {
                DebugLogger.WriteException(exc, "Disposing SteamApiInstance");
            }

            StopGameCacheThread();

            try
            {
                if (Global.TurnTimer != null)
                {
                    Global.TurnTimer.Stop();
                }
            }
            catch (Exception exc)
            {
                DebugLogger.WriteException(exc, "Stopping TurnTimer");
            }
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            // At this point we have information about the error
            HttpContext ctx = HttpContext.Current;

            Exception exception = ctx.Server.GetLastError();

            if (!exception.Message.Contains("was not found or does not implement IController.")
                && !exception.Message.Contains("The supplied connection string is not valid"))
            {
                DebugLogger.WriteException(exception, "Application Error!");
            }
        }

        protected Thread StartGameCacheThread()
        {
            Thread gameCache = new Thread(() =>
                {
                    while (true)
                    {
                        Thread.Sleep(60 * 60 * 1000);
                        GameManager.ClearAllGamesFromCache();
                    }
                });
            gameCache.Name = "Game Cache Thread";
            gameCache.IsBackground = true;
            gameCache.Start();
            return gameCache;
        }

        protected void StopGameCacheThread()
        {
            if (Global.GameCacheThread != null)
            {
                if (Global.GameCacheThread.IsAlive)
                    Global.GameCacheThread.Abort();

                Global.GameCacheThread = null;
            }

        }
    }
}