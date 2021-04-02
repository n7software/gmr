using System;
using System.Collections.Generic;

namespace GmrServer.Models
{
    public class Blog
    {
        public Blog()
        {
            Posts = new SortedSet<BlogPost>();
        }

        public Blog(Blog other)
        {
            this.Title = other.Title;
            this.Url = other.Url;
            this.Description = other.Description;
            Posts = new SortedSet<BlogPost>();
            foreach (var post in other.Posts)
            {
                var clone = new BlogPost
                {
                    Title = post.Title,
                    Url = post.Url,
                    CommentsUrl = post.CommentsUrl,
                    CommentCount = post.CommentCount,
                    PublicationDate = post.PublicationDate,
                    Author = post.Author,
                    Content = post.Content,
                    ContentHtml = post.ContentHtml,
                    Categories = new HashSet<string>(post.Categories)
                };
                Posts.Add(clone);
            }
        }

        public string Title { get; set; }
        public string Url { get; set; }
        public string Description { get; set; }
        public SortedSet<BlogPost> Posts { get; set; }
    }

    public class BlogPost : IComparable<BlogPost>, IEquatable<BlogPost>
    {
        public BlogPost()
        {
            Categories = new HashSet<string>();
        }

        public string Title { get; set; }
        public string Url { get; set; }
        public string CommentsUrl { get; set; }
        public int CommentCount { get; set; }
        public DateTime PublicationDate { get; set; }
        public string Author { get; set; }
        public ISet<string> Categories { get; set; }
        public string Content { get; set; }
        public string ContentHtml { get; set; }

        public int CompareTo(BlogPost other)
        {
            return this.PublicationDate.CompareTo(other.PublicationDate);
        }

        public bool Equals(BlogPost other)
        {
            return this.Url == other.Url;
        }

        public override bool Equals(object obj)
        {
            if (obj is BlogPost)
                return this.Equals((BlogPost)obj);
            else return false;
        }

        public override int GetHashCode()
        {
            return Url.GetHashCode();
        }
    }
}