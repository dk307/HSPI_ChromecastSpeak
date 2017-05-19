using Newtonsoft.Json;

namespace SharpCaster.Models.ChromecastRequests
{
    public class VolumeRequest : RequestWithId
    {
        public VolumeRequest(double? level, bool? muted, int requestId) : base("SET_VOLUME", requestId)
        {
            VolumeDataObject = new VolumeDataObject { Level = level, Muted = muted };
        }

        [JsonProperty("volume")]
        public VolumeDataObject VolumeDataObject { get; set; }
    }
}