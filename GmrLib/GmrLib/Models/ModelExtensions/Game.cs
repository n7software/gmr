using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;

namespace GmrLib.Models
{
    [MetadataType(typeof(GameMetadata))]
    public partial class Game : ICommentBase
    {
        public static Dictionary<GameType, string> AllGameTypes = new Dictionary<GameType, string>
        {
            {GameType.Standard, "Normal"},
            {GameType.Mod, "Basic Mod"},
            {GameType.ModTotalConversion, "Total Mod"},
            {GameType.Scenario, "Scenario"}
        };

        private byte[] UnalteredSaveData;
        private string UnalteredSavePath;

        public int NumberOfHumanPlayers
        {
            get { return Players.Count(p => p.UserID != 0); }
        }

        public int NumberOfFilledPlayerSlots
        {
            get { return HasStarted ? NumberOfHumanPlayers : Players.Count; }
        }

        public bool HasStarted
        {
            get { return Started != null; }
        }

        public bool HasAiPlayers
        {
            get { return Players.Count(p => p.UserID == 0) > 0; }
        }

        public string UnalteredSaveFilePath
        {
            get
            {
                if (string.IsNullOrEmpty(UnalteredSavePath) || GameID < 1)
                {
                    UnalteredSavePath = Path.Combine(UnalteredSaveDir, GameID + ".Civ5Save");
                }

                return UnalteredSavePath;
            }
        }

        public static readonly string UnalteredSaveDir = Path.Combine(Save.SaveDir, @"Originals\");

        public byte[] UnalteredSaveFileBytes
        {
            get
            {
                if (UnalteredSaveData == null)
                {
                    if (File.Exists(UnalteredSaveFilePath))
                    {
                        UnalteredSaveData = File.ReadAllBytes(UnalteredSaveFilePath);
                    }
                }
                return UnalteredSaveData;
            }
            set { UnalteredSaveData = value; }
        }

        public double AverageTurnsPerDay
        {
            get
            {
                TimeSpan? age = DateTime.UtcNow - Started;
                if (age != null)
                {
                    return Math.Round(((Turns.Count - 1) / age.Value.TotalDays), 1);
                }

                return 0.0;
            }
        }

        public double TotalGameTurns
        {
            get { return Math.Round(((Turns.Count - 1) / (double)Players.Count(p => p.UserID != 0)), 1); }
        }

        public int TotalSubmittedTurns
        {
            get { return Turns.Count - 1; }
        }

        public ISet<DayOfWeek> TurnTimerActiveDays
        {
            get
            {
                string raw = TurnTimerDays ?? "SuMoTuWeThFrSa";
                var shortNameMapping = new Dictionary<string, string>();
                string[] originalNames = Enum.GetNames(typeof(DayOfWeek));
                foreach (string day in originalNames)
                    shortNameMapping.Add(day.Substring(0, 2), day);

                ISet<DayOfWeek> excludedDays = new HashSet<DayOfWeek>();

                for (int i = 0; i < raw.Length; i += 2)
                {
                    string day = raw.Substring(i, 2);
                    if (shortNameMapping.ContainsKey(day))
                    {
                        string fullDay = shortNameMapping[day];
                        excludedDays.Add((DayOfWeek)Enum.Parse(typeof(DayOfWeek), fullDay));
                    }
                }

                return excludedDays;
            }
        }

        public GameType GameType
        {
            get { return (GameType)Type; }
            set { Type = (int)value; }
        }

        public bool IsSaveAvailable()
        {
            return Turns.Count() > 1 &&
                   Turns.OrderByDescending(t => t.Number).Skip(1).First().Save != null;
        }

        public bool IsOnFirstTurn()
        {
            return Turns.Count() <= 1;
        }

        public Guid GenerateInviteToken()
        {
            Guid inviteToken = Guid.NewGuid();
            InviteTokens.Add(new GameInviteToken { Token = inviteToken, Created = DateTime.UtcNow });
            while (InviteTokens.Count > 50)
                InviteTokens.Remove(InviteTokens.First());
            return inviteToken;
        }

        public List<Tuple<string, TimeSpan>> GetAverageTurnTimePerPlayer()
        {
            var result = new List<Tuple<string, TimeSpan>>();
            var userTimes = new Dictionary<User, List<TimeSpan>>();

            foreach (Turn turn in Turns)
            {
                if (!userTimes.ContainsKey(turn.User))
                {
                    userTimes[turn.User] = new List<TimeSpan>();
                }

                if (turn.Finished.HasValue && turn.Started.HasValue)
                {
                    userTimes[turn.User].Add((turn.Finished.Value - turn.Started.Value));
                }
            }


            foreach (var userTime in userTimes)
            {
                if (userTime.Value.Count > 0)
                {
                    long averageTicks = Convert.ToInt64(userTime.Value.Average(timeSpan => timeSpan.Ticks));
                    result.Add(new Tuple<string, TimeSpan>(userTime.Key.UserName, new TimeSpan(averageTicks)));
                }
            }

            result.Sort((t1, t2) => t1.Item2.Ticks.CompareTo(t2.Item2.Ticks));

            return result;
        }

        public static string GameTypeToString(GameType gameType)
        {
            switch (gameType)
            {
                case GameType.Standard:
                    return "Normal";
                case GameType.Mod:
                case GameType.ModTotalConversion:
                    return "Mod";
                case GameType.Scenario:
                    return "Scenario";
                default:
                    return "Invalid";
            }
        }

        public static Game GetGameById(int gameId)
        {
            var gmrDb = GmrEntities.CreateContext();
            return gmrDb.Games.FirstOrDefault(g => g.GameID == gameId);
        }

        #region ICommentBase

        public int Id
        {
            get { return GameID; }
        }

        public CommentType CommentType
        {
            get { return CommentType.Game; }
        }

        public int CommentTypeInt
        {
            get { return (int)CommentType.Game; }
        }

        IEnumerable<Comment> ICommentBase.Comments
        {
            get { return Comments; }
        }

        #endregion
    }

    public enum GameType
    {
        Standard = 0,
        Mod = 1,
        ModTotalConversion = 2,
        Scenario = 3
    }

    public class GameMetadata
    {
        [Required]
        [StringLength(50)]
        public string Name { get; set; }
    }

    internal class InfiniteExcludedDaysEnumerator : IEnumerator<DateTime>
    {
        private readonly ISet<DayOfWeek> DaysToRun;
        private readonly DateTime Start;
        private DateTime _Current;

        public InfiniteExcludedDaysEnumerator(DateTime start, ISet<DayOfWeek> daysToRun)
        {
            Start = start.Date;
            DaysToRun = daysToRun;
            Reset();
        }

        public DateTime Current
        {
            get { return _Current; }
        }

        public void Dispose()
        {
        }

        object IEnumerator.Current
        {
            get { return _Current; }
        }

        public bool MoveNext()
        {
            if (DaysToRun.Count >= 7)
                return false;

            _Current = _Current.AddDays(1);
            while (DaysToRun.Contains(_Current.DayOfWeek))
                _Current = _Current.AddDays(1);

            return true;
        }

        public void Reset()
        {
            _Current = Start;
            MoveNext();
        }
    }
}