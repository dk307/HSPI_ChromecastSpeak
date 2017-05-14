using System.Diagnostics;

namespace Hspi
{
    public class HSTraceListener : TraceListener
    {
        public HSTraceListener(ILogger logger)
        {
            this.logger = logger;
        }

        public override void Write(string message)
        {
            logger.DebugLog(message);
        }

        public override void WriteLine(string message)
        {
            logger.DebugLog(message);
        }

        private readonly ILogger logger;
    }
}