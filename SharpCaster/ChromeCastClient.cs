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
using NullGuard;

namespace SharpCaster
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class ChromeCastClient : IDisposable
    {
        public bool Connected
        {
            get { return _connected; }
            set
            {
                var oldConnected = _connected;
                _connected = value;
                if (_connected != oldConnected) ConnectedChanged?.Invoke(this, value);
            }
        }

        private bool _connected;

        internal ChromecastSocketService ChromecastSocketService { get; set; }

        public ChromecastStatus ChromecastStatus { get; set; }
        public MediaStatus MediaStatus { get; set; }
        public ConnectionChannel ConnectionChannel { get; }
        public MediaChannel MediaChannel { get; }
        public HeartbeatChannel HeartbeatChannel { get; }
        public ReceiverChannel ReceiverChannel { get; }
        public Uri DeviceUri { get; }

        public event EventHandler<bool> ConnectedChanged;

        private const int ChromecastPort = 8009;
        private IList<ChromecastChannel> Channels;

        public ChromeCastClient(Uri uri)
        {
            DeviceUri = uri;
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

        public async Task ConnectChromecast(CancellationToken token)
        {
            await clientConnectLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await ChromecastSocketService.Connect(DeviceUri.Host, ChromecastPort, ConnectionChannel,
                    HeartbeatChannel, ReadPacket, token).ConfigureAwait(false);
            }
            finally
            {
                clientConnectLock.Release();
            }
        }

        public async Task Disconnect(CancellationToken token)
        {
            await DisconnectCore(true, token).ConfigureAwait(false);
        }

        public async Task Abort(CancellationToken token = default(CancellationToken))
        {
            await DisconnectCore(false, token).ConfigureAwait(false);
        }

        private async Task DisconnectCore(bool sendClose, CancellationToken token)
        {
            await clientConnectLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                List<Task> abortTasks = new List<Task>();
                foreach (var channel in Channels)
                {
                    abortTasks.Add(channel.Abort());
                }

                await Task.WhenAll(abortTasks.ToArray()).ConfigureAwait(false);

                if (sendClose)
                {
                    await ConnectionChannel.CloseConnection(token).ConfigureAwait(false);
                }
                await ChromecastSocketService.Disconnect(token).ConfigureAwait(false);
            }
            finally
            {
                clientConnectLock.Release();
            }
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
                    var sizeBuffer = new byte[4];
                    byte[] messageBuffer = { };
                    // First message should contain the size of message
                    await stream.ReadAsync(sizeBuffer, 0, sizeBuffer.Length, token);
                    // The message is little-endian (that is, little end first),
                    // reverse the byte array.
                    Array.Reverse(sizeBuffer);
                    //Retrieve the size of message
                    var messageSize = BitConverter.ToInt32(sizeBuffer, 0);
                    messageBuffer = new byte[messageSize];
                    await stream.ReadAsync(messageBuffer, 0, messageBuffer.Length, token);
                    entireMessage = messageBuffer;
                }

                var entireMessageArray = entireMessage.ToArray();
                var castMessage = entireMessageArray.ToCastMessage();
                if (string.IsNullOrEmpty(castMessage?.Namespace)) return;
                Trace.WriteLine("Received: " + castMessage.GetJsonType());
                ReceivedMessage(castMessage);
            }
            catch (Exception ex)
            {
                // TODO: Catch disconnect - HResult = 0x80072745 -
                // catch this (remote device disconnect) ex = {"An established connection was aborted
                // by the software in your host machine. (Exception from HRESULT: 0x80072745)"}

                // Log these bytes
                Trace.WriteLine(ex);
            }
        }

        private void ReceivedMessage(CastMessage castMessage)
        {
            foreach (var channel in Channels.Where(i => i.Namespace == castMessage.Namespace))
            {
                channel.OnMessageReceived(castMessage);
            }
        }

        private readonly SemaphoreSlim clientConnectLock = new SemaphoreSlim(1);

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    ChromecastSocketService?.Dispose();
                    clientConnectLock.Dispose();
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion IDisposable Support
    }
}