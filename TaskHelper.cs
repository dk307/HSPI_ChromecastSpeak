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

        public static async Task WaitOnRequestCompletion(this Task task, CancellationToken token)
        {
            await Task.WhenAny(task, Task.Delay(-1, token));
            token.ThrowIfCancellationRequested();
        }
    }
}