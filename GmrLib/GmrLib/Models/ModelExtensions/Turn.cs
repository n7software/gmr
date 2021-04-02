using System;
using System.Collections.Generic;

namespace GmrLib.Models
{
    public partial class Turn
    {
        public static readonly DateTime MaxSqlDateTime = new DateTime(9999, 12, 31);

        public DateTime CalculateExpiration()
        {
            if (Game.TurnTimeLimit == null)
            {
                return MaxSqlDateTime;
            }

            ISet<DayOfWeek> daysToRun = Game.TurnTimerActiveDays;

            if (Started == null ||
                daysToRun.Count >= 8 || daysToRun.Count == 0 ||
                (Game.TurnTimerStart.HasValue && Game.TurnTimerStop.HasValue &&
                 Game.TurnTimerStart.Value > Game.TurnTimerStop.Value))
                throw new InvalidOperationException("Turn timer setup with invalid data");

            DateTime expirationTimeLocal = Started.Value.AddHours(Game.TurnTimeLimit.Value).AddHours(Game.TimeZone);
            DateTime turnStartedLocalTime = Started.Value.AddHours(Game.TimeZone);

            if (!daysToRun.Contains(turnStartedLocalTime.DayOfWeek))
                expirationTimeLocal =
                    expirationTimeLocal.Add(turnStartedLocalTime.AddDays(1).Date.Subtract(turnStartedLocalTime));

            if (Game.TurnTimerStop.HasValue && expirationTimeLocal.TimeOfDay > Game.TurnTimerStop.Value)
                expirationTimeLocal = expirationTimeLocal.AddDays(1).Date.Add(Game.TurnTimerStart.Value);
            else if (Game.TurnTimerStart.HasValue && expirationTimeLocal.TimeOfDay < Game.TurnTimerStart.Value)
                expirationTimeLocal = expirationTimeLocal.Date.Add(Game.TurnTimerStart.Value);


            var excludedDays = new InfiniteExcludedDaysEnumerator(turnStartedLocalTime, daysToRun);

            DateTime next = excludedDays.Current;
            while (next > turnStartedLocalTime && next < expirationTimeLocal)
            {
                expirationTimeLocal = expirationTimeLocal.AddDays(1);
                excludedDays.MoveNext();
                next = excludedDays.Current;
            }

            DateTime expirationTimeUTC = expirationTimeLocal.AddHours(-Game.TimeZone);

            return expirationTimeUTC;
        }

        public SkipStatus SkipStatus
        {
            get { return (SkipStatus)Skipped; }
            set { Skipped = (int)value; }
        }

        public TimeSpan TimeRemaining
        {
            get { return new TimeSpan(ExpiresOn.Ticks - DateTime.UtcNow.Ticks); }
        }

        public string SkipStatusDisplay
        {
            get
            {
                switch (SkipStatus)
                {
                    case SkipStatus.NotSkipped:
                        goto default;

                    case SkipStatus.TurnTimerSkip:
                        return "Yes";

                    case SkipStatus.VacationModeSkip:
                        return "Vacation";

                    default:
                        return "No";
                }
            }
        }
    }

    public enum SkipStatus
    {
        NotSkipped = 0,
        VacationModeSkip = 1,
        TurnTimerSkip = 2
    }
}