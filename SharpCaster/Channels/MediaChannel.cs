using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SharpCaster.Models;
using SharpCaster.Models.Metadata;
using SharpCaster.Models.ChromecastRequests;
using SharpCaster.Models.MediaStatus;
using System.Collections.Concurrent;
using System.Threading;
using System;
using SharpCaster.Models.ChromecastStatus;

namespace SharpCaster.Channels
{
    using SharpCaster.Exceptions;
    using static System.FormattableString;

    internal class MediaChannel : ChromecastChannel
    {
        public MediaChannel(ChromeCastClient client)
            : base(client, "urn:x-cast:com.google.cast.media")
        {
            MessageReceived += OnMessageReceived;
        }

        private void OnMessageReceived(object sender, ChromecastSSLClientDataReceivedArgs chromecastSSLClientDataReceivedArgs)
        {
            var json = chromecastSSLClientDataReceivedArgs.Message.PayloadUtf8;
            var response = JsonConvert.DeserializeObject<MediaStatusResponse>(json);

            if (response.status != null)
            {
                Client.MediaStatus = response.status.FirstOrDefault();
            }

            if (response.requestId != 0)
            {
                if (completedList.TryRemove(response.requestId, out var completed))
                {
                    if (response.status == null)
                    {
                        completed.SetException(new MediaLoadException(Client.DeviceUri.ToString(), response.type));
                    }
                    else
                    {
                        completed.SetResult(true);
                    }
                }
            }
        }

        public async Task GetMediaStatus(string transportId, CancellationToken token)
        {
            int requestId = RequestIdProvider.Next;

            await Write(MessageFactory.MediaStatus(transportId, requestId), token);

            TaskCompletionSource<bool> completed = new TaskCompletionSource<bool>();
            completedList[requestId] = completed;
            await completed.Task;
        }

        public async Task LoadMedia(
            ChromecastApplication application,
            Uri mediaUrl,
            string contentType /*= "application/vnd.ms-sstr+xml"*/,
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
            await Write(MessageFactory.Load(application.TransportId, reqJson), token);

            TaskCompletionSource<bool> completed = new TaskCompletionSource<bool>();
            completedList[requestId] = completed;
            await completed.Task;
        }

        private readonly ConcurrentDictionary<int, TaskCompletionSource<bool>> completedList =
                        new ConcurrentDictionary<int, TaskCompletionSource<bool>>();
    }
}