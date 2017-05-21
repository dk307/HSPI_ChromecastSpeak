using Hspi;
using Newtonsoft.Json;
using SharpCaster.Models;
using SharpCaster.Models.ChromecastRequests;
using SharpCaster.Models.ChromecastStatus;
using SharpCaster.Models.MediaStatus;
using SharpCaster.Models.Metadata;
using System;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCaster.Channels
{
    internal class MediaChannel : ChromecastChannelWithRequestTracking<MediaStatus>
    {
        public event EventHandler<MediaStatus> MessageReceived;

        public MediaChannel(ChromeCastClient client)
            : base(client, "urn:x-cast:com.google.cast.media")
        {
        }

        internal override void OnMessageReceived(CastMessage castMessage)
        {
            var json = castMessage.PayloadUtf8;
            var response = JsonConvert.DeserializeObject<MediaStatusResponse>(json);

            if (response.requestId != 0)
            {
                if (TryRemoveRequestTracking(response.requestId, out var completed))
                {
                    completed.SetResult(response.status?.FirstOrDefault());
                }
            }
            else
            {
                MessageReceived?.Invoke(this, response.status?.FirstOrDefault());
            }
        }

        public async Task<MediaStatus> GetMediaStatus(string transportId, CancellationToken token)
        {
            int requestId = RequestIdProvider.Next;

            var requestCompletedSource = await AddRequestTracking(requestId, token).ConfigureAwait(false);
            await Write(MessageFactory.MediaStatus(transportId, requestId), token).ConfigureAwait(false);
            return await requestCompletedSource.Task.WaitOnRequestCompletion(token).ConfigureAwait(false);
        }

        public async Task<MediaStatus> LoadMedia(
            ChromecastApplication application,
            Uri mediaUrl,
            string contentType,
            CancellationToken token,
            IMetadata metadata = null,
            string streamType = "BUFFERED",
            double duration = 0D,
            object customData = null,
            Track[] tracks = null,
            int[] activeTrackIds = null,
            bool autoPlay = true,
            double currentTime = 0D)
        {
            int requestId = RequestIdProvider.Next;
            var mediaObject = new MediaData(mediaUrl.ToString(), contentType, metadata, streamType, duration, customData, tracks);
            var req = new LoadRequest(requestId, application.SessionId, mediaObject, autoPlay, currentTime,
                                      customData, activeTrackIds);

            var reqJson = req.ToJson();
            var requestCompletedSource = await AddRequestTracking(requestId, token).ConfigureAwait(false);
            await Write(MessageFactory.Load(application.TransportId, reqJson), token).ConfigureAwait(false);
            return await requestCompletedSource.Task.WaitOnRequestCompletion(token).ConfigureAwait(false);
        }
    }
}