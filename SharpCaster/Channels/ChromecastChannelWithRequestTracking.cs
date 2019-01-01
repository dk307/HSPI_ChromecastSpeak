using Nito.AsyncEx;
using SharpCaster.Exceptions;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCaster.Channels
{
    internal abstract class ChromecastChannelWithRequestTracking<T> : ChromecastChannel
    {
        protected ChromecastChannelWithRequestTracking(ChromeCastClient client, string ns) :
            base(client, ns)
        {
        }

        public override Task Abort()
        {
            return AbortAsync();
        }

        private async Task AbortAsync()
        {
            using (var sync = await completedListLock.LockAsync().ConfigureAwait(false))
            {
                aborted = true;
                foreach (var pair in completedList)
                {
                    pair.Value.SetException(new ChromecastDeviceException(Client.DeviceUri.ToString(), "Device got disconnected."));
                }
                completedList.Clear();
            }
        }

        protected async Task<TaskCompletionSource<T>> AddRequestTracking(int requestId, CancellationToken token)
        {
            using (var sync = await completedListLock.LockAsync(token).ConfigureAwait(false))
            {
                TaskCompletionSource<T> requestTracking = new TaskCompletionSource<T>();
                {
                    if (aborted)
                    {
                        throw new ChromecastDeviceException(Client.DeviceUri.ToString(), "Device got disconnected.");
                    }

                    completedList[requestId] = requestTracking;
                    return requestTracking;
                }
            }
        }

        protected bool TryRemoveRequestTracking(int requestId, out TaskCompletionSource<T> completionSource)
        {
            completionSource = null;
            using (var sync = completedListLock.Lock())
            {
                if (completedList.TryGetValue(requestId, out completionSource))
                {
                    completedList.Remove(requestId);
                    return true;
                }
                return false;
            }
        }

        private volatile bool aborted = false;
        private readonly AsyncLock completedListLock = new AsyncLock();

        private readonly IDictionary<int, TaskCompletionSource<T>> completedList =
                        new Dictionary<int, TaskCompletionSource<T>>();
    }
}