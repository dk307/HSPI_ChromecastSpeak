using System.Runtime.Serialization;

namespace SharpCaster.Models.ChromecastRequests
{
    [DataContract]
    public class VolumeRequest : RequestWithId
    {
        public VolumeRequest(double? level, bool? muted, int requestId) : base("SET_VOLUME", requestId)
        {
            VolumeDataObject = new VolumeDataObject { Level = level, Muted = muted };
        }

        [DataMember(Name = "volume")]
        public VolumeDataObject VolumeDataObject { get; set; }
    }
}