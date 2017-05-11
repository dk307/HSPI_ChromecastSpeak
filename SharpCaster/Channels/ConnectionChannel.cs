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
            //if (castMessage.GetJsonType() == "CLOSE")
            //{
            //    Client.Connected = false;
            //};
        }

        public override void Abort()
        {
        }

        public async void OpenConnection(CancellationToken token)
        {
            await Write(MessageFactory.Connect(), token);
        }

        public async Task CloseConnection(CancellationToken token)
        {
            await Write(MessageFactory.Close, token);
        }

        public async Task ConnectWithDestination(string transportId, CancellationToken token)
        {
            await Write(MessageFactory.ConnectWithDestination(transportId), token);
        }
    }
}