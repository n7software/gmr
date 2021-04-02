using System.Collections.Generic;
using System.Linq;

namespace GmrLib.Models
{
    public class PackagedGame
    {
        #region Properties

        public string Name { get; set; }

        public int GameId { get; set; }

        public List<PackagedUser> Players { get; set; }

        public PackagedTurn CurrentTurn { get; set; }

        public GameType Type { get; set; }

        #endregion

        #region Constructors

        public PackagedGame()
        {
            GameId = -1;
            Name = "NA";
            Players = new List<PackagedUser>();
            CurrentTurn = new PackagedTurn();
            Type = GameType.Standard;
        }

        public PackagedGame(Game game)
            : this()
        {
            if (game != null)
            {
                GameId = game.GameID;
                Name = game.Name;
                Type = game.GameType;

                Players.AddRange(game.Players.Select(gp => new PackagedUser(gp)));

                Turn turn = game.Turns.OrderByDescending(t => t.Number).FirstOrDefault();

                bool isFirstTurn = turn.Number == 1 && (game.GameType == GameType.Standard);

                CurrentTurn = new PackagedTurn(turn, turn.GamePlayer.TurnOrder, isFirstTurn);
            }
        }

        #endregion
    }
}