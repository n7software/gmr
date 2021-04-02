using System.Collections.Generic;

namespace GmrLib.Models
{
    public partial class Message : ICommentBase
    {
        #region ICommentBase

        public CommentType CommentType
        {
            get { return CommentType.Message; }
        }

        public int CommentTypeInt
        {
            get { return (int)CommentType.Message; }
        }

        IEnumerable<Comment> ICommentBase.Comments
        {
            get { return Comments; }
        }

        #endregion
    }
}