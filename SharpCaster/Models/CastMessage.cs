using Newtonsoft.Json;
using ProtoBuf;
using SharpCaster.Extensions;
using System.IO;

namespace SharpCaster.Models
{
    [ProtoContract]
    internal sealed class CastMessage
    {
        public CastMessage() :
            this("receiver-0", "sender-0")
        {
        }

        public CastMessage(string destinationId, string sourceId)
        {
            ProtocolVersion = 0;
            PayloadType = 0;
            PayloadBinary = null;
            DestinationId = destinationId;
            SourceId = sourceId;
        }

        [ProtoMember(1, IsRequired = true, Name = "protocol_version")]
        public int ProtocolVersion = 0;

        [ProtoMember(2, IsRequired = true, Name = "source_id")]
        public string SourceId;

        [ProtoMember(3, IsRequired = true, Name = "destination_id")]
        public string DestinationId;

        [ProtoMember(4, IsRequired = true, Name = "namespace")]
        public string Namespace;

        [ProtoMember(5, IsRequired = true, Name = "payload_type")]
        public int PayloadType;

        [ProtoMember(6, IsRequired = false, Name = "payload_utf8")]
        public string PayloadUtf8;

        [ProtoMember(7, IsRequired = false, Name = "payload_binary")]
        public byte[] PayloadBinary;

        public string GetJsonType()
        {
            if (string.IsNullOrEmpty(PayloadUtf8))
            {
                return string.Empty;
            }

            dynamic stuff = JsonConvert.DeserializeObject(PayloadUtf8);

            return stuff.type;
        }

        public byte[] ToProto(bool includeHeader = true)
        {
            using (var bufStream = new MemoryStream())
            {
                Serializer.Serialize(bufStream, this);

                if (includeHeader)
                {
                    var buffer = bufStream.ToArray().AddHeader();
                    return buffer;
                }
                return bufStream.ToArray();
            }
        }
    }
}