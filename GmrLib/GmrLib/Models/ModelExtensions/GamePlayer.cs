using System;
using System.Collections.Generic;
using System.Linq;

namespace GmrLib.Models
{
    public partial class GamePlayer
    {
        public TimeSpan? AverageTurnTimeSpan
        {
            get
            {
                if (!AverageTurnTime.HasValue)
                {
                    RecalculateAverageTurnTime();
                }

                if (AverageTurnTime.HasValue)
                    return new TimeSpan(0, 0, AverageTurnTime.Value);
                return null;
            }
        }

        public void RecalculateAverageTurnTime()
        {
            IEnumerable<double> turnSeconds =
                from turn in Game.Turns
                where User == turn.User
                      && turn.Skipped != (int)SkipStatus.VacationModeSkip
                      && turn.Finished != null
                select (turn.Finished.Value - turn.Started.Value).TotalSeconds;

            if (turnSeconds.Count() > 0)
                AverageTurnTime = (int)turnSeconds.Average();
        }
    }
}