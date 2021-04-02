using System;

namespace GmrLib.SteamAPI
{
    public class SteamPlayer : IEquatable<SteamPlayer>
    {
        public long SteamID { get; set; }
        public SteamCommunityVisibility CommunityVisibilityState { get; set; }
        public SteamProfileState ProfileState { get; set; }
        public string PersonaName { get; set; }
        public DateTime? LastLogOff { get; set; }
        public string ProfileUrl { get; set; }
        public string Avatar { get; set; }
        public string AvatarMedium { get; set; }
        public string AvatarFull { get; set; }
        public SteamPlayerState PersonaState { get; set; }
        public string RealName { get; set; }
        public long PrimaryClanId { get; set; }
        public DateTime? TimeCreated { get; set; }
        public string GameServerIp { get; set; }
        public string GameExtraInfo { get; set; }
        public int GameID { get; set; }
        public long GameServerSteamID { get; set; }
        public string LocCountryCode { get; set; }
        public string LocStateCode { get; set; }
        public int LocCityID { get; set; }

        public bool Equals(SteamPlayer other)
        {
            if ((other as object) == null)
                return false;

            return SteamID.Equals(other.SteamID);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is SteamPlayer))
                return false;
            return Equals((SteamPlayer)obj);
        }

        public override int GetHashCode()
        {
            return (int)SteamID;
        }

        public static bool operator ==(SteamPlayer a, SteamPlayer b)
        {
            if (((object)a) == null)
                return (((object)b) == null);

            return a.Equals(b);
        }

        public static bool operator !=(SteamPlayer a, SteamPlayer b)
        {
            if (((object)a) == null)
                return (((object)b) != null);

            return !a.Equals(b);
        }
    }

    public enum SteamPlayerState
    {
        Offline = 0,
        Online = 1,
        Busy = 2,
        Away = 3,
        Snooze = 4
    }

    public enum SteamCommunityVisibility
    {
        Private = 1,
        FriendsOnly = 2,
        Public = 3
    }

    public enum SteamProfileState
    {
        NotSetUp = 0,
        SetUp = 1
    }
}