using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Hspi
{
    internal static class TaskHelper
    {
        public static async Task WaitForFinishNoCancelException(this Task task)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
            }
        }

        public static async Task<TResult> WaitOnRequestCompletion<TResult>(this Task<TResult> task, CancellationToken token)
        {
            Task finishedTask = await Task.WhenAny(task, Task.Delay(-1, token));

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