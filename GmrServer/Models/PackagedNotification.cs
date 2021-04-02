using GmrLib.Models;
using System;
using System.Web.Mvc;

namespace GmrServer.Models
{

    public class PackagedNotification
    {

        public PackagedNotification(Notification messyNofitication, UrlHelper urlContext)
        {
            switch ((NotificationType)messyNofitication.NotificationType)
            {
                case NotificationType.Custom:
                    Message = messyNofitication.CustomMessage;
                    break;
                case NotificationType.Welcome:
                    Message = "Welcome to Giant Multiplayer Robot!";
                    break;
                case NotificationType.GameInvite:
                    Message = messyNofitication.SendingUser.UserName + " has invited you to the game "
                        + messyNofitication.Game.Name + "!";
                    Url = urlContext.Action("Join", "Game", new { id = messyNofitication.GameID });
                    break;
                case NotificationType.YourTurn:
                    Message = (messyNofitication.SendingUser != null ? messyNofitication.SendingUser.UserName +
                        "has reminded you that it" : "It") +
                        " is now your turn in " + messyNofitication.Game.Name;
                    Url = urlContext.Action("Details", "Game", new { id = messyNofitication.GameID });
                    break;
                case NotificationType.GameOverVote:
                    Message = messyNofitication.SendingUser.UserName + " has declared that " + messyNofitication.Game.Name +
                        " has ended with " + GetUserName(messyNofitication.Game.Winner, messyNofitication, false) + " as the winner. " +
                        "Is this correct?";
                    Url = urlContext.Action("Details", "Game", new { id = messyNofitication.GameID });
                    break;
                case NotificationType.GameOver:
                    Message = messyNofitication.Game.Name + " has officially ended. " +
                        GetUserName(messyNofitication.Game.Winner, messyNofitication, true) + " won!";
                    Url = urlContext.Action("Details", "Game", new { id = messyNofitication.GameID });
                    break;
                case NotificationType.PlayerJoinedGame:
                    Message = messyNofitication.SendingUser.UserName + " has joined your game " +
                        messyNofitication.Game.Name;
                    Url = urlContext.Action("Details", "Game", new { id = messyNofitication.GameID });
                    break;
                case NotificationType.PlayerLeftGame:
                    Message = messyNofitication.SendingUser.UserName + " has left your game " + messyNofitication.Game.Name;
                    Url = urlContext.Action("Details", "Game", new { id = messyNofitication.GameID });
                    break;
                case NotificationType.GameComment:
                    Message = messyNofitication.SendingUser.UserName + " has commented on your game " + messyNofitication.Game.Name;
                    Url = urlContext.Action("Details", "Game", new { id = messyNofitication.GameID });
                    break;
                case NotificationType.PlayerSurrendered:
                    Message = messyNofitication.SendingUser.UserName + " has surrendered your game " + messyNofitication.Game.Name;
                    Url = urlContext.Action("Details", "Game", new { id = messyNofitication.GameID });
                    break;
                case NotificationType.GameCancelled:
                    Message = messyNofitication.SendingUser.UserName + " has cancelled a game you had joined.";
                    break;
                case NotificationType.ThanksForSupport:
                    Message = "Thank you for supporting us!";
                    break;
                case NotificationType.AccountPromotion:
                    Message = "Your account has been promoted! You can now create or join more games.";
                    break;
                case NotificationType.TurnTimerOn:
                    Message = messyNofitication.SendingUser.UserName + " has enabled the turn timer in your game " + messyNofitication.Game.Name;
                    Url = urlContext.Action("Details", "Game", new { id = messyNofitication.GameID });
                    break;
                case NotificationType.TurnTimerOff:
                    if (messyNofitication.SendingUser != null)
                        Message = messyNofitication.SendingUser.UserName + " has disabled the turn timer in your game " + messyNofitication.Game.Name;
                    else Message = "The turn timer in your game " + messyNofitication.Game.Name + " has been disabled due to too many consecutive players being skipped.";
                    Url = urlContext.Action("Details", "Game", new { id = messyNofitication.GameID });
                    break;
                case NotificationType.TurnTimerChanged:
                    Message = messyNofitication.SendingUser.UserName + " has modified the turn timer in your game " + messyNofitication.Game.Name;
                    Url = urlContext.Action("Details", "Game", new { id = messyNofitication.GameID });
                    break;
                case NotificationType.Skipped:
                    Message = "You have been skipped in your game " + messyNofitication.Game.Name + ". The Civ V AI will play your turn.";
                    Url = urlContext.Action("Details", "Game", new { id = messyNofitication.GameID });
                    break;
                case NotificationType.GiftedSupport:
                    Message = messyNofitication.SendingUser.UserName + " has gifted you an increased game limit!";
                    break;
                case NotificationType.NewMessage:
                    Message = "New message from " + messyNofitication.SendingUser.UserName;
                    Url = urlContext.Action("Messages", "User") + "#" + messyNofitication.Message.Id;
                    break;

                default:
                    throw new ArgumentException("Invalid notification");
            }
            Checked = messyNofitication.Checked;
            SentHowLongAgo = DateTime.UtcNow.Subtract(messyNofitication.Sent).FriendlyTimeDiff();
            ID = messyNofitication.NotificationID;
        }

        private string GetUserName(User user, Notification context, bool capitalize)
        {
            if (user == null)
                return capitalize ? "Nobody" : "nobody";
            else if (user.UserId == context.ReceivingUser.UserId)
                return capitalize ? "You" : "you";
            else return user.UserName;
        }

        public int ID { get; private set; }
        public string Message { get; private set; }
        public string Url { get; private set; }
        public DateTime? Checked { get; set; }
        public string SentHowLongAgo { get; private set; }
    }
}