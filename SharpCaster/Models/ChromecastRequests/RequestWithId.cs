using Newtonsoft.Json;

namespace SharpCaster.Models.ChromecastRequests
{
    public abstract class RequestWithId : Request
    {
        protected RequestWithId(string requestType, int requestId)
            : base(requestType)
        {
            RequestId = requestId;
        }

        [JsonProperty("requestId")]
        public int RequestId { get; set; }
    }
}