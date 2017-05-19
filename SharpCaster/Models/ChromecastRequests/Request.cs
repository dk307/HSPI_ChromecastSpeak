 using Newtonsoft.Json;

namespace SharpCaster.Models.ChromecastRequests
{
     public abstract class Request
    {
        protected Request(string requestType)
        {
            RequestType = requestType;
        }

        [JsonProperty("type")]
        public string RequestType { get; set; }

        public string ToJson()
        {
            var settings = new JsonSerializerSettings {NullValueHandling = NullValueHandling.Ignore};
            return JsonConvert.SerializeObject(this, settings);
        }
    }
}