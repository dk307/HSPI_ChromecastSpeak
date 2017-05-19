using Newtonsoft.Json;

namespace SharpCaster.Models.MediaStatus
{
    internal class Track
    {
        [JsonProperty("customData")]
        public object CustomData { get; set; }

        [JsonProperty("language")]
        public object Language { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("subtype")]
        public string SubType { get; set; }

        [JsonProperty("trackContentId")]
        public string TrackContentId { get; set; }

        [JsonProperty("trackContentType")]
        public string TrackContentType { get; set; }

        [JsonProperty("trackId")]
        public long TrackId { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }
}