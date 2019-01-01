using Hspi.Utils;
using Nito.AsyncEx;
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
                if (connected != oldConnected)
                {
                    ConnectedChanged?.Invoke(this, value);
                }
            }
        }

        public ConnectionChannel ConnectionChannel { get; }
        public Uri DeviceUri { get; }
        public HeartbeatChannel HeartbeatChannel { get; }
        public MediaChannel MediaChannel { get; }
        public ReceiverChannel ReceiverChannel { get; }
        internal ChromecastSocketService ChromecastSocketService { get; set; }

        public async Task ConnectChromecast(CancellationToken token)
        {
            using (var sync = await clientConnectLock.LockAsync(token).ConfigureAwait(false))
            {
                ChromecastSocketService.Disconnected.Register(async () => await ConnectionDroped(false));
                ConnectionChannel.CloseReceived.Register(async () => await ConnectionDroped(true));

                await ChromecastSocketService.Connect(DeviceUri.Host, ChromecastPort, ConnectionChannel,
                    HeartbeatChannel, ReadPacket, token).ConfigureAwait(false);
            }
        }

        public async Task Disconnect(CancellationToken token)
        {
            await DisconnectCore(true, true, token).ConfigureAwait(false);
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
            }

            disposedValue = true;
        }

        private async Task ConnectionDroped(bool disconnectSocket)
        {
            await DisconnectCore(false, disconnectSocket, CancellationToken.None).ConfigureAwait(false);
        }

        private async Task DisconnectCore(bool sendClose, bool disconnectSocket, CancellationToken token)
        {
            if (connected)
            {
                Connected = false;
                using (var sync = await clientConnectLock.LockAsync(token).ConfigureAwait(false))
                {
                    if (sendClose)
                    {
                        await ConnectionChannel.CloseConnection(token).ConfigureAwait(false);
                    }
                    if (disconnectSocket)
                    {
                        await ChromecastSocketService.Disconnect(token).ConfigureAwait(false);
                    }

                    var abortTasks = new List<Task>();

                    foreach (var channel in Channels)
                    {
                        abortTasks.Add(channel.Abort().WaitForFinishNoException());
                    }

                    await Task.WhenAll(abortTasks.ToArray()).ConfigureAwait(false);
                }
            }
        }

        private async Task ReadPacket(Stream stream, CancellationToken token)
        {
            try
            {
                var buffer = new byte[stream.Length];
                await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);

                var castMessage = buffer.ToCastMessage();
                if (string.IsNullOrWhiteSpace(castMessage?.Namespace))
                {
                    return;
                }
                Trace.WriteLine("Received: " + castMessage.GetJsonType());
                ReceivedMessage(castMessage);
            }
            catch (Exception ex)
            {
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
        private readonly AsyncLock clientConnectLock = new AsyncLock();
        private IList<ChromecastChannel> Channels;
        private volatile bool connected = false;
        private bool disposedValue = false; // To detect redundant calls
    }
}