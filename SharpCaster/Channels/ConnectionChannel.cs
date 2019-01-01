using NullGuard;
using SharpCaster.Models;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCaster.Channels
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class ConnectionChannel : ChromecastChannel
    {
        public ConnectionChannel(ChromeCastClient client) :
            base(client, "urn:x-cast:com.google.cast.tp.connection")
        {
        }

        internal override void OnMessageReceived(CastMessage castMessage)
        {
            if (castMessage.GetJsonType() == "CLOSE")
            {
                Task.Run(() => closeReceived.Cancel());
            };
        }

        public override Task Abort()
        {
            return Task.CompletedTask;
        }

        public async void OpenConnection(CancellationToken token)
        {
            await Write(MessageFactory.Connect(), token).ConfigureAwait(false);
        }

        public async Task CloseConnection(CancellationToken token)
        {
            await Write(MessageFactory.Close, token).ConfigureAwait(false);
        }

        public async Task ConnectWithDestination(string transportId, CancellationToken token)
        {
            await Write(MessageFactory.ConnectWithDestination(transportId), token).ConfigureAwait(false);
        }

        public CancellationToken CloseReceived => closeReceived.Token;

        private readonly CancellationTokenSource closeReceived = new CancellationTokenSource();
    }
}