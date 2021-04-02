using GmrLib;
using GmrLib.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;

namespace GmrServer.Controllers
{
    public class QueryController : Controller
    {
        public JsonResult AvatarAndNotifications(string extraUsers)
        {
            if (Request.IsAuthenticated && Session[Global.UserSteamIDKey] == null)
            {
                return Json(new
                {
                    sessionExpired = true
                }, JsonRequestBehavior.AllowGet);
            }

            IEnumerable<long> extraIDs = new long[0];
            if (!string.IsNullOrWhiteSpace(extraUsers))
            {
                extraIDs = extraUsers.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(long.Parse);
            }

            using (var gmrDB = GmrEntities.CreateContext())
            {
                bool isAuthenticated = (Request.IsAuthenticated && Global.UserSteamID > 0);

                var userNotifications = isAuthenticated ? NotificationsController.NotificationQuery(gmrDB).Count(n => n.Checked == null)
                                                        : 0;

                var userMessages = isAuthenticated ? gmrDB.MessageRecipients.Count(m => m.Recipient.UserId == Global.UserSteamID && m.HasNew)
                                                   : 0;

                Avatar userAvatar = isAuthenticated ? Avatar.GetBorderImages(Global.UserSteamID) : null;

                var userBorders = extraIDs.Select(
                        id => new
                        {
                            id = id.ToString(),
                            avatar = Avatar.GetBorderImages(id)
                        }
                    );

                var result = Json(new
                {
                    sessionExpired = false,
                    id = Global.UserSteamID.ToString(),
                    notifications = userNotifications,
                    messages = userMessages,
                    avatarBorder = isAuthenticated ? Url.Content(userAvatar.MediumBorder) : string.Empty,
                    smallAvatarBorder = isAuthenticated ? Url.Content(userAvatar.SmallBorder) : string.Empty,
                    otherUsers = userBorders.Select(
                            user => new
                            {
                                id = user.id,
                                avatarBorder = Url.Content(user.avatar.MediumBorder),
                                smallAvatarBorder = Url.Content(user.avatar.SmallBorder),
                                largeAvatarBorder = Url.Content(user.avatar.LargeBorder)
                            })
                }, JsonRequestBehavior.AllowGet);

                return result;
            }
        }

        public JsonResult FindUsers(string search, int? gameContext)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();

                var gmrDb = GmrEntities.CreateContext();
                var matches = gmrDb.Users.Where(u => u.UserId != 0 && u.UserName.ToLower().Contains(search));

                if (gameContext.HasValue)
                {
                    matches = matches.Where(
                        u =>
                            u.GamePlayers.Count(gp => gp.GameID == gameContext.Value) == 0
                        && u.ReceivedNotifications.Count(n => n.GameID == gameContext.Value && n.NotificationType == (int)NotificationType.GameInvite) == 0
                    );
                }

                var results = matches.Select(
                        m => new
                        {
                            userID = m.UserId,
                            userName = m.UserName,
                            avatar = m.AvatarUrl
                        }).ToList();

                return Json(new
                {
                    results = results.Select(
                        r => new
                        {
                            userId = r.userID.ToString(),
                            userName = r.userName,
                            avatar = r.avatar,
                            border = Url.Content(Avatar.GetBorderImages(r.userID).SmallBorder)
                        })
                }, JsonRequestBehavior.AllowGet);
            }

