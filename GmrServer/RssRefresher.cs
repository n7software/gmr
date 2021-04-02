using GmrServer.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Configuration;
using System.Xml.Linq;

namespace GmrServer
{
    public class RssRefresher
    {
        public Blog CachedBlog = null;

        private static Task RunTaskOnIntervalAsync(Action task, CancellationToken cancelToken, TimeSpan interval)
        {
            return Task.Run(async () =>
            {
                while (!cancelToken.IsCancellationRequested)
                {
                    task();
                    await Task.Delay(interval, cancelToken);
                }
            }, cancelToken);
        }

        CancellationTokenSource Cancel;
        string Url;
        TimeSpan CheckInterval;
        int MaxCachedPosts;

        private RssRefresher(int? maxCachedPosts = null, string url = null, TimeSpan? checkInterval = null)
        {
            if (!maxCachedPosts.HasValue)
                maxCachedPosts = int.Parse(WebConfigurationManager.AppSettings["MaxBlogPosts"]);
            MaxCachedPosts = maxCachedPosts.Value;

            if (url == null)
                url = WebConfigurationManager.AppSettings["BlogRssUrl"];
            Url = url;
            if (!checkInterval.HasValue)
                checkInterval = TimeSpan.FromSeconds(int.Parse(WebConfigurationManager.AppSettings["BlogRssIntervalSeconds"] ?? "60"));
            CheckInterval = checkInterval.Value;
        }

        public void Start()
        {
            Cancel = new CancellationTokenSource();
            RunTaskOnIntervalAsync(CheckFeed, Cancel.Token, CheckInterval);
        }

        public void Stop()
        {
            Cancel.Cancel();
        }

        private void CheckFeed()
        {
            var xml = XDocument.Load(Url);
            XElement channel = (XElement)xml.Root.FirstNode;
            Blog blog = null;
            if (CachedBlog == null)
            {
                blog = new Blog
                {
                    Title = channel.Element("title").Value,
                    Description = channel.Element("description").Value,
                    Url = channel.Element("link").Value
                };
            }
            lock (this)
            {
                if (blog != null)
                    CachedBlog = blog;
                var items = channel.Elements("item");
                foreach (var item in items)
                {
                    var post = new BlogPost
                    {
                        Title = item.Element("title").Value,
                        Url = item.Element("link").Value,
                        CommentsUrl = item.Element("comments").Value,
                        CommentCount = int.Parse(item.Element("{http://purl.org/rss/1.0/modules/slash/}comments").Value),
                        PublicationDate = DateTime.Parse(item.Element("pubDate").Value),
                        Author = item.Element("{http://purl.org/dc/elements/1.1/}creator").Value,
                        Content = item.Element("description").Value,
                        ContentHtml = item.Element("{http://purl.org/rss/1.0/modules/content/}encoded").Value
                    };
                    var categories = item.Elements("category");
                    foreach (var category in categories)
                    {
                        post.Categories.Add(category.Value);
                    }
                    CachedBlog.Posts.Add(post);
                }
                while (CachedBlog.Posts.Count > MaxCachedPosts)
                    CachedBlog.Posts.Remove(CachedBlog.Posts.First());
            }
        }

        private static RssRefresher _Instance;
        public static RssRefresher Instance
        {
            get
            {
                if (_Instance == null)
                    _Instance = new RssRefresher();
                return _Instance;
            }
        }
    }
}