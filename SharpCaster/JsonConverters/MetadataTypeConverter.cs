using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharpCaster.Models.Enums;
using SharpCaster.Models.Metadata;
using System.Globalization;

namespace SharpCaster.JsonConverters
{
    internal class MetadataTypeConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            writer.WriteValue(value);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject jObject = JObject.Load(reader);

            var value = jObject.GetValue("metadataType", StringComparison.Ordinal).ToString();
            MetadataType metadataType;

            if (Enum.TryParse(value, out metadataType))
            {
                switch (metadataType)
                {
                    case MetadataType.GENERIC:
                        return jObject.ToObject<GenericMediaMetadata>();

                    case MetadataType.MOVIE:
                        return jObject.ToObject<MovieMediaMetadata>();

                    case MetadataType.TV_SHOW:
                        return jObject.ToObject<TvShowMediaMetadata>();

                    case MetadataType.MUSIC_TRACK:
                        return jObject.ToObject<MusicTrackMediaMetadata>();

                    case MetadataType.PHOTO:
                        return jObject.ToObject<PhotoMediaMetadata>();

                    default:
                        throw new ArgumentOutOfRangeException(nameof(reader));
                }
            }
            throw new ArgumentOutOfRangeException(nameof(reader));
        }

        public override bool CanConvert(Type objectType)
        {
            return true;
        }
    }
}