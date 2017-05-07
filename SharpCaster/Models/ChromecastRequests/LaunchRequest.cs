using System.Runtime.Serialization;

namespace SharpCaster.Models.ChromecastRequests
{
    [DataContract]
    internal class LaunchRequest : RequestWithId
    {
        public LaunchRequest(string appId, int requestId)
            : base("LAUNCH", requestId)
        {
            ApplicationId = appId;
        }

        [DataMember(Name = "appId")]
        public string ApplicationId { get; set; }
    }
}