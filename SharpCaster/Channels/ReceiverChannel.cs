using Hspi.Utils;
using Newtonsoft.Json;
using NullGuard;
using SharpCaster.Exceptions;
using SharpCaster.Models;
using SharpCaster.Models.ChromecastRequests;
using SharpCaster.Models.ChromecastStatus;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCaster.Channels
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class ReceiverChannel : ChromecastChannelWithRequestTracking<ChromecastStatusResponse>
    {
        public ReceiverChannel(ChromeCastClient client) :
            base(client, "urn:x-cast:com.google.cast.receiver")
        {
        }

        internal override void OnMessageReceived(CastMessage castMessage)
        {
            var json = castMessage.PayloadUtf8;
            var response = JsonConvert.DeserializeObject<ChromecastStatusResponse>(json);
            if (response.RequestId != 0)
            {
                if (TryRemoveRequestTracking(response.RequestId, out var completed))
                {
                    completed.SetResult(response);
                }
            }
        }

        public async Task<ChromecastStatus> LaunchApplication(string applicationId, CancellationToken token)
        {
            int requestId = RequestIdProvider.Next;
            var message = MessageFactory.Launch(applicationId, requestId);
            var requestCompletedSource = await AddRequestTracking(requestId, token).ConfigureAwait(false);
            await Write(message, token).ConfigureAwait(false);
            var chromecastStatusResponse = await requestCompletedSource.Task.WaitOnRequestCompletion(token).ConfigureAwait(false);

            if (chromecastStatusResponse.ChromecastStatus == null)
            {
                throw new ApplicationLoadException(string.Empty, chromecastStatusResponse.Type);
            }
            return chromecastStatusResponse.ChromecastStatus;
        }

        public async Task<ChromecastStatus> StopApplication(string applicationId, CancellationToken token)
        {
            int requestId = RequestIdProvider.Next;
            var message = MessageFactory.StopApplication(applicationId, requestId);
            var requestCompletedSource = await AddRequestTracking(requestId, token).ConfigureAwait(false);
            await Write(message, token).ConfigureAwait(false);
            var chromecastStatusResponse = await requestCompletedSource.Task.WaitOnRequestCompletion(token).ConfigureAwait(false);
            return chromecastStatusResponse.ChromecastStatus;
        }

        public async Task<ChromecastStatus> GetChromecastStatus(CancellationToken token)
        {
            int requestId = RequestIdProvider.Next;
            var requestCompletedSource = await AddRequestTracking(requestId, token).ConfigureAwait(false);
            await Write(MessageFactory.Status(requestId), token).ConfigureAwait(false);
            var chromecastStatusResponse = await requestCompletedSource.Task.WaitOnRequestCompletion(token).ConfigureAwait(false);
            return chromecastStatusResponse.ChromecastStatus;
        }

        public async Task<ChromecastStatus> SetVolume(double? level, bool? muted, CancellationToken token)
        {
            if (level.HasValue && (level < 0 || level > 1.0))
            {
                throw new ArgumentException("level must be between 0.0f and 1.0f", nameof(level));
            }

            int requestId = RequestIdProvider.Next;
            var requestCompletedSource = await AddRequestTracking(requestId, token).ConfigureAwait(false);
            await Write(MessageFactory.Volume(level, muted, requestId), token).ConfigureAwait(false);
            var chromecastStatusResponse = await requestCompletedSource.Task.WaitOnRequestCompletion(token).ConfigureAwait(false);
            return chromecastStatusResponse.ChromecastStatus;
        }
    }
}