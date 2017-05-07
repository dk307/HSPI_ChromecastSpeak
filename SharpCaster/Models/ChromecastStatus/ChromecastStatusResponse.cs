using Newtonsoft.Json;

namespace SharpCaster.Models.ChromecastStatus
{
    internal class ChromecastStatusResponse
    {
        public int requestId { get; set; }

        [JsonProperty("status")]
        public ChromecastStatus ChromecastStatus { get; set; }

        public string type { get; set; }
    }
}