using Newtonsoft.Json;
using NullGuard;
using SharpCaster.Models;
using SharpCaster.Models.ChromecastRequests;
using SharpCaster.Models.ChromecastStatus;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCaster.Channels
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class ReceiverChannel : ChromecastChannelWithRequestTracking
    {
        public ReceiverChannel(ChromeCastClient client) :
            base(client, "urn:x-cast:com.google.cast.receiver")
        {
        }

        internal override void OnMessageReceived(CastMessage castMessage)
        {
            var json = castMessage.PayloadUtf8;
            var response = JsonConvert.DeserializeObject<ChromecastStatusResponse>(json);
            if (response.ChromecastStatus != null)
            {
                Client.ChromecastStatus = response.ChromecastStatus;
            }
            if (response.RequestId != 0)
            {
                if (TryRemoveRequestTracking(response.RequestId, out var completed))
                {
                    completed.SetResult(true);
                }
            }
        }

        public async Task LaunchApplication(string applicationId, CancellationToken token)
        {
            int requestId = RequestIdProvider.Next;
            var message = MessageFactory.Launch(applicationId, requestId);
            var requestCompletedSource = await AddRequestTracking(requestId, token);
            await Write(message, token);
            await WaitOnRequestCompletion(requestCompletedSource.Task, token);
        }

        public async Task GetChromecastStatus(CancellationToken token)
        {
            int requestId = RequestIdProvider.Next;
            var requestCompletedSource = await AddRequestTracking(requestId, token);
            await Write(MessageFactory.Status(requestId), token);
            await WaitOnRequestCompletion(requestCompletedSource.Task, token);
        }

        public async Task SetVolume(double? level, bool? muted, CancellationToken token)
        {
            if (level.HasValue && (level < 0 || level > 1.0))
            {
                throw new ArgumentException("level must be between 0.0f and 1.0f", nameof(level));
            }

            int requestId = RequestIdProvider.Next;
            var requestCompletedSource = await AddRequestTracking(requestId, token);
            await Write(MessageFactory.Volume(level, muted, requestId), token);
            await WaitOnRequestCompletion(requestCompletedSource.Task, token);
        }
    }
}