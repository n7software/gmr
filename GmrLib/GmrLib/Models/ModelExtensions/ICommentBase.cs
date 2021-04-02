using System.Collections.Generic;

namespace GmrLib.Models
{
    public interface ICommentBase
    {
        int Id { get; }
        CommentType CommentType { get; }
        int CommentTypeInt { get; }
        IEnumerable<Comment> Comments { get; }
    }

    public enum CommentType
    {
        Game = 0,
        Message = 1
    }
}