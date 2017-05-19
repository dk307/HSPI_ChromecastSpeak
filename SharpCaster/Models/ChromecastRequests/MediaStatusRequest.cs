using System.Runtime.Serialization;

namespace SharpCaster.Models.ChromecastRequests
{
    public class MediaStatusRequest : RequestWithId
    {
        public MediaStatusRequest(int requestId) : base("GET_STATUS", requestId)
        {
        }
    }
}