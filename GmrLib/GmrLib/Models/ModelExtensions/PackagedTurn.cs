using System;

namespace GmrLib.Models
{
    public class PackagedTurn
    {
        #region Properties

        public int TurnId { get; set; }

        public int Number { get; set; }

        public long UserId { get; set; }

        public DateTime Started { get; set; }

        public DateTime? Expires { get; set; }

        public bool Skipped { get; set; }

        public int PlayerNumber { get; set; }

        public bool IsFirstTurn { get; set; }

        #endregion

        #region Constructor

        public PackagedTurn()
        {
            TurnId = -1;
            UserId = -1;
            Number = -1;
            Started = DateTime.MinValue;
            Expires = null;
            Skipped = false;
            PlayerNumber = -1;
            IsFirstTurn = false;
        }

        public PackagedTurn(Turn turn, int playerNumber, bool isFirstTurn)
            : this()
        {
            if (turn != null)
            {
                TurnId = turn.TurnID;
                UserId = turn.UserID;
                Number = turn.Number;
                Started = turn.Started ?? DateTime.MinValue;
                Skipped = turn.SkipStatus != SkipStatus.NotSkipped;
                IsFirstTurn = isFirstTurn;
                PlayerNumber = playerNumber;

                if (turn.Game.TurnTimeLimit != null)
                {
                    Expires = turn.ExpiresOn;
                }
            }
        }

        #endregion
    }
}