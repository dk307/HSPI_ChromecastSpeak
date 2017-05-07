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
    internal class ReceiverChannel : ChromecastChannel
    {
        public ReceiverChannel(ChromeCastClient client) :
            base(client, "urn:x-cast:com.google.cast.receiver")
        {
            MessageReceived += ReceiverChannel_MessageReceived;
        }

        private void ReceiverChannel_MessageReceived(object sender, ChromecastSSLClientDataReceivedArgs e)
        {
            var json = e.Message.PayloadUtf8;
            var response = JsonConvert.DeserializeObject<ChromecastStatusResponse>(json);
            if (response.ChromecastStatus != null)
            {
                Client.ChromecastStatus = response.ChromecastStatus;
            }
            if (response.requestId != 0)
            {
                if (completedList.TryRemove(response.requestId, out var completed))
                {
                    completed.SetResult(true);
                }
            }
        }

        public async Task LaunchApplication(string applicationId, CancellationToken token)
        {
            int requestId = RequestIdProvider.Next;
            var message = MessageFactory.Launch(applicationId, requestId);
            await Write(message, token);

            TaskCompletionSource<bool> completed = new TaskCompletionSource<bool>();
            completedList[requestId] = completed;
            await completed.Task;
        }

        public async Task GetChromecastStatus(CancellationToken token)
        {
            int requestId = RequestIdProvider.Next;
            await Write(MessageFactory.Status(requestId), token);
            TaskCompletionSource<bool> completed = new TaskCompletionSource<bool>();
            completedList[requestId] = completed;
            await completed.Task;
        }

        private readonly ConcurrentDictionary<int, TaskCompletionSource<bool>> completedList =
                                new ConcurrentDictionary<int, TaskCompletionSource<bool>>();
    }
}