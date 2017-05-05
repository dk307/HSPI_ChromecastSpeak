using HomeSeerAPI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

namespace Hspi
{
    using static System.FormattableString;

    /// <summary>
    /// Class to store PlugIn Configuration
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    internal class PluginConfig : IDisposable
    {
        public event EventHandler<EventArgs> ConfigChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="PluginConfig"/> class.
        /// </summary>
        /// <param name="HS">The homeseer application.</param>
        public PluginConfig(IHSApplication HS)
        {
            this.HS = HS;

            debugLogging = GetValue(DebugLoggingKey, false);
            forwardSpeech = GetValue(ForwardSpeechKey, true);
            webServerPort = GetValue(WebServerPortKey, 8081);
            string deviceIdsConcatString = GetValue(DeviceIds, string.Empty);
            var deviceIds = deviceIdsConcatString.Split(DeviceIdsSeparator);

            foreach (var deviceId in deviceIds)
            {
                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    continue;
                }
                string ipAddressString = GetValue(IPAddressKey, string.Empty, deviceId);
                IPAddress.TryParse(ipAddressString, out var deviceIP);

                string name = GetValue(NameKey, string.Empty, deviceId);
                devices.Add(deviceId, new ChromecastDevice(deviceId, name, deviceIP));
            }
        }

        /// <summary>
        /// Gets or sets the devices
        /// </summary>
        /// <value>
        /// The API key.
        /// </value>
        public IReadOnlyDictionary<string, ChromecastDevice> Devices
        {
            get
            {
                configLock.EnterReadLock();
                try
                {
                    return new ReadOnlyDictionary<string, ChromecastDevice>(devices);
                }
                finally
                {
                    configLock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether debug logging is enabled.
        /// </summary>
        /// <value>
        ///   <c>true</c> if [debug logging]; otherwise, <c>false</c>.
        /// </value>
        public bool DebugLogging
        {
            get
            {
                configLock.EnterReadLock();
                try
                {
                    return debugLogging;
                }
                finally
                {
                    configLock.ExitReadLock();
                }
            }

            set
            {
                configLock.EnterWriteLock();
                try
                {
                    SetValue(DebugLoggingKey, value);
                    debugLogging = value;
                }
                finally
                {
                    configLock.ExitWriteLock();
                }
            }
        }

        public bool ForwardSpeach
        {
            get
            {
                configLock.EnterReadLock();
                try
                {
                    return forwardSpeech;
                }
                finally
                {
                    configLock.ExitReadLock();
                }
            }

            set
            {
                configLock.EnterWriteLock();
                try
                {
                    SetValue(ForwardSpeechKey, value);
                    forwardSpeech = value;
                }
                finally
                {
                    configLock.ExitWriteLock();
                }
            }
        }

        public int WebServerPort
        {
            get
            {
                configLock.EnterReadLock();
                try
                {
                    return webServerPort;
                }
                finally
                {
                    configLock.ExitReadLock();
                }
            }

            set
            {
                configLock.EnterWriteLock();
                try
                {
                    SetValue(WebServerPortKey, value);
                    webServerPort = value;
                }
                finally
                {
                    configLock.ExitWriteLock();
                }
            }
        }

        public void AddDevice(ChromecastDevice device)
        {
            configLock.EnterWriteLock();
            try
            {
                devices[device.Id] = device;
                SetValue(NameKey, device.Name, device.Id);
                SetValue(IPAddressKey, device.DeviceIP.ToString(), device.Id);
                SetValue(DeviceIds, devices.Keys.Aggregate((x, y) => x + DeviceIdsSeparator + y));
            }
            finally
            {
                configLock.ExitWriteLock();
            }
        }

        public void RemoveDevice(string deviceId)
        {
            configLock.EnterWriteLock();
            try
            {
                devices.Remove(deviceId);
                if (devices.Count > 0)
                {
                    SetValue(DeviceIds, devices.Keys.Aggregate((x, y) => x + DeviceIdsSeparator + y));
                }
                else
                {
                    SetValue(DeviceIds, string.Empty);
                }
                HS.ClearINISection(deviceId, FileName);
            }
            finally
            {
                configLock.ExitWriteLock();
            }
        }

        private T GetValue<T>(string key, T defaultValue)
        {
            return GetValue(key, defaultValue, DefaultSection);
        }

        private T GetValue<T>(string key, T defaultValue, string section)
        {
            string stringValue = HS.GetINISetting(section, key, null, FileName);

            if (stringValue != null)
            {
                try
                {
                    T result = (T)System.Convert.ChangeType(stringValue, typeof(T), CultureInfo.InvariantCulture);
                    return result;
                }
                catch (Exception)
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        private void SetValue<T>(string key, T value)
        {
            SetValue<T>(key, value, DefaultSection);
        }

        private void SetValue<T>(string key, T value, string section)
        {
            string stringValue = System.Convert.ToString(value, CultureInfo.InvariantCulture);
            HS.SaveINISetting(section, key, stringValue, FileName);
        }

        /// <summary>
        /// Fires event that configuration changed.
        /// </summary>
        public void FireConfigChanged()
        {
            if (ConfigChanged != null)
            {
                var ConfigChangedCopy = ConfigChanged;
                ConfigChangedCopy(this, EventArgs.Empty);
            }
        }

        #region IDisposable Support

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    configLock.Dispose();
                }
                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
        }

        #endregion IDisposable Support

        private const string NameKey = "Name";
        private const string DeviceIds = "DevicesIds";
        private const string DebugLoggingKey = "DebugLogging";
        private const string ForwardSpeechKey = "FowardSpeech";
        private const string WebServerPortKey = "WebServerPort";
        private readonly static string FileName = Invariant($"{Path.GetFileName(System.Reflection.Assembly.GetEntryAssembly().Location)}.ini");
        private const string IPAddressKey = "IPAddress";
        private const string DefaultSection = "Settings";
        private const char DeviceIdsSeparator = '|';
        private const char PortsEnabledSeparator = ',';

        private readonly Dictionary<string, ChromecastDevice> devices = new Dictionary<string, ChromecastDevice>();
        private readonly IHSApplication HS;
        private int webServerPort;
        private bool debugLogging;
        private bool forwardSpeech;
        private bool disposedValue = false;
        private readonly ReaderWriterLockSlim configLock = new ReaderWriterLockSlim();
    };
}