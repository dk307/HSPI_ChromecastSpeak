using Newtonsoft.Json;
using SharpCaster.JsonConverters;
using SharpCaster.Models.Enums;
using SharpCaster.Models.MediaStatus;
using System.Collections.Generic;

namespace SharpCaster.Models.Metadata
{
    internal interface IMetadata
    {
        List<ChromecastImage> images { get; set; }

        [JsonConverter(typeof(MetadataTypeEnumConverter))]
        MetadataType metadataType { get; set; }

        string title { get; set; }
    }
}