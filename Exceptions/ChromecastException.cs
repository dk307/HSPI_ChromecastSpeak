using System;
using System.Runtime.Serialization;

namespace Hspi.Exceptions
{
    [Serializable]
    internal class ChromecastException : HspiException
    {
        public ChromecastException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public ChromecastException()
        {
        }

        protected ChromecastException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}