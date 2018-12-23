using NullGuard;
using System;
using System.Net;

namespace Hspi
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class ChromecastDevice
    {
        public ChromecastDevice(string id, string name, IPAddress deviceIP, [AllowNull]ushort? volume)
        {
            if ((volume.HasValue) && (volume > 100))
            {
                throw new ArgumentOutOfRangeException(nameof(volume));
            }
            Volume = volume;
            Name = name;
            Id = id;
            DeviceIP = deviceIP;
        }

        public string Id { get; }
        public string Name { get; }
        public IPAddress DeviceIP { get; }
        public ushort? Volume { get; }
    }
}