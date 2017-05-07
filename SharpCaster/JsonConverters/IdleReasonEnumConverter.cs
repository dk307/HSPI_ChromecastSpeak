using Newtonsoft.Json;
using SharpCaster.Models.MediaStatus;
using System;

namespace SharpCaster.JsonConverters
{
    internal class IdleReasonEnumConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            var playerState = (IdleReason)value;
            switch (playerState)
            {
                case IdleReason.CANCELLED:
                    writer.WriteValue("CANCELLED");
                    break;

                case IdleReason.ERROR:
                    writer.WriteValue("CANCELLED");
                    break;

                case IdleReason.FINISHED:
                    writer.WriteValue("FINISHED");
                    break;

                case IdleReason.INTERRUPTED:
                    writer.WriteValue("INTERRUPTED");
                    break;

                case IdleReason.NONE:
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
            var enumString = (string)reader.Value;
            IdleReason idleReason = IdleReason.NONE;

            if (Enum.TryParse<IdleReason>(enumString, out idleReason))
            {
                return idleReason;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(reader));
            }
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(string);
        }
    }
}