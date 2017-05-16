using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SharpCaster.Models;
using SharpCaster.Models.ChromecastStatus;
using System.Threading;
using SharpCaster.Models.ChromecastRequests;
using System.Collections.Concurrent;
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

        internal override void OnMessageReceived(CastMessage castMessage)
        {
            if (castMessage.GetJsonType() == "CLOSE")
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await Client.Abort();
                    }
                    catch (ObjectDisposedException)
                    { }
                });
            };
        }

        public override Task Abort()
        {
            return Task.FromResult(true);
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
    }
}