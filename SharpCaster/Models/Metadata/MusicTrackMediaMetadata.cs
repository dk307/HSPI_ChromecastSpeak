using Newtonsoft.Json;
using SharpCaster.JsonConverters;
using SharpCaster.Models.Enums;
using SharpCaster.Models.MediaStatus;
using System.Collections.Generic;

namespace SharpCaster.Models.Metadata
{
    //Fields: https://developers.google.com/cast/docs/reference/chrome/chrome.cast.media.MusicTrackMediaMetadata
    internal class MusicTrackMediaMetadata : IMetadata
    {
        public MusicTrackMediaMetadata()
        {
            metadataType = MetadataType.MUSIC_TRACK;
        }

        public string albumArtist { get; set; }
        public string albumName { get; set; }
        public string artist { get; set; }
        public string composer { get; set; }
        public int discNumber { get; set; }
        public List<ChromecastImage> images { get; set; }

        [JsonConverter(typeof(MetadataTypeEnumConverter))]
        public MetadataType metadataType { get; set; }

        public string releaseDate { get; set; }
        public string songName { get; set; }
        public string title { get; set; }
        public int trackNumber { get; set; }
    }
}