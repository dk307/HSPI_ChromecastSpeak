using System;
using System.Runtime.Serialization;

namespace SharpCaster.Exceptions
{
    [Serializable]
    internal class MediaLoadException : ChromecastDeviceException
    {
        public string FailureType { get; }

        public MediaLoadException(string deviceName, string failureType) :
            base(deviceName)
        {
            FailureType = failureType;
        }

        protected MediaLoadException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            this.FailureType = info.GetString(FailureTypeKey);
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            info.AddValue(FailureTypeKey, this.FailureType);
            base.GetObjectData(info, context);
        }

        private const string FailureTypeKey = "FailureType";
    }
}