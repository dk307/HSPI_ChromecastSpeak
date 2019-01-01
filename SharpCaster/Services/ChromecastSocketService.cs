using Hspi.Utils;
using Nito.AsyncEx;
using NullGuard;
using SharpCaster.Channels;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCaster.Services
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class ChromecastSocketService : IDisposable
    {
        public async Task Connect(string host, int port,
            ConnectionChannel connectionChannel,
            HeartbeatChannel heartbeatChannel,
            Func<Stream, CancellationToken, Task> packetReader,
            CancellationToken token)
        {
            using (var sync = await clientConnectLock.LockAsync(token).ConfigureAwait(false))
            {
                if (client != null)
                {
                    throw new Exception("Already set");
                }
                client = new ChromecastTcpClient();
                combinedStopTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
                CancellationToken combinedToken = combinedStopTokenSource.Token;
                await client.ConnectAsync(host, port, combinedToken).ConfigureAwait(false);

                readTask = TaskHelper.StartAsync(() => ProcessRead(packetReader, combinedToken), combinedToken);

                connectionChannel.OpenConnection(combinedToken);
                heartbeatChannel.StartHeartbeat(combinedToken);
            }
        }

        public async Task Disconnect(CancellationToken token)
        {
            using (var sync = await clientConnectLock.LockAsync(token).ConfigureAwait(false))
            {
                combinedStopTokenSource?.Cancel();
                client?.Disconnect();
                await readTask?.WaitForFinishNoException();
            }
        }

        public async Task Write(byte[] bytes, CancellationToken token)
        {
            using (var sync = await clientWriteLock.LockAsync(token).ConfigureAwait(false))
            {
                await client.Stream.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
            }
        }

        private async Task ProcessRead(Func<Stream, CancellationToken, Task> packetReader, CancellationToken combinedToken)
        {
            try
            {
                using (combinedToken.Register(() => client.Disconnect()))
                {
                    while (!combinedToken.IsCancellationRequested)
                    {
                        var sizeBuffer = new byte[4];

                        // First message should contain the size of message
                        await client.Stream.ReadAsync(sizeBuffer, 0, sizeBuffer.Length, combinedToken).ConfigureAwait(false);
                        // The message is little-endian (that is, little end first), reverse the byte array.
                        Array.Reverse(sizeBuffer);

                        //Retrieve the size of message
                        var messageSize = BitConverter.ToInt32(sizeBuffer, 0);
                        var messageBuffer = new byte[messageSize];
                        await client.Stream.ReadAsync(messageBuffer, 0, messageBuffer.Length, combinedToken).ConfigureAwait(false);
                        using (var answer = new MemoryStream(messageBuffer.Length))
                        {
                            await answer.WriteAsync(messageBuffer, 0, messageBuffer.Length, combinedToken).ConfigureAwait(false);
                            answer.Position = 0;
                            await packetReader(answer, combinedToken).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch
            {
                disconnectedTokenSource.Cancel();
                throw;
            }
        }

        public CancellationToken Disconnected => disconnectedTokenSource.Token;

        private readonly CancellationTokenSource disconnectedTokenSource = new CancellationTokenSource();
        private readonly AsyncLock clientConnectLock = new AsyncLock();
        private readonly AsyncLock clientWriteLock = new AsyncLock();
        private ChromecastTcpClient client;
        private CancellationTokenSource combinedStopTokenSource;
        private Task readTask;

        #region IDisposable Support

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "disconnectedTokenSource")]
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    combinedStopTokenSource?.Cancel();
                    client?.Disconnect();
                    client?.Dispose();
                }

                disposedValue = true;
            }
        }

        private bool disposedValue = false; // To detect redundant calls

        #endregion IDisposable Support
    }
}