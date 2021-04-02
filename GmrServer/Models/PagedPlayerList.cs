namespace GmrServer.Models
{
    public class PagedPlayerList
    {
        #region Properties
        public int Id { get; set; }
        public string DisplayName { get; set; }
        public string QueryUrl { get; set; }
        public string UserPageQueryUrl { get; set; }
        public int TotalPages { get; set; }
        #endregion

        #region Constructor
        public PagedPlayerList()
        {
            Id = 0;
            DisplayName = string.Empty;
            QueryUrl = string.Empty;
            UserPageQueryUrl = string.Empty;
            TotalPages = 0;
        }
        #endregion
    }
}