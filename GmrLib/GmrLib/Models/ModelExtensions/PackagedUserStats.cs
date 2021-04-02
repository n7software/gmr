using System;

namespace GmrLib.Models
{
    public class PackagedUserStats
    {
        public long UserId { get; set; }
        public string UserName { get; set; }
        public string AvatarUrl { get; set; }
        public int TotalPoints { get; set; }
        public string Rank { get; set; }
        public int GameCount { get; set; }
        public int TurnCount { get; set; }
        public int SkipCount { get; set; }
        public int VacationCount { get; set; }
        public TimeSpan AverageTurnTime { get; set; }
        public int Index { get; set; }

        public DateTime LastUpdated { get; set; }
    }
}