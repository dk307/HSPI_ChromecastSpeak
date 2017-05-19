namespace SharpCaster.Models.ChromecastRequests
{
    internal class PingRequest : Request
    {
        public PingRequest()
            : base("PING")
        {
        }
    }
}