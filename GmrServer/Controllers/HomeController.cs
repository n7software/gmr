using GmrServer.Models;
using System.Web.Mvc;

namespace GmrServer.Controllers
{
    public class HomeController : SessionCheckController
    {
        //
        // GET: /Home/

        public ActionResult Index()
        {
            Blog blog = null;
            lock (RssRefresher.Instance)
            {
                if (RssRefresher.Instance.CachedBlog != null)
                    blog = new Blog(RssRefresher.Instance.CachedBlog);
            }
            return View(blog);
        }

        public ActionResult Welcome()
        {
            return View();
        }

    }
}
