using System.Runtime.Serialization;

namespace SharpCaster.Models.ChromecastRequests
{
    [DataContract]
    internal class ConnectRequest : Request
    {
        public ConnectRequest()
            : base("CONNECT")
        {
            UserAgent = "Homeseer Speak PlugIn";
        }

        [DataMember(Name = "userAgent")]
        public string UserAgent { get; set; }
    }
}