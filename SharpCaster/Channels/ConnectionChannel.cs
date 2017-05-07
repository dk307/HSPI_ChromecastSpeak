using System.Threading.Tasks;
using SharpCaster.Models;
using System.Threading;
using NullGuard;

namespace SharpCaster.Channels
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class ConnectionChannel : ChromecastChannel
    {
        public ConnectionChannel(ChromeCastClient client) :
            base(client, "urn:x-cast:com.google.cast.tp.connection")
        {
        }

        public async void OpenConnection(CancellationToken token)
        {
            await Write(MessageFactory.Connect(), token);
        }

        public async Task ConnectWithDestination(string transportId, CancellationToken token)
        {
            await Write(MessageFactory.ConnectWithDestination(transportId), token);
        }
    }
}