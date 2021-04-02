using GmrLib.Models;
using GmrServer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace GmrServer.Controllers
{
    public class NotificationsController : SessionCheckController
    {
        public ActionResult Index()
        {
            var gmrDB = GmrEntities.CreateContext();
            var notifications = NotificationQuery(gmrDB);
            var packaged = new List<PackagedNotification>();
            foreach (var n in notifications)
            {
                packaged.Add(new PackagedNotification(n, Url));
                n.Checked = DateTime.UtcNow;
            }

            gmrDB.SaveChanges();

            return View(packaged);
        }

        public ActionResult Panel()
        {
            var gmrDB = GmrEntities.CreateContext();
            var notifications = NotificationQuery(gmrDB).Take(6);
            var packaged = new List<PackagedNotification>();

            foreach (var n in notifications)
            {
                packaged.Add(new PackagedNotification(n, Url));
            }

            ViewBag.Width = 280;

            return PartialView(packaged);
        }

        [HttpPost]
        public ActionResult PanelClosed()
        {
            var gmrDB = GmrEntities.CreateContext();
            var notifications = NotificationQuery(gmrDB).Take(6);

            foreach (var n in notifications)
                n.Checked = DateTime.UtcNow;

            gmrDB.SaveChanges();

            return Content(String.Empty);
        }

        public static IOrderedQueryable<Notification> NotificationQuery(GmrEntities gmrDB)
        {
            var notifications =
                (from n in gmrDB.Notifications
                 where n.ReceivingUserID == Global.UserSteamID
                 orderby n.Checked.HasValue, n.Sent descending
                 select n);

            Task.Factory.StartNew(RemoveOldNotifications, Global.UserSteamID);

            return notifications;
        }

        private static void RemoveOldNotifications(object oUserId)
        {
            long userId = (long)oUserId;

            using (var gmrDB = GmrEntities.CreateContext())
            {
                var OneDayAgo = DateTime.UtcNow.AddDays(-1);
                var ThirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

                foreach (var deadNotification in gmrDB.Notifications.Where(n => n.ReceivingUser.UserId == userId
                                                                             && (n.Checked != null && n.Checked < OneDayAgo
                                                                                 || n.Sent < ThirtyDaysAgo)).ToList())
                {
                    gmrDB.Notifications.Remove(deadNotification);
                }

                gmrDB.SaveChanges();
            }
        }
    }
}
