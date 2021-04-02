using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web.Mvc;

namespace GmrLib.Models
{
    public class TurnTimer
    {
        public TurnTimer()
        {
            Editable = false;
        }

        public TurnTimer(Game game)
        {
            Editable = false;
            GameID = game.GameID;
            GameStarted = game.HasStarted;

            var lastTurn = game.Turns.OrderByDescending(t => t.Number).FirstOrDefault();

            bool turnsTaken = lastTurn != null;

            TurnExpiration = turnsTaken ? lastTurn.ExpiresOn : Turn.MaxSqlDateTime;
            TimeZone = game.TimeZone;
            SkipLimit = game.SkipLimit;
            Days = game.TurnTimeLimit.HasValue ? game.TurnTimeLimit.Value / 24 : 0;
            Hours = game.TurnTimeLimit.HasValue ? game.TurnTimeLimit.Value % 24 : 0;
            Start = game.TurnTimerStart;
            Stop = game.TurnTimerStop;
            CurrentPlayerTimesSkipped = 0;

            if (lastTurn != null)
            {
                GamePlayer lastPlayer = game.Players.FirstOrDefault(p => p.UserID == lastTurn.UserID);
                if (lastPlayer != null)
                {
                    CurrentPlayerTimesSkipped = lastPlayer.TimesSkipped;
                }
            }

            DaysLeft = turnsTaken ? lastTurn.TimeRemaining.Days : 0;
            if (DaysLeft < 0)
            {
                DaysLeft = 0;
            }
            HoursLeft = turnsTaken ? lastTurn.TimeRemaining.Hours : 0;
            if (HoursLeft < 0)
            {
                HoursLeft = 0;
            }

            Sunday = game.TurnTimerActiveDays.Contains(DayOfWeek.Sunday);
            Monday = game.TurnTimerActiveDays.Contains(DayOfWeek.Monday);
            Tuesday = game.TurnTimerActiveDays.Contains(DayOfWeek.Tuesday);
            Wednesday = game.TurnTimerActiveDays.Contains(DayOfWeek.Wednesday);
            Thursday = game.TurnTimerActiveDays.Contains(DayOfWeek.Thursday);
            Friday = game.TurnTimerActiveDays.Contains(DayOfWeek.Friday);
            Saturday = game.TurnTimerActiveDays.Contains(DayOfWeek.Saturday);
        }

        public int GameID { get; private set; }
        public bool GameStarted { get; private set; }

        public DateTime TurnExpiration { get; set; }
        public int TimeZone { get; set; }

        public string FriendlyTimeZone
        {
            get
            {
                string sign = TimeZone >= 0 ? "+" : "-";
                return "GMT" + sign + Math.Abs(TimeZone);
            }
        }

        public string FriendlyTurnExpiration
        {
            get
            {
                return TurnExpiration.AddHours(TimeZone).ToString(@"dddd a\t h:mm tt") + " GMT" +
                       (TimeZone >= 0 ? "+" : String.Empty) + TimeZone;
            }
        }

        public int CurrentPlayerTimesSkipped { get; private set; }

        [Range(1, 50)]
        public int? SkipLimit { get; set; }

        public bool Sunday { get; set; }
        public bool Monday { get; set; }
        public bool Tuesday { get; set; }
        public bool Wednesday { get; set; }
        public bool Thursday { get; set; }
        public bool Friday { get; set; }
        public bool Saturday { get; set; }

        [Range(0, 7)]
        public int? Days { get; set; }

        [Range(0, 23)]
        public int? Hours { get; set; }

        public int DaysLeft { get; private set; }
        public int HoursLeft { get; private set; }

        public string FriendlyDaysAndHours
        {
            get
            {
                return Days + " " + (Days == 1 ? "day" : "days") + ", " + Hours + " " + (Hours == 1 ? "hour" : "hours");
            }
        }

        [DataType(DataType.Time)]
        public TimeSpan? Start { get; set; }

        public string FriendlyStart
        {
            get { return MakeTimeSpanPretty(Start); }
        }

        public TimeSpan? Stop { get; set; }

        public string FriendlyStop
        {
            get { return MakeTimeSpanPretty(Stop); }
        }

        public bool Editable { get; set; }

        public static List<SelectListItem> TimeZones
        {
            get
            {
                var timeZones = new List<SelectListItem>();
                for (int i = -10; i <= 14; i++)
                {
                    string sign = i >= 0 ? "+" : "-";
                    timeZones.Add(new SelectListItem
                    {
                        Text = "GMT" + sign + Math.Abs(i),
                        Value = i.ToString(),
                        Selected = i == 0
                    });
                }
                return timeZones;
            }
        }

        public string RunsOnTheseDays(bool addSpacing)
        {
            string days = String.Empty;
            string space = addSpacing ? " " : String.Empty;
            if (Sunday)
                days += "Su" + space;
            if (Monday)
                days += "Mo" + space;
            if (Tuesday)
                days += "Tu" + space;
            if (Wednesday)
                days += "We" + space;
            if (Thursday)
                days += "Th" + space;
            if (Friday)
                days += "Fr" + space;
            if (Saturday)
                days += "Sa";
            return days.Trim();
        }

        private static string MakeTimeSpanPretty(TimeSpan? timeSpan)
        {
            if (timeSpan.HasValue)
                return new DateTime().Add(timeSpan.Value).ToString("h:mm tt");
            return String.Empty;
        }
    }
}