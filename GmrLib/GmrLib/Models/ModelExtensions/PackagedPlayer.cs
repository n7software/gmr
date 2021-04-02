using GmrLib.SteamAPI;

namespace GmrLib.Models
{
    public class PackagedPlayer
    {
        #region Properties

        public long SteamID { get; set; }

        public string PersonaName { get; set; }

        public string AvatarUrl { get; set; }

        public SteamPlayerState PersonaState { get; set; }

        public int GameID { get; set; }

        #endregion

        #region Constructor

        public PackagedPlayer()
        {
            SteamID = -1;
            PersonaName = string.Empty;
            AvatarUrl = string.Empty;
            PersonaState = SteamPlayerState.Offline;
            GameID = -1;
        }

        public PackagedPlayer(SteamPlayer player)
        {
            SteamID = player.SteamID;
            PersonaName = player.PersonaName;
            AvatarUrl = player.Avatar;
            PersonaState = player.PersonaState;
            GameID = player.GameID;
        }

        #endregion
    }
}