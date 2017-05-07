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
    public class MediaChannel : ChromecastChannel
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
                        completed.SetException(new Exception("Request Failed"));
                    }
                    else
                    {
                        completed.SetResult(true);
                    }
                }
            }
        }

        //public async Task GetMediaStatus()
        //{
        //    await Write(MessageFactory.MediaStatus(Client.CurrentApplicationTransportId));
        //}

        public async Task LoadMedia(
            ChromecastApplication application,
            string mediaUrl,
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
            var mediaObject = new MediaData(mediaUrl, contentType, metadata, streamType, duration, customData, tracks);
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