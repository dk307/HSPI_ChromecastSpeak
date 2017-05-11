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
            if (response.requestId != 0)
            {
                if (TryRemoveRequestTracking(response.requestId, out var completed))
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