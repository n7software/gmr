//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace GmrLib.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class Civilization
    {
        public Civilization()
        {
            this.GamePlayers = new HashSet<GamePlayer>();
            this.SteamGameCivilizations = new HashSet<SteamGameCivilization>();
            this.Games = new HashSet<Game>();
        }
    
        public int CivID { get; set; }
        public string Name { get; set; }
        public string Leader { get; set; }
        public string ImageUrl { get; set; }
        public string SmallImageUrl { get; set; }
        public int InGameId { get; set; }
        public string InGameKey { get; set; }
        public string InGameLeaderKey { get; set; }
        public string InGameColorKey { get; set; }
        public string BackgroundColor { get; set; }
        public string ForegroundColor { get; set; }
    
        public virtual ICollection<GamePlayer> GamePlayers { get; set; }
        public virtual ICollection<SteamGameCivilization> SteamGameCivilizations { get; set; }
        public virtual ICollection<Game> Games { get; set; }
    }
}