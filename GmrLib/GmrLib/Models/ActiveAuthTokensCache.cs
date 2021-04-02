using System;
using System.Collections.Generic;

namespace GmrLib.Models
{
    public class ActiveAuthTokensCache
    {
        public ActiveAuthTokensCache()
        {
            ActiveAuthTokens = new Dictionary<string, DateTime>();
        }

        public Dictionary<string, DateTime> ActiveAuthTokens { get; set; }
    }
}