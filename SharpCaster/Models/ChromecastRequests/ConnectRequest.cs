using System.Runtime.Serialization;

namespace SharpCaster.Models.ChromecastRequests
{
    [DataContract]
    internal class ConnectRequest : Request
    {
        public ConnectRequest()
            : base("CONNECT")
        {
        }
    }
}