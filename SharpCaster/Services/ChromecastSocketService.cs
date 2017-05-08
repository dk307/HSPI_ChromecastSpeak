using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCaster.Channels;
using System.Net.Sockets;
using NullGuard;

namespace SharpCaster.Services
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class ChromecastSocketService : IDisposable
    {
        public async Task Connect(string host, int port,
            ConnectionChannel connectionChannel,
            HeartbeatChannel heartbeatChannel,
            Func<Stream, bool, CancellationToken, Task> packetReader,
            CancellationToken token)
        {
            await Task.Delay(0);
            if (client != null)
            {
                throw new Exception("Already set");
            }
            client = new ChromecastTcpClient();
            stopTokenSource = new CancellationTokenSource();
            combinedStopTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stopTokenSource.Token, token);
            CancellationToken combinedToken = combinedStopTokenSource.Token;
            await client.ConnectAsync(host, port, combinedToken);

            connectionChannel.OpenConnection(combinedStopTokenSource.Token);
            heartbeatChannel.StartHeartbeat(combinedStopTokenSource.Token);

            runTask = Task.Factory.StartNew(async () =>
            {
                while (!combinedToken.IsCancellationRequested)
                {
                    var sizeBuffer = new byte[4];
                    byte[] messageBuffer = { };
                    // First message should contain the size of message
                    await client.Stream.ReadAsync(sizeBuffer, 0, sizeBuffer.Length, combinedToken).ConfigureAwait(false);
                    // The message is little-endian (that is, little end first),
                    // reverse the byte array.
                    Array.Reverse(sizeBuffer);
                    //Retrieve the size of message
                    var messageSize = BitConverter.ToInt32(sizeBuffer, 0);
                    messageBuffer = new byte[messageSize];
                    await client.Stream.ReadAsync(messageBuffer, 0, messageBuffer.Length, combinedToken).ConfigureAwait(false);
                    using (var answer = new MemoryStream(messageBuffer.Length))
                    {
                        await answer.WriteAsync(messageBuffer, 0, messageBuffer.Length, combinedToken).ConfigureAwait(false);
                        answer.Position = 0;
                        await packetReader(answer, true, combinedToken);
                    }
                }
            }, combinedToken);
        }

        public async Task Disconnect()
        {
            if (client != null)
            {
                stopTokenSource.Cancel();
                await client.DisconnectAsync();
                runTask.Wait();
                client = null;
            }
        }

        public async Task Write(byte[] bytes, CancellationToken token)
        {
            await clientWriteLock.WaitAsync(token);
            try
            {
                await client.Stream.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
            }
            finally
            {
                clientWriteLock.Release();
            }
        }

        private ChromecastTcpClient client;
        private Task runTask;
        private CancellationTokenSource stopTokenSource;
        private CancellationTokenSource combinedStopTokenSource;
        private readonly SemaphoreSlim clientWriteLock = new SemaphoreSlim(1);

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    stopTokenSource?.Dispose();
                    combinedStopTokenSource?.Dispose();
                    runTask?.Dispose();
                    client?.Dispose();
                    clientWriteLock.Dispose();
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