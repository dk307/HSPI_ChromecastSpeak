using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace Hspi
{
    internal class ChromecastDevice
    {
        public ChromecastDevice(string id, string name, IPAddress deviceIP, ushort? volume)
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