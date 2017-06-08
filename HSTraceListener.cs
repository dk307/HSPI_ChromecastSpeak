using System;
using System.Diagnostics;

namespace Hspi
{
    internal class HSTraceListener : TraceListener
    {
        public HSTraceListener(ILogger logger)
        {
            loggerWeakReference = new WeakReference<ILogger>(logger);
        }

        public override void Write(string message)
        {
            Log(message);
        }

        public override void WriteLine(string message)
        {
            Log(message);
        }

        private void Log(string message)
        {
            if (loggerWeakReference.TryGetTarget(out var logger))
            {
                logger.LogDebug(message);
            }
        }

        private readonly WeakReference<ILogger> loggerWeakReference;
    }
}