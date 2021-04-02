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
    
    public partial class Message
    {
        public Message()
        {
            this.Comments = new HashSet<Comment>();
            this.Recipients = new HashSet<MessageRecipient>();
            this.Notifications = new HashSet<Notification>();
        }
    
        public int Id { get; set; }
        public long CreatedByUserId { get; set; }
        public System.DateTime Created { get; set; }
        public System.DateTime LastUpdated { get; set; }
    
        public virtual ICollection<Comment> Comments { get; set; }
        public virtual ICollection<MessageRecipient> Recipients { get; set; }
        public virtual User CreatedByUser { get; set; }
        public virtual ICollection<Notification> Notifications { get; set; }
    }
}
