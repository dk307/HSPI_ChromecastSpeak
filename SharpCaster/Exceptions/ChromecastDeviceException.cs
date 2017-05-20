using System;
using System.Runtime.Serialization;

namespace SharpCaster.Exceptions
{
    [Serializable]
    internal class ChromecastDeviceException : ChromecastException
    {
        public string DeviceName { get; }

        public ChromecastDeviceException(string deviceName)
        {
            DeviceName = deviceName;
        }

        public ChromecastDeviceException(string deviceName, string message) :
            base(message)
        {
            DeviceName = deviceName;
        }

        protected ChromecastDeviceException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            this.DeviceName = info.GetString(DeviceNameKey);
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            info.AddValue(DeviceNameKey, this.DeviceName);
            base.GetObjectData(info, context);
        }

        private const string DeviceNameKey = "DeviceName";
    }
}