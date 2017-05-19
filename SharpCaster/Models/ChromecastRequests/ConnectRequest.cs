using Newtonsoft.Json;

namespace SharpCaster.Models.ChromecastRequests
{
    internal class ConnectRequest : Request
    {
        public ConnectRequest()
            : base("CONNECT")
        {
            UserAgent = "Homeseer Speak PlugIn";
        }

        [JsonProperty("userAgent")]
        public string UserAgent { get; set; }
    }
}