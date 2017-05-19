using Newtonsoft.Json;

namespace SharpCaster.Models.ChromecastRequests
{
    internal class GetStatusRequest : RequestWithId
    {
        public GetStatusRequest(int requestId)
            : base("GET_STATUS", requestId)
        {
        }
    }
}