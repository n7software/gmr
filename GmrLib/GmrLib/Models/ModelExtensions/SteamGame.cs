using System;
using System.Linq;

namespace GmrLib.Models
{
    public partial class SteamGame
    {
        public static SteamGame Civ5
        {
            get
            {
                try
                {
                    GmrEntities gmrDb = GmrEntities.CreateContext();

                    return gmrDb.SteamGames.FirstOrDefault(game => game.SteamID == 8930L);
                }
                catch (Exception exc)
                {
                    DebugLogger.WriteException(exc, "Getting Vanilla Civ Game");
                    return null;
                }
            }
        }
    }
}