using Newtonsoft.Json;

namespace SharpCaster.Models.ChromecastRequests
{
    internal class LaunchRequest : RequestWithId
    {
        public LaunchRequest(string appId, int requestId)
            : base("LAUNCH", requestId)
        {
            ApplicationId = appId;
        }

        [JsonProperty("appId")]
        public string ApplicationId { get; set; }
    }
}