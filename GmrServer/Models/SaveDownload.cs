using System.IO;
using System.ServiceModel;

namespace GmrServer.Models
{
    [MessageContract]
    public class SaveDownload
    {
        [MessageHeader]
        public long FileLength { get; set; }

        [MessageBodyMember]
        public Stream FileStream { get; set; }
    }

    [MessageContract]
    public class SaveDownloadRequest
    {
        [MessageHeader]
        public string AuthKey { get; set; }

        [MessageHeader]
        public int GameID { get; set; }
    }
}