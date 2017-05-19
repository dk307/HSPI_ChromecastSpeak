using Newtonsoft.Json;

namespace SharpCaster.Models.ChromecastRequests
{
    public class VolumeDataObject
    {
        [JsonProperty("level")]
        public double? Level { get; set; }

        [JsonProperty("muted")]
        public bool? Muted { get; set; }
    }
}