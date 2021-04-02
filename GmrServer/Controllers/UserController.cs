using DotNetOpenAuth.Messaging;
using DotNetOpenAuth.OpenId;
using DotNetOpenAuth.OpenId.RelyingParty;
using GmrLib;
using GmrLib.Models;
using GmrLib.SteamAPI;
using GmrServer.Models;
using GmrServer.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;


namespace GmrServer.Controllers
{
    public class UserController : SessionCheckController
    {
        const string SteamOpenID = "http://steamcommunity.com/openid";

        public const int MessagesPerPage = 20;

        //
        // GET: /User/

        public ActionResult Login(string returnUrl)
        {
            var openid = new OpenIdRelyingParty();
            var response = openid.GetResponse();
            if (response == null)
            {
                #region Submit identifier
                Identifier id = Identifier.Parse(SteamOpenID);

                try
                {
                    return openid.CreateRequest(SteamOpenID).RedirectingResponse.AsActionResultMvc5();
                }
                catch (ProtocolException ex)
                {
                    ViewBag.Message = ex.Message;
                    return View("Login");
                }
                #endregion
            }
            else
            {
                // Stage 3: OpenID Provider sending assertion response
                switch (response.Status)
                {
                    case AuthenticationStatus.Authenticated:
                        #region Get Response From API
                        string fullID = response.ClaimedIdentifier;
                        FormsAuthentication.SetAuthCookie(fullID, false);
                        long steamID = long.Parse(fullID.Substring(fullID.LastIndexOf('/') + 1));
                        #endregion

                        #region Get player info from Steam API
                        var steamApi = new SteamApiClient(steamID);
                        var steamPlayer = steamApi.GetPlayerSummaries().FirstOrDefault();
                        #endregion

                        Global.UserSteamID = steamID;

                        #region Find or create the user
                        var gmrDB = GmrEntities.CreateContext();
                        User user;
                        bool newUser = false;

                        var existing = gmrDB.Users.Where(u => u.UserId == steamID);

                        if (existing.Any())
                        {
                            user = existing.First();
                        }
                        else if (steamPlayer == null)
                        {
                            ViewBag.Message = "Sorry, we can't seem to access the Steam Web API right now.";
                            return View();
                        }
                        else
                        {
                            user = new User();
                            newUser = true;
                            user.AuthKey = MakeAuthKey();
                            gmrDB.Users.Add(user);
                            user.FirstLogin = DateTime.UtcNow;
                            user.ReceivedNotifications.Add(new Notification
                            {
                                NotificationType = (int)NotificationType.Welcome,
                                Sent = DateTime.UtcNow
                            });
                            CreateDefaultPrefs(user);
                        }

                        #endregion

                        #region Set or update user info
                        user.UserId = steamID;
                        user.LastLoggedIn = DateTime.UtcNow;
                        if (steamPlayer != null)
                        {
                            user.UserName = steamPlayer.PersonaName;
                            user.ProfileUrl = steamPlayer.ProfileUrl;
                            user.AvatarUrl = steamPlayer.Avatar;
                            user.MediumAvatarUrl = steamPlayer.AvatarMedium;
                            user.FullAvatarUrl = steamPlayer.AvatarFull;
                        }
                        #endregion


                        gmrDB.SaveChanges();

                        #region Set session
                        Global.AvatarUrl = user.MediumAvatarUrl;
                        if (steamPlayer != null)
                            Global.AddPlayerToCache(steamPlayer);
                        Global.ApiThreader.RequestUserPolling(steamID);
                        #endregion


                        return RedirectToAction("CompleteLogin", new { returnUrl = returnUrl, newUser = newUser });
                    /* Steam should never return either of these cases. They don't have a cancellation,
                     * and they handle failed login on their end with ajax.
                     * */
                    case AuthenticationStatus.Canceled:
                        ViewBag.Message = "Canceled at provider";
                        return View();
                    case AuthenticationStatus.Failed:
                        ViewBag.Message = response.Exception.Message;
                        return View();
                }
            }
            return RedirectToAction("CompleteLogin", new { returnUrl = returnUrl });
        }

        private void CreateDefaultPrefs(User user)
        {
            foreach (var kvp in PackagedPreferences.All)
            {
                user.Preferences.Add(new Preference
                {
                    Key = kvp.Key,
                    Value = kvp.Value
                });
            }
        }

        private string MakeAuthKey()
        {
            var sb = new StringBuilder();
            Random r = new Random();
            for (int i = 0; i < 12; i++)
            {
                int rand;
                #region Make a random alphanumeric
                do
                {
                    rand = r.Next('0', 'Z');
                } while (rand < '0' ||
                    (rand > '9' && rand < 'A') ||
                    rand > 'Z');
                #endregion
                sb.Append((char)rand);
            }
            return sb.ToString();
        }

        [Authorize]
        public ActionResult CompleteLogin(string returnUrl, bool? newUser)
        {
            if (!newUser ?? true)
            {
                Uri url = new Uri(Request.Url, Uri.UnescapeDataString(returnUrl));
                if (Url.IsLocalUrl(url.ToString()) || url.Host == Request.Url.Host)
                    return Redirect(url.ToString());
                else return RedirectToAction("Index", "Home");
            }
            else
            {
                var gmrDB = GmrEntities.CreateContext();
                var user = gmrDB.Users.Single(u => u.UserId == Global.UserSteamID);
                ViewBag.AuthKey = user.AuthKey;
                return View();
            }
        }

        [Authorize]
        [HttpPost]
        public ActionResult CompleteLogin(Email email, string returnUrl)
        {
            if (!String.IsNullOrWhiteSpace(email.EmailAddress))
            {
                var gmrDB = GmrEntities.CreateContext();
                var user = gmrDB.Users.Single(u => u.UserId == Global.UserSteamID);
                user.Email = email.EmailAddress;
                gmrDB.SaveChanges();
            }
            if (Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            else return RedirectToAction("Index", "Home");
        }

        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            Session.Abandon();
            return RedirectToAction("Index", "Home");
        }

        public ActionResult ControlPanel()
        {
            var gmrDB = GmrEntities.CreateContext();
            var user = gmrDB.Users.FirstOrDefault(u => u.UserId == Global.UserSteamID);
            if (user != null)
            {
                return PartialView(user);
            }
            else return Content("There was an error. Please refresh the website.");
        }

        public ActionResult UpdateEmail(string email)
        {
            var gmrDB = GmrEntities.CreateContext();
            var user = gmrDB.Users.Single(u => u.UserId == Global.UserSteamID);
            if (String.IsNullOrWhiteSpace(email))
                email = null;
            user.Email = email;
            gmrDB.SaveChanges();
            return Content(String.Empty);
        }

        public ActionResult UpdatePreference(string key, string value)
        {
            using (var gmrDB = GmrEntities.CreateContext())
            {
                var user = gmrDB.Users.FirstOrDefault(u => u.UserId == Global.UserSteamID);
                if (user != null)
                {
                    var preference = user.Preferences.FirstOrDefault(p => p.Key == key);

                    if (preference != null)
                    {
                        preference.Value = value;
                    }
                    else if (PackagedPreferences.All.ContainsKey(key))
                    {
                        user.Preferences.Add(new Preference { Key = key, Value = value });
                    }

                    if (key == "GamePassword")
                    {
                        Task.Run(() => GameManager.UpdateUserCurrentTurnsPassword(Global.UserSteamID));
                    }
                    else if (key == "VacationMode" && value == "true")
                    {
                        Task.Run(() => GameManager.SkipPlayerInVacationedGames(Global.UserSteamID));
                    }

                    gmrDB.SaveChanges();
                }
            }

            return Content(String.Empty);
        }

        public ActionResult SessionExpired(string returnUrl)
        {
            while (true)
            {
                try
                {
                    if (User.Identity.IsAuthenticated)
                    {
                        if (!String.IsNullOrWhiteSpace(returnUrl))
                            return Redirect(returnUrl);
                        return RedirectToAction("Index", "Home");
                    }
                    return View();
                }
                catch
                {
                    Thread.Sleep(100);
                }
            }
        }

        public ActionResult Messages()
        {
            return View();
        }

        [Authorize]
        public ActionResult MyMessages()
        {
            var messages = GetMyMessagesQuery().Take(MessagesPerPage).ToList();

            return PartialView(messages);
        }

        [Authorize]
        public ActionResult MyMessagesPage(int page)
        {
            var messages = GetMyMessagesQuery().Skip(page * MessagesPerPage)
                                               .Take(MessagesPerPage).ToList();

            return PartialView(messages);
        }

        private static IEnumerable<Message> GetMyMessagesQuery()
        {
            var gmrDb = GmrEntities.CreateContext();

            return Global.CurrentUser(gmrDb).MessageRecipients
                                            .OrderBy(r => r, new CompareMyMessages())
                                            .Select(r => r.Message);
        }

        public ActionResult MessageDetails(int id)
        {
            var gmrDb = GmrEntities.CreateContext();

            Message message = null;

            var currentUser = Global.CurrentUser(gmrDb);
            if (currentUser != null)
            {
                var messageRecipient = currentUser.MessageRecipients.FirstOrDefault(mr => mr.Message.Id == id);
                if (messageRecipient != null)
                {
                    message = messageRecipient.Message;

                    if (messageRecipient.HasNew)
                    {
                        messageRecipient.HasNew = false;
                        gmrDb.SaveChanges();
                    }

                    return PartialView(message);
                }
            }

            return RedirectToAction("Messages");
        }

        public ActionResult CreateNewMessage(string messageBody, string recipientList, bool appendToExisting)
        {
            try
            {
                if (Global.UserSteamID == -1)
                    return Content("Sorry, but you need to be authenticated to create a new message.");

                var recipientIds = recipientList.Split(',').Select(long.Parse).ToList();
                if (!recipientIds.Any())
                    return Content("You must select at least one recipient.");

                if (string.IsNullOrWhiteSpace(messageBody) || messageBody.Length < 2 || messageBody.Length > 2048)
                    return Content("The message text must be between 2 and 2,048 characters.");

                var encodedMessageBody = HttpUtility.HtmlEncode(messageBody);
                string parsedComment = string.Empty;

                try
                {
                    parsedComment = Utilities.ParseCommentCodes(encodedMessageBody);
                }
                catch (Exception)
                {
                    return Content("The message entered contains invalid tags. Please check the formatting tags you entered and try again.");
                }

                recipientIds.Add(Global.UserSteamID);

                using (var gmrDb = GmrEntities.CreateContext())
                {
                    var currentUser = Global.CurrentUser(gmrDb);

                    var message = appendToExisting ? (from msg in gmrDb.Messages
                                                      where msg.CreatedByUser.UserId == Global.UserSteamID
                                                            && msg.Recipients.Count() == recipientIds.Count
                                                            && msg.Recipients.All(r => recipientIds.Contains(r.Recipient.UserId))
                                                      select msg).FirstOrDefault()
                                                   : null;

                    if (message == null)
                    {
                        message = new Message()
                        {
                            Created = DateTime.UtcNow,
                            CreatedByUser = currentUser
                        };

                        foreach (var recipientId in recipientIds)
                        {
                            var user = gmrDb.Users.FirstOrDefault(u => u.UserId == recipientId);

                            if (user != null)
                            {
                                var recipient = new MessageRecipient()
                                {
                                    Recipient = user,
                                    HasNew = user != currentUser
                                };

                                message.Recipients.Add(recipient);
                            }
                        }

                        gmrDb.Messages.Add(message);
                    }

                    var newComment = new Comment()
                    {
                        DateCreated = DateTime.UtcNow,
                        User = currentUser,
                        Value = encodedMessageBody,
                        Message = message
                    };
                    message.Comments.Add(newComment);

                    foreach (var recipient in message.Recipients)
                    {
                        if (recipient.Recipient.UserId != currentUser.UserId)
                        {
                            recipient.HasNew = true;
                        }
                    }

                    message.LastUpdated = DateTime.UtcNow;

                    gmrDb.SaveChanges();

                    GameManager.SendMessageCommentNotificationAndEmail(message, currentUser, parsedComment);
                }
            }
            catch (Exception exc)
            {
                DebugLogger.WriteException(exc, "Creating new message");
                return Content("There was an error creating your message, please try again.");
            };

            return Content(string.Empty);
        }

        [Authorize]
        public ActionResult MarkMessageAsRead(int id)
        {
            using (var gmrDb = GmrEntities.CreateContext())
            {
                var messageRecipient = Global.CurrentUser(gmrDb).MessageRecipients.FirstOrDefault(r => r.Message.Id == id);
                if (messageRecipient != null)
                {
                    messageRecipient.HasNew = false;

                    gmrDb.SaveChanges();
                }
            }

            return Content(string.Empty);
        }
    }

    class CompareMyMessages : IComparer<MessageRecipient>
    {
        #region IComparer<Game> Members

        public int Compare(MessageRecipient recipient1, MessageRecipient recipient2)
        {
            if (recipient1.Id == recipient2.Id)
                return 0;

            if (recipient1.HasNew)
            {
                if (!recipient2.HasNew)
                    return -1;
            }
            else if (recipient2.HasNew)
            {
                return 1;
            }

            return recipient2.Message.LastUpdated.CompareTo(recipient1.Message.LastUpdated);
        }

        #endregion
    }
}
