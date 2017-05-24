using Newtonsoft.Json;

namespace SharpCaster.Models.ChromecastRequests
{
    internal class LoadRequest : RequestWithId
    {
        public LoadRequest(int requestId, string sessionId, MediaData media, bool autoPlay, double currentTime,
            object customData = null, int[] activeTrackIds = null)
            : base("LOAD", requestId)
        {
            SessionId = sessionId;
            Media = media;
            AutoPlay = autoPlay;
            CurrentTime = currentTime;
            Customdata = customData;
            ActiveTrackIds = activeTrackIds;
        }

        [JsonProperty("sessionId")]
        public string SessionId { get; private set; }

        [JsonProperty("media")]
        public MediaData Media { get; private set; }

        [JsonProperty("autoplay")]
        public bool AutoPlay { get; private set; }

        [JsonProperty("currentTime")]
        public double CurrentTime { get; private set; }

        [JsonProperty("customData")]
        public object Customdata { get; }

        [JsonProperty("activeTrackIds")]
        public int[] ActiveTrackIds { get; set; }
    }
}