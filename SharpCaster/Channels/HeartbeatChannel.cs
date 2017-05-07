using System;
using System.Diagnostics;
using System.Threading.Tasks;
using SharpCaster.Models;
using System.Threading;
using NullGuard;

namespace SharpCaster.Channels
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class HeartbeatChannel : ChromecastChannel
    {
        public HeartbeatChannel(ChromeCastClient client) :
            base(client, "urn:x-cast:com.google.cast.tp.heartbeat")
        {
            MessageReceived += HeartbeatChannel_MessageReceived;
        }

        private void HeartbeatChannel_MessageReceived(object sender, ChromecastSSLClientDataReceivedArgs e)
        {
            Debug.WriteLine(e.Message.GetJsonType());
            if (Client.Connected || e.Message.GetJsonType() != "PONG") return;
            Client.Connected = true;
        }

        public void StartHeartbeat(CancellationToken token)
        {
            Task.Run(async () =>
            {
                TimeSpan pingTimeSpan = TimeSpan.FromSeconds(3);
                while (!token.IsCancellationRequested)
                {
                    await Write(MessageFactory.Ping, token).ConfigureAwait(false);
                    await Task.Delay(pingTimeSpan, token).ConfigureAwait(false);
                }
            }, token);
        }
    }
}