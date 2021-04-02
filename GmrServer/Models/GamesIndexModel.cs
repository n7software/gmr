using GmrLib.Models;
using System.Collections.Generic;

namespace GmrServer.Models
{
    public class GamesIndexModel
    {
        public IEnumerable<Game> MyGames { get; set; }

        public IEnumerable<Game> PublicGames { get; set; }
    }
}