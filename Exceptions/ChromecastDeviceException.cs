using System;
using System.Runtime.Serialization;

namespace Hspi.Chromecast
{
    [Serializable]
    internal class ChromecastDeviceException : Exception
    {
        private string name;
        private string v;

        public ChromecastDeviceException()
        {
        }

        public ChromecastDeviceException(string message) : base(message)
        {
        }

        public ChromecastDeviceException(string name, string v)
        {
            this.name = name;
            this.v = v;
        }

        public ChromecastDeviceException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected ChromecastDeviceException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}