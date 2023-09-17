using System;
using System.Runtime.Serialization;

namespace Hspi.Chromecast
{
    [Serializable]
    internal class MediaLoadException : Exception
    {
        private string v1;
        private string v2;

        public MediaLoadException()
        {
        }

        public MediaLoadException(string message) : base(message)
        {
        }

        public MediaLoadException(string v1, string v2)
        {
            this.v1 = v1;
            this.v2 = v2;
        }

        public MediaLoadException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected MediaLoadException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}