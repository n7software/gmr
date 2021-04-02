using System;
using System.Collections.Generic;
using System.Dynamic;

namespace GmrLib.Models
{
    public static class PackagedPreferences
    {
        public static readonly Dictionary<string, string> All = new Dictionary<string, string>
        {
            {"EmailGameEnds", "true"},
            {"EmailGameInvites", "true"},
            {"EmailOther", "true"},
            {"EmailComment", "true"},
            {"EmailTurnNotify", "true"},
            {"GamePassword", String.Empty},
            {"EmailPlayerJoinsGame", "true"},
            {"EmailTurnTimerChanges", "true"},
            {"EmailSkipped", "true"},
            {"VacationMode", "false"},
            {"TimeZone", "0"},
            {"AutoDST", "true"},
            {"ShowPublicNewGames", "true"},
            {"ShowPublicInProgressGames", "true"},
            {"SortPublicGamesMethod", "1"},
            {"EmailNewMessage", "true"}
        };

        public static dynamic GetPackagedPreferences(this User user)
        {
            return Get(user);
        }

        public static dynamic Get(User user)
        {
            dynamic prefs = new ExpandoObject();
            var found = new HashSet<string>();

            if (user != null)
            {
                foreach (Preference p in user.Preferences)
                {
                    ParseKeyValPair(prefs, p.Key, p.Value);
                    found.Add(p.Key);
                }
            }

            foreach (var kvp in All)
            {
                if (!found.Contains(kvp.Key))
                    ParseKeyValPair(prefs, kvp.Key, kvp.Value);
            }

            return prefs;
        }

        private static void ParseKeyValPair(dynamic prefs, string key, string value)
        {
            switch (key)
            {
                case "GamePassword":
                    ((IDictionary<string, object>)prefs).Add(key, value);
                    break;

                default:
                    dynamic val = value;
                    bool bParse;
                    int iParse;
                    decimal dParse;
                    if (bool.TryParse(val, out bParse))
                        val = bParse;
                    else if (int.TryParse(val, out iParse))
                        val = iParse;
                    else if (decimal.TryParse(val, out dParse))
                        val = dParse;

                    ((IDictionary<string, object>)prefs).Add(key, val);
                    break;
            }
        }
    }

    public enum PublicGameSortMethod
    {
        DateCreated = 0,
        DateCreatedDesc = 1,
        HumanPlayers = 2,
        HumanPlayersCount = 3
    }
}