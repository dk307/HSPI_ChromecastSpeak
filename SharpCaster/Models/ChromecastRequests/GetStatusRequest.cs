using System.Runtime.Serialization;

namespace SharpCaster.Models.ChromecastRequests
{
    [DataContract]
    internal class GetStatusRequest : RequestWithId
    {
        public GetStatusRequest(int requestId)
            : base("GET_STATUS", requestId)
        {
        }
    }
}