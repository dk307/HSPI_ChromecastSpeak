using NullGuard;
using SharpCaster.Models;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx.Synchronous;
using Hspi.Utils;

namespace SharpCaster.Channels
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class HeartbeatChannel : ChromecastChannel
    {
        public HeartbeatChannel(ChromeCastClient client) :
            base(client, "urn:x-cast:com.google.cast.tp.heartbeat")
        {
        }

        public override Task Abort()
        {
            combinedCancellationSource.Cancel();
            return heartBeatTask;
        }

        internal override void OnMessageReceived(CastMessage castMessage)
        {
            Trace.WriteLine(castMessage.GetJsonType());
            if (Client.Connected || castMessage.GetJsonType() != "PONG")
            {
                return;
            }
            Client.Connected = true;
        }

        public void StartHeartbeat(CancellationToken token)
        {
            combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            heartBeatTask = TaskHelper.StartAsync(HeartBeat, combinedCancellationSource.Token);
        }

        private async Task HeartBeat()
        {
            TimeSpan pingTimeSpan = TimeSpan.FromSeconds(4);
            while (!combinedCancellationSource.IsCancellationRequested)
            {
                await Write(MessageFactory.Ping, combinedCancellationSource.Token).ConfigureAwait(false);
                await Task.Delay(pingTimeSpan, combinedCancellationSource.Token).ConfigureAwait(false);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                combinedCancellationSource?.Cancel();
                heartBeatTask?.WaitWithoutException();
                combinedCancellationSource?.Dispose();
            }

            base.Dispose(disposing);
        }

        private Task heartBeatTask;
        private CancellationTokenSource combinedCancellationSource;
    }
}