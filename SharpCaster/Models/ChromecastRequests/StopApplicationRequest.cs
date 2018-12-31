using System.Runtime.Serialization;

namespace SharpCaster.Models.ChromecastRequests
{
    [DataContract]
    public class StopApplicationRequest : RequestWithId
    {
        public StopApplicationRequest(string sessionId, int requestId)
            : base("STOP", requestId)
        {
            SessionId = sessionId;
        }

        [DataMember(Name = "sessionId")]
        public string SessionId { get; set; }
    }
}