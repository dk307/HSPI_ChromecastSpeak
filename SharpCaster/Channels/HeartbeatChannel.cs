using Hspi;
using NullGuard;
using SharpCaster.Models;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

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
            abortCancellationSource.Cancel();
            return heartBeatTask.WaitForFinishNoCancelException();
        }

        internal override void OnMessageReceived(CastMessage castMessage)
        {
            Trace.WriteLine(castMessage.GetJsonType());
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
            TimeSpan pingTimeSpan = TimeSpan.FromSeconds(4);
            while (!combinedToken.IsCancellationRequested)
            {
                await Write(MessageFactory.Ping, combinedToken).ConfigureAwait(false);
                await Task.Delay(pingTimeSpan, combinedToken).ConfigureAwait(false);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                abortCancellationSource.Dispose();
                if (combinedCancellationSource != null)
                {
                    combinedCancellationSource.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        private Task heartBeatTask;
        private readonly CancellationTokenSource abortCancellationSource = new CancellationTokenSource();
        private CancellationTokenSource combinedCancellationSource;
    }
}