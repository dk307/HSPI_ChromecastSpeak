using System;
using Newtonsoft.Json;
using SharpCaster.Models.Enums;

using System.Globalization;

namespace SharpCaster.JsonConverters
{
    internal class MetadataTypeEnumConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            var metadataType = (MetadataType)value;
            switch (metadataType)
            {
                case MetadataType.GENERIC:
                    writer.WriteValue(0);
                    break;

                case MetadataType.MOVIE:
                    writer.WriteValue(1);
                    break;

                case MetadataType.TV_SHOW:
                    writer.WriteValue(2);
                    break;

                case MetadataType.MUSIC_TRACK:
                    writer.WriteValue(3);
                    break;

                case MetadataType.PHOTO:
                    writer.WriteValue(4);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(value));
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }
            var enumString = (Int64)reader.Value;
            MetadataType metadataType;

            if (Enum.TryParse(enumString.ToString(CultureInfo.InvariantCulture), out metadataType))
            {
                return metadataType;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(existingValue));
            }
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(int);
        }
    }
}