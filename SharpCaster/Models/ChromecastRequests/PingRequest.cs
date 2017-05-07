using System.Runtime.Serialization;

namespace SharpCaster.Models.ChromecastRequests
{
    [DataContract]
    internal class PingRequest : Request
    {
        public PingRequest()
            : base("PING")
        {
        }
    }
}