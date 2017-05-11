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
        }

        public override void Abort()
        {
            abortCancellationSource.Cancel();
            heartBeatFinished.Task.Wait();
        }

        internal override void OnMessageReceived(CastMessage castMessage)
        {
            Debug.WriteLine(castMessage.GetJsonType());
            if (Client.Connected || castMessage.GetJsonType() != "PONG") return;
            Client.Connected = true;
        }

        public void StartHeartbeat(CancellationToken token)
        {
            combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(token, abortCancellationSource.Token);
            var combinedToken = combinedCancellationSource.Token;
            Task.Run(async () =>
            {
                TimeSpan pingTimeSpan = TimeSpan.FromSeconds(5);
                try
                {
                    while (!combinedToken.IsCancellationRequested)
                    {
                        await Write(MessageFactory.Ping, combinedToken).ConfigureAwait(false);
                        await Task.Delay(pingTimeSpan, combinedToken).ConfigureAwait(false);
                    }
                }
                finally
                {
                    heartBeatFinished.SetResult(true);
                }
            }, token);
        }

        private readonly CancellationTokenSource abortCancellationSource = new CancellationTokenSource();
        private CancellationTokenSource combinedCancellationSource;

        private readonly TaskCompletionSource<bool> heartBeatFinished = new TaskCompletionSource<bool>();
    }
}