using System.Web.Mvc;
using System.Web.Security;

namespace GmrServer.Controllers
{
    public class SessionCheckController : Controller
    {
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (Request.IsAuthenticated && Session[Global.UserSteamIDKey] == null)
            {
                FormsAuthentication.SignOut();
                Response.Redirect("~/User/SessionExpired?returnUrl=" + Url.Encode(Request.RawUrl));
            }
        }

    }
}
