using System.Collections.Generic;

namespace GmrLib.Models
{
    public class PackagedGameCache
    {
        public PackagedGameCache()
        {
            PackagedGames = new Dictionary<int, PackagedGame>();
        }

        public Dictionary<int, PackagedGame> PackagedGames { get; set; }
    }
}