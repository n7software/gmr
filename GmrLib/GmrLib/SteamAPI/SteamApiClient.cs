using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace GmrLib.SteamAPI
{
    public class SteamApiClient
    {
        private const string SteamApiUrl = "http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/";

        private static readonly string SteamKey = ConfigurationManager.AppSettings["SteamApiKey"];

        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private readonly HashSet<long> SteamIDs = new HashSet<long>();

        private readonly int Timeout;


        public SteamApiClient(long? steamID, int timeout = 100000)
        {
            if (steamID.HasValue)
                AddUser(steamID.Value);
            Timeout = timeout;
        }

        public int TotalClients
        {
            get
            {
                int count = 0;

                count = SteamIDs.Count;

                return count;
            }
        }

        public long Last
        {
            get
            {
                long last = 0;

                last = SteamIDs.Last();

                return last;
            }
        }

        public void AddUser(long steamID)
        {
            if (SteamIDs.Count >= 100)
                throw new OverflowException("Max number of clients reached (100)");

            SteamIDs.Add(steamID);
        }

        public void RemoveUser(long steamID)
        {
            SteamIDs.Remove(steamID);
        }

        public bool Contains(long steamID)
        {
            return SteamIDs.Contains(steamID);
        }

        private string GetSteamIDList()
        {
            var s = new StringBuilder();
            foreach (long l in SteamIDs)
            {
                s.Append(l.ToString());
                s.Append(",");
            }
            s.Remove(s.Length - 1, 1);
            return s.ToString();
        }

        public ISet<SteamPlayer> GetPlayerSummaries()
        {
            var players = new HashSet<SteamPlayer>();
            string data;

            if (SteamIDs.Count == 0)
                return players;

            try
            {
                data = WebHelpers.HttpGet(SteamApiUrl + "?key=" + SteamKey + "&steamids=" + GetSteamIDList(), Timeout);
            }
            catch
            {
                return players;
            }

            var player = new SteamPlayer();

            using (var reader = new JsonTextReader(new StringReader(data)))
            {
                while (reader.Read())
                {
                    if (!String.IsNullOrWhiteSpace(reader.Value as string))
                    {
                        PropertyInfo prop = typeof(SteamPlayer).GetProperty(reader.Value as string,
                            BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                        if (prop != null)
                        {
                            if ((reader.Value as string).ToLower() == "steamid" && player.SteamID != 0)
                            {
                                players.Add(player);
                                player = new SteamPlayer();
                            }

                            reader.Read();
                            object val = reader.Value.ToString();

                            if (prop.PropertyType == typeof(DateTime?))
                                val = UnixTimeToDateTime(val.ToString());
                            else if (prop.PropertyType.IsSubclassOf(typeof(Enum)))
                                val = Enum.Parse(prop.PropertyType, val.ToString());
                            else
                            {
                                MethodInfo parse = prop.PropertyType.GetMethod("Parse", new[] { typeof(String) });
                                try
                                {
                                    if (parse != null)
                                        val = parse.Invoke(null, new[] { val });
                                }
                                catch
                                {
                                    if (prop.Name == "GameID")
                                        val = -1;
                                }
                            }

                            prop.SetValue(player, val, null);
                        }
                    }
                }
                players.Add(player);
            }
            return players;
        }

        private static DateTime UnixTimeToDateTime(string text)
        {
            int seconds = int.Parse(text, CultureInfo.InvariantCulture);
            return Epoch.AddSeconds(seconds);
        }
    }
}