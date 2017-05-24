using Newtonsoft.Json;
using SharpCaster.Models.MediaStatus;
using SharpCaster.Models.Metadata;

namespace SharpCaster.Models.ChromecastRequests
{
    internal class MediaData
    {
        public MediaData(string url, string contentType, IMetadata metadata = null,
                         string streamType = "BUFFERED", double duration = 0d,
                         object customData = null, Track[] tracks = null)
        {
            Url = url;
            ContentType = contentType;
            StreamType = streamType;
            Duration = duration;
            Metadata = metadata;
            CustomData = customData;
            Tracks = tracks;
        }

        [JsonProperty("contentId")]
        public string Url { get; set; }

        [JsonProperty("contentType")]
        public string ContentType { get; set; }

        [JsonProperty("metadata")]
        public IMetadata Metadata { get; set; }

        ///-------------------------------------------------------------------------------------------------
        /// <summary>
        ///     Gets or sets the type of the stream. This can be BUFFERED, LIVE or NONE
        /// </summary>
        ///
        /// <value>
        ///     The type of the stream.
        /// </value>
        ///-------------------------------------------------------------------------------------------------
        [JsonProperty("streamType")]
        public string StreamType { get; set; }

        [JsonProperty("duration")]
        public double Duration { get; set; }

        [JsonProperty("customData")]
        public object CustomData { get; set; }

        [JsonProperty("tracks")]
        public Track[] Tracks { get; set; }
    }
}