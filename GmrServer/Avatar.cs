using GmrLib.SteamAPI;
using System.Web;

namespace GmrServer
{
    public class Avatar
    {
        public string LargeBorder { get; set; }
        public string MediumBorder { get; set; }
        public string SmallBorder { get; set; }

        private const string SmallGrayBorder = "~/Content/images/gryavsm.png";
        private const string MediumGrayBorder = "~/Content/images/gryav.png";
        private const string LargeGrayBorder = "~/Content/images/gryav_lg.png";

        private const string SmallBlueBorder = "~/Content/images/bluavsm.png";
        private const string MediumBlueBorder = "~/Content/images/bluav.png";
        private const string LargeBlueBorder = "~/Content/images/bluav_lg.png";

        private const string SmallGreenBorder = "~/Content/images/grnavsm.png";
        private const string MediumGreenBorder = "~/Content/images/grnav.png";
        private const string LargeGreenBorder = "~/Content/images/grnav_lg.png";

        private const string SmallOrangeBorder = "~/Content/images/ornavsm.png";
        private const string MediumOrangeBorder = "~/Content/images/ornav.png";
        private const string LargeOrangeBorder = "~/Content/images/ornav_lg.png";

        public static Avatar GetBorderImages(long steamID)
        {
            if (steamID == 0) //0 is the AI Player
                return new Avatar { SmallBorder = SmallOrangeBorder, MediumBorder = MediumOrangeBorder, LargeBorder = LargeOrangeBorder };

            Global.SteamApiInstance.RequestUserPolling(steamID);

            SteamPlayer player = Global.GetCachedPlayer(steamID);

            if (player != null)
            {
                if (player.PersonaState == SteamPlayerState.Offline)
                {
                    HttpContext.Current.Application.UnLock();
                    return new Avatar { SmallBorder = SmallGrayBorder, MediumBorder = MediumGrayBorder, LargeBorder = LargeGrayBorder };
                }
                else
                {
                    if (player.GameID == Global.CivVGameID)
                    {
                        HttpContext.Current.Application.UnLock();
                        return new Avatar { SmallBorder = SmallGreenBorder, MediumBorder = MediumGreenBorder, LargeBorder = LargeGreenBorder };
                    }
                    else
                    {
                        HttpContext.Current.Application.UnLock();
                        return new Avatar { SmallBorder = SmallBlueBorder, MediumBorder = MediumBlueBorder, LargeBorder = LargeBlueBorder };
                    }
                }
            }

            return new Avatar { SmallBorder = SmallGrayBorder, MediumBorder = MediumGrayBorder, LargeBorder = LargeGrayBorder };
        }

    }
}