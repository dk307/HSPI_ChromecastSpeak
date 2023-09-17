using System;
using System.Runtime.Serialization;
using Hspi.Exceptions;

namespace Hspi.Chromecast
{
    [Serializable]
    internal class ChromecastException : HspiException
    {
        public ChromecastException()
        {
        }

        public ChromecastException(string message) : base(message)
        {
        }

        public ChromecastException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected ChromecastException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}