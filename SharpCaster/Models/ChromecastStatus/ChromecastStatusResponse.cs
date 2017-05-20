using Newtonsoft.Json;

namespace SharpCaster.Models.ChromecastStatus
{
    internal class ChromecastStatusResponse
    {
        [JsonConstructor]
        public ChromecastStatusResponse(int requestId, ChromecastStatus status, string type)
        {
            RequestId = requestId;
            ChromecastStatus = status;
            Type = type;
        }

        public int RequestId { get; }
        public ChromecastStatus ChromecastStatus { get; }
        public string Type { get; }
    }
}