using System.Web.Mvc;

namespace GmrServer.Controllers
{
    public class SupportUsController : SessionCheckController
    {
        //
        // GET: /SupportUs/


        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Thanks()
        {
            return View();
        }
    }
}
