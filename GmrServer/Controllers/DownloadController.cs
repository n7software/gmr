using GmrLib.Models;
using System.Linq;
using System.Web.Mvc;

namespace GmrServer.Controllers
{
    public class DownloadController : SessionCheckController
    {
        //
        // GET: /Download/


        public ActionResult Index()
        {
            if (User.Identity.IsAuthenticated)
            {
                var gmrDB = GmrEntities.CreateContext();
                var user = gmrDB.Users.FirstOrDefault(u => u.UserId == Global.UserSteamID);
                if (user != null)
                {
                    return View(user);
                }
            }

            return View();
        }

    }
}
