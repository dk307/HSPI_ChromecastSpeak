using System;
using System.Diagnostics;
using System.Threading.Tasks;
using SharpCaster.Models;
using System.Threading;
using NullGuard;
using Hspi;

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
            heartBeatTask.WaitForFinishNoCancelException().Wait();
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
            heartBeatTask = HeartBeat(combinedCancellationSource.Token);
        }

        private async Task HeartBeat(CancellationToken combinedToken)
        {
            TimeSpan pingTimeSpan = TimeSpan.FromSeconds(5);
            while (!combinedToken.IsCancellationRequested)
            {
                await Write(MessageFactory.Ping, combinedToken).ConfigureAwait(false);
                await Task.Delay(pingTimeSpan, combinedToken).ConfigureAwait(false);
            }
        }

        private Task heartBeatTask;
        private readonly CancellationTokenSource abortCancellationSource = new CancellationTokenSource();
        private CancellationTokenSource combinedCancellationSource;
    }
}