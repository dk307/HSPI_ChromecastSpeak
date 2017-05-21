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
            await completedListLock.WaitAsync();
            aborted = true;
            try
            {
                foreach (var pair in completedList)
                {
                    pair.Value.SetException(new ChromecastDeviceException(Client.DeviceUri.ToString(), "Device got disconnected."));
                }
                completedList.Clear();
            }
            finally
            {
                completedListLock.Release();
            }
        }

        protected async Task<TaskCompletionSource<T>> AddRequestTracking(int requestId, CancellationToken token)
        {
            await completedListLock.WaitAsync(token).ConfigureAwait(false);
            TaskCompletionSource<T> requestTracking = new TaskCompletionSource<T>();
            try
            {
                if (aborted)
                {
                    throw new ChromecastDeviceException(Client.DeviceUri.ToString(), "Device got disconnected.");
                }

                completedList[requestId] = requestTracking;
                return requestTracking;
            }
            finally
            {
                completedListLock.Release();
            }
        }

        protected bool TryRemoveRequestTracking(int requestId, out TaskCompletionSource<T> completionSource)
        {
            completionSource = null;
            completedListLock.Wait();
            try
            {
                if (completedList.TryGetValue(requestId, out completionSource))
                {
                    completedList.Remove(requestId);
                    return true;
                }
                return false;
            }
            finally
            {
                completedListLock.Release();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                completedListLock.Dispose();
            }

            base.Dispose(disposing);
        }

        private bool aborted = false;
        private readonly SemaphoreSlim completedListLock = new SemaphoreSlim(1);

        private readonly IDictionary<int, TaskCompletionSource<T>> completedList =
                        new Dictionary<int, TaskCompletionSource<T>>();
    }
}