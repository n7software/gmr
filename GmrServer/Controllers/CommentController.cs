using GmrLib;
using GmrLib.Models;
using GmrServer.Util;
using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace GmrServer.Controllers
{
    public class CommentController : Controller
    {

        public ActionResult Index()
        {
            return RedirectToAction("Index", "Game");
        }

        [HttpPost]
        [Authorize]
        [ValidateInput(false)]
        public ActionResult AddComment(int id, int type, string newComment)
        {
            string encodedComment = HttpUtility.HtmlEncode(newComment),
                   parsedComment = string.Empty;

            if (encodedComment.Length >= 2080)
            {
                return Content("Your comment was too long. Please shorten it to 2,048 characters or less and try again.");
            }

            var gmrDb = GmrEntities.CreateContext();

            if (!string.IsNullOrWhiteSpace(newComment))
            {
                try
                {
                    parsedComment = Utilities.ParseCommentCodes(encodedComment);
                }
                catch (InvalidCommentException)
                {
                    return
                        Content("The comment you entered was invalid. Please make sure your tags are matched and properly formatted.");
                }
            }

            if (!string.IsNullOrWhiteSpace(parsedComment))
            {
                var comment = new Comment()
                {
                    DateCreated = DateTime.UtcNow,
                    DateModified = null,
                    User = Global.CurrentUser(gmrDb),
                    Value = encodedComment
                };

                switch ((CommentType)type)
                {
                    case CommentType.Game:
                        AddGameComment(id, gmrDb, comment, parsedComment);
                        break;

                    case CommentType.Message:
                        AddMessageComment(id, gmrDb, comment, parsedComment);
                        break;
                }
            }

            return Content(string.Empty);
        }
        private static void AddMessageComment(int id, GmrEntities gmrDb, Comment comment, string parsedComment)
        {
            var message = gmrDb.Messages.FirstOrDefault(m => m.Id == id);
            if (message != null)
            {
                message.Comments.Add(comment);
                message.LastUpdated = DateTime.UtcNow;

                foreach (var messageRecipient in message.Recipients)
                {
                    if (messageRecipient.Recipient.UserId != Global.UserSteamID)
                    {
                        messageRecipient.HasNew = true;
                    }
                }

                GameManager.SendMessageCommentNotificationAndEmail(message, Global.CurrentUser(gmrDb), parsedComment);

                gmrDb.SaveChanges();
            }
        }
        private static void AddGameComment(int id, GmrEntities gmrDb, Comment comment, string parsedComment)
        {
            var game = gmrDb.Games.FirstOrDefault(g => g.GameID == id);
            if (game != null)
            {
                game.Comments.Add(comment);

                GameManager.SendNewGameCommentNotificationAndEmail(game, Global.CurrentUser(gmrDb), parsedComment);

                gmrDb.SaveChanges();
            }
        }

        [HttpPost]
        [Authorize]
        [ValidateInput(false)]
        public ActionResult EditComment(int id, string commentText)
        {
            var gmrDb = GmrEntities.CreateContext();

            var comment = gmrDb.Comments.FirstOrDefault(c => c.CommentID == id);
            if (comment != null)
            {
                if (!string.IsNullOrWhiteSpace(commentText))
                {
                    string encodedComment = HttpUtility.HtmlEncode(commentText);

                    if (Utilities.IsTextMarkupValid(encodedComment))
                    {
                        comment.Value = encodedComment;
                        comment.DateModified = DateTime.UtcNow;

                        gmrDb.SaveChanges();
                    }
                    else
                    {
                        return Content("The comment you entered was invalid. Please make sure your tags are matched and properly formatted.");
                    }
                }
            }

            return Content(string.Empty);
        }

        [HttpPost]
        [Authorize]
        public void DeleteComment(int id)
        {
            var gmrDb = GmrEntities.CreateContext();

            var comment = gmrDb.Comments.FirstOrDefault(c => c.CommentID == id);
            if (comment != null && comment.UserID == Global.UserSteamID)
            {
                gmrDb.Comments.Remove(comment);
                gmrDb.SaveChanges();
            }
        }

        public ActionResult CommentPage(int id, int type, int page)
        {
            var gmrDB = GmrEntities.CreateContext();

            IQueryable<Comment> comments = null;

            switch ((CommentType)type)
            {
                case CommentType.Game:
                    comments = gmrDB.Comments.Where(c => c.GameID == id);
                    break;

                case CommentType.Message:
                    comments = gmrDB.Comments.Where(c => c.Message.Id == id);
                    break;
            }

            comments = comments.OrderByDescending(c => c.DateCreated)
                               .Skip(page * CommentsPerPage)
                               .Take(CommentsPerPage);

            return PartialView("~/Views/Shared/CommentPage.cshtml", comments);
        }

        public const int CommentsPerPage = 10;
    }
}
