using System.IO;
using System.ServiceModel;

namespace GmrServer.Models
{
    [MessageContract]
    public class SaveUpload
    {
        [MessageHeader]
        public string AuthKey { get; set; }

        [MessageHeader]
        public int TurnId { get; set; }

        [MessageBodyMember]
        public Stream FileStream { get; set; }
    }

    [MessageContract]
    public class SaveUploadResponse
    {
        [MessageHeader]
        public SubmitTurnResult Result { get; set; }
    }
}