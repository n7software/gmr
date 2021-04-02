using System.Web.Mvc;

namespace GmrServer.Controllers
{
    public class AboutController : SessionCheckController
    {
        //
        // GET: /About/

        public ActionResult Index()
        {
            return RedirectToAction("Us");
        }

        public ActionResult PrivacyPolicy()
        {
            return View();
        }

        public ActionResult TermsOfUse()
        {
            return View();
        }

        public ActionResult SteamDataPolicy()
        {
            return View();
        }

        public ActionResult Us()
        {
            return View();
        }

        public ActionResult InGamePasswords()
        {
            return View();
        }

        public ActionResult TurnTimer()
        {
            return View();
        }

        public ActionResult Mods()
        {
            return View();
        }

        public ActionResult FAQ()
        {
            return View();
        }

        public ActionResult API()
        {
            return View();
        }
    }
}