            return Json(new { }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public HttpStatusCodeResult PayPalIPN()
        {
            LogIPNRequest(Request);

            Task.Run(() => VerifyIPNTask(Request));

            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }

        private void LogIPNRequest(HttpRequestBase request)
        {
            var output = new StringBuilder();

            foreach (var key in request.Form.AllKeys)
            {
                output.Append($"{key} => {request.Form[key]} | ");
            }

            DebugLogger.WriteLine("Received IPN Request", output.ToString());
        }

        private void VerifyIPNTask(HttpRequestBase ipnRequest)
        {
            var verificationResponse = string.Empty;

            try
            {
                var verificationRequest = (HttpWebRequest)WebRequest.Create(WebConfigurationManager.AppSettings["PayPalIPNCallback"]);

                // Set values for the verification request
                verificationRequest.Method = "POST";
                verificationRequest.ContentType = "application/x-www-form-urlencoded";
                var param = Request.BinaryRead(ipnRequest.ContentLength);
                var strRequest = Encoding.ASCII.GetString(param);

                // Add cmd=_notify-validate to the payload
                strRequest = "cmd=_notify-validate&" + strRequest;
                verificationRequest.ContentLength = strRequest.Length;

                // Attach payload to the verification request
                var streamOut = new StreamWriter(verificationRequest.GetRequestStream(), Encoding.ASCII);
                streamOut.Write(strRequest);
                streamOut.Close();

                // Send the request to PayPal and get the response
                var streamIn = new StreamReader(verificationRequest.GetResponse().GetResponseStream());
                verificationResponse = streamIn.ReadToEnd();
                streamIn.Close();
            }
            catch (Exception exception)
            {
                DebugLogger.WriteException(exception, "IPN Error");
                _ = GmrLib.WebHelpers.SendEmail("support@multiplayerrobot.com", "PayPal IPN Error", exception.ToString());
            }

            ProcessVerificationResponse(verificationResponse, ipnRequest);
        }

        private void ProcessVerificationResponse(string verificationResponse, HttpRequestBase request)
        {
            try
            {
                if (verificationResponse == "VERIFIED")
                {
                    var gmrDb = GmrEntities.CreateContext();
                    var apiResponse = ParsePayPalVariables(request.Form);

                    long userId = -1;
                    long? payingUserId = null;
                    string custom = apiResponse.custom.ToString();
                    string[] userIdTokens = custom.Split('-');

                    userId = long.Parse(userIdTokens[0]);

                    if (userIdTokens.Length > 1)
                    {
                        payingUserId = long.Parse(userIdTokens[1]);
                    }

                    var user = gmrDb.Users.Single(u => u.UserId == userId);
                    if (apiResponse.payment_status == "Completed"
                        || apiResponse.payment_status == "Reversed"
                        || apiResponse.payment_status == "Canceled_Reversal"
                        || apiResponse.payment_status == "Refunded")
                    {
                        bool isGift = payingUserId.HasValue;
                        var payingUser = gmrDb.Users.FirstOrDefault(u => u.UserId == payingUserId);

                        user.Payments.Add(new Payment
                        {
                            User = user,
                            TransactionId = apiResponse.txn_id.ToString(),
                            Amount = apiResponse.mc_gross,
                            Date = apiResponse.payment_date,
                            PayingUserId = payingUserId
                        });

                        if (apiResponse.mc_gross > decimal.Zero)
                        {
                            user.ReceivedNotifications.Add(new Notification
                            {
                                NotificationType = isGift ? (int)NotificationType.GiftedSupport : (int)NotificationType.ThanksForSupport,
                                Sent = DateTime.UtcNow,
                                SendingUser = payingUser
                            });
                        }

                        var previousAccountLevel = user.AccountTypeInt;

                        decimal totalPayment = user.Payments.Sum(p => p.Amount);

                        foreach (var supportAmount in Global.SupportAmountByAccountType.Reverse())
                        {
                            if (totalPayment >= supportAmount.Value)
                            {
                                user.AccountType = supportAmount.Key;
                                break;
                            }
                        }

                        bool limitIncreased = user.AccountTypeInt > previousAccountLevel;

                        if (limitIncreased)
                        {
                            user.ReceivedNotifications.Add(new Notification
                            {
                                NotificationType = (int)NotificationType.AccountPromotion,
                                Sent = DateTime.UtcNow
                            });
                        }

                        if (payingUser != null)
                        {
                            payingUser.ReceivedNotifications.Add(new Notification
                            {
                                NotificationType = (int)NotificationType.ThanksForSupport,
                                Sent = DateTime.UtcNow
                            });

                            GameManager.SendSupportThankYou(payingUser, user, limitIncreased);
                        }
                        else
                        {
                            GameManager.SendSupportThankYou(user, null, limitIncreased);
                        }

                        gmrDb.SaveChanges();
                    }
                }
                else
                {
                    string requestStr = request.Form.ToString();
                    DebugLogger.WriteLine("Invalid IPN Request", requestStr);
                    _ = GmrLib.WebHelpers.SendEmail("support@multiplayerrobot.com", "PayPal IPN Invalid Request", requestStr);
                }
            }
            catch (Exception e)
            {
                DebugLogger.WriteException(e, "IPN Error");
                _ = GmrLib.WebHelpers.SendEmail("support@multiplayerrobot.com", "PayPal IPN Error", e.ToString());
            }
        }

        /// <summary>
        /// Parses a raw PayPal IPN request
        /// </summary>
        /// <param name="raw">The string containing the raw data</param>
        /// <returns>A dynamic object whose members are documented here: https://cms.paypal.com/us/cgi-bin/?cmd=_render-content&content_ID=developer/e_howto_admin_IPNIntro#id091F0M006Y4 
        /// Values can be bool, long, decimal, DateTime, or string</returns>
        private dynamic ParsePayPalVariables(NameValueCollection raw)
        {
            var parsed = new ExpandoObject();
            foreach (var key in raw.AllKeys)
            {
                string value = raw[key];
                dynamic val = value;
                bool bParse;
                long lParse;
                decimal dParse;
                DateTime dtParse;
                if (bool.TryParse(val, out bParse))
                    val = bParse;
                else if (long.TryParse(val, out lParse))
                    val = lParse;
                else if (decimal.TryParse(val, out dParse))
                    val = dParse;
                else if (DateTime.TryParse(val.Replace('+', ' ').Replace("PST", "").Replace("PDT", ""), out dtParse))
                    val = value.Contains("PST") ? dtParse.AddHours(8) : dtParse.AddHours(7); //This looks ridiculous because it is. We get +'s instead of spaces, and the time in PST or PDT
                ((IDictionary<string, object>)parsed).Add(key, val);
            }
            return parsed;
        }
    }
}
