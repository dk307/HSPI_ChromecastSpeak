using SharpCaster.Exceptions;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCaster.Channels
{
    internal abstract class ChromecastChannelWithRequestTracking : ChromecastChannel
    {
        protected ChromecastChannelWithRequestTracking(ChromeCastClient client, string ns) :
            base(client, ns)
        {
        }

        public override void Abort()
        {
            completedListLock.Wait();
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

        protected static async Task WaitOnRequestCompletion(Task requestCompletedTask, CancellationToken token)
        {
            await Task.WhenAny(requestCompletedTask, Task.Delay(-1, token));
            token.ThrowIfCancellationRequested();
        }

        protected async Task<TaskCompletionSource<bool>> AddRequestTracking(int requestId, CancellationToken token)
        {
            await completedListLock.WaitAsync(token).ConfigureAwait(false);
            TaskCompletionSource<bool> requestTracking = new TaskCompletionSource<bool>();
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

        protected bool TryRemoveRequestTracking(int requestId, out TaskCompletionSource<bool> completionSource)
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

        private bool aborted = false;
        private readonly SemaphoreSlim completedListLock = new SemaphoreSlim(1);

        private readonly IDictionary<int, TaskCompletionSource<bool>> completedList =
                        new Dictionary<int, TaskCompletionSource<bool>>();
    }
}