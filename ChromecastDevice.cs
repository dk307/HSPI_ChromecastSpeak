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
        public ChromecastDevice(string id, string name, IPAddress deviceIP, double? volume = null)
        {
            Volume = 0.2D;
            Name = name;
            Id = id;
            DeviceIP = deviceIP;
        }

        public string Id { get; }
        public string Name { get; }
        public IPAddress DeviceIP { get; }
        public double? Volume { get; }
    }
}