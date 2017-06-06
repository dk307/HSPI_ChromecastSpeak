using NullGuard;
using SharpCaster.Channels;
using SharpCaster.Extensions;
using SharpCaster.Models;
using SharpCaster.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCaster
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class ChromeCastClient : IDisposable
    {
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

        public event EventHandler<bool> ConnectedChanged;

        public bool Connected
        {
            get { return connected; }
            set
            {
                var oldConnected = connected;
                connected = value;
                if (connected != oldConnected) ConnectedChanged?.Invoke(this, value);
            }
        }

        public ConnectionChannel ConnectionChannel { get; }
        public Uri DeviceUri { get; }
        public HeartbeatChannel HeartbeatChannel { get; }
        public MediaChannel MediaChannel { get; }

        public ReceiverChannel ReceiverChannel { get; }

        internal ChromecastSocketService ChromecastSocketService { get; set; }

        public async Task Abort(CancellationToken token = default(CancellationToken))
        {
            await DisconnectCore(false, token).ConfigureAwait(false);
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public async Task Disconnect(CancellationToken token)
        {
            await DisconnectCore(true, token).ConfigureAwait(false);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "<ConnectionChannel>k__BackingField")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "<HeartbeatChannel>k__BackingField")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "<MediaChannel>k__BackingField")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "<ReceiverChannel>k__BackingField")]
        public void Dispose()
        {
            if (!disposedValue)
            {
                HeartbeatChannel.Dispose();
                ConnectionChannel.Dispose();
                MediaChannel.Dispose();
                ReceiverChannel.Dispose();

                ChromecastSocketService?.Dispose();
                clientConnectLock.Dispose();
            }

            disposedValue = true;
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

        private const int ChromecastPort = 8009;
        private readonly SemaphoreSlim clientConnectLock = new SemaphoreSlim(1);
        private IList<ChromecastChannel> Channels;
        private bool connected;
        private bool disposedValue = false; // To detect redundant calls
    }
}