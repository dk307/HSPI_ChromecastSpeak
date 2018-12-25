using System;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using Nito.AsyncEx.Synchronous;

namespace Hspi.Utils
{
    internal static class TaskHelper
    {
        public static async Task WaitForFinishNoException(this Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch
            {
            }
        }

        public static Task StartAsync(Action action, CancellationToken token)
        {
            return Task.Factory.StartNew(action, token,
                                         TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                                         TaskScheduler.Current);
        }

        public static Task StartAsync(Func<Task> taskAction, CancellationToken token)
        {
            var task = Task.Factory.StartNew(() => taskAction(), token,
                                          TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                                          TaskScheduler.Current).WaitAndUnwrapException(token);
            return task;
        }

        public static async Task<TResult> WaitOnRequestCompletion<TResult>(this Task<TResult> task, CancellationToken token)
        {
            var finishedTask = await Task.WhenAny(task, Task.Delay(-1, token)).ConfigureAwait(false);

            if (finishedTask == task)
            {
                return task.Result;
            }
            else
            {
                throw new TaskCanceledException();
            }
        }
    }
}