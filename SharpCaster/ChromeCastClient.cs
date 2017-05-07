using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SharpCaster.Channels;
using SharpCaster.Extensions;
using SharpCaster.Models;
using SharpCaster.Models.ChromecastStatus;
using SharpCaster.Services;
using System.Threading;

using SharpCaster.Models.MediaStatus;

namespace SharpCaster
{
    public class ChromeCastClient : IDisposable
    {
        public ChromecastStatus ChromecastStatus
        {
            get
            {
                return _chromecastStatus;
            }
            set
            {
                _chromecastStatus = value;
            }
        }

        private ChromecastStatus _chromecastStatus;

        public MediaStatus MediaStatus
        {
            get
            {
                return _mediaStatus;
            }
            set
            {
                _mediaStatus = value;
            }
        }

        private MediaStatus _mediaStatus;

        public bool Connected
        {
            get { return _connected; }
            set
            {
                if (_connected != value) ConnectedChanged?.Invoke(this, EventArgs.Empty);
                _connected = value;
            }
        }

        private bool _connected;

        internal ChromecastSocketService ChromecastSocketService { get; set; }

        public ConnectionChannel ConnectionChannel;
        public MediaChannel MediaChannel;
        public HeartbeatChannel HeartbeatChannel;
        public ReceiverChannel ReceiverChannel;
        private const int ChromecastPort = 8009;
        public string ChromecastApplicationId;

        public event EventHandler ConnectedChanged;

        //public event EventHandler<ChromecastApplication> ApplicationStarted;

        //public event EventHandler<MediaStatus> MediaStatusChanged;

        //public event EventHandler<ChromecastStatus> ChromecastStatusChanged;

        public List<ChromecastChannel> Channels;

        public ChromeCastClient()
        {
            ChromecastSocketService = new ChromecastSocketService();
            Channels = new List<ChromecastChannel>();
            ConnectionChannel = new ConnectionChannel(this);
            Channels.Add(ConnectionChannel);
            HeartbeatChannel = new HeartbeatChannel(this);
            Channels.Add(HeartbeatChannel);
            ReceiverChannel = new ReceiverChannel(this);
            Channels.Add(ReceiverChannel);
            MediaChannel = new MediaChannel(this);
            Channels.Add(MediaChannel);
        }

        public async Task ConnectChromecast(Uri uri, CancellationToken token)
        {
            await ChromecastSocketService.Connect(uri.Host, ChromecastPort, ConnectionChannel,
                HeartbeatChannel, ReadPacket, token).ConfigureAwait(false);
        }

        public async Task Disconnect()
        {
            await ChromecastSocketService.Disconnect();
        }

        private async Task ReadPacket(Stream stream, bool parsed, CancellationToken token)
        {
            try
            {
                IEnumerable<byte> entireMessage;
                if (parsed)
                {
                    var buffer = new byte[stream.Length];
                    await stream.ReadAsync(buffer, 0, buffer.Length, token);
                    entireMessage = buffer;
                }
                else
                {
                    entireMessage = stream.ParseData();
                }

                var entireMessageArray = entireMessage.ToArray();
                var castMessage = entireMessageArray.ToCastMessage();
                if (string.IsNullOrEmpty(castMessage?.Namespace)) return;
                Debug.WriteLine("Received: " + castMessage.GetJsonType());
                ReceivedMessage(castMessage);
            }
            catch (Exception ex)
            {
                // TODO: Catch disconnect - HResult = 0x80072745 -
                // catch this (remote device disconnect) ex = {"An established connection was aborted
                // by the software in your host machine. (Exception from HRESULT: 0x80072745)"}

                // Log these bytes
                Debug.WriteLine(ex);
            }
        }

        private void ReceivedMessage(CastMessage castMessage)
        {
            foreach (var channel in Channels.Where(i => i.Namespace == castMessage.Namespace))
            {
                channel.OnMessageReceived(new ChromecastSSLClientDataReceivedArgs(castMessage));
            }
        }

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    ChromecastSocketService?.Dispose();
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
    }
}