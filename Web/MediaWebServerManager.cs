using System;
using System.Threading;
using System.Threading.Tasks;
using NullGuard;
using Hspi.Exceptions;

namespace Hspi.Web
{
    using static System.FormattableString;

    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class MediaWebServerManager : IDisposable
    {
        public MediaWebServerManager(CancellationToken shutdownCancellationToken)
        {
            ShutdownCancellationToken = shutdownCancellationToken;
        }

        public async Task StartupServer(string address, int port)
        {
            await webServerLock.WaitAsync(ShutdownCancellationToken).ConfigureAwait(false);
            try
            {
                // Stop any existing server
                webServerTokenSource?.Cancel();
                webServerTask?.Wait(ShutdownCancellationToken);
                webServerTokenSource?.Dispose();
                combinedWebServerTokenSource?.Dispose();
                webServer?.Dispose();

                webServerTokenSource = new CancellationTokenSource();
                combinedWebServerTokenSource = CancellationTokenSource.CreateLinkedTokenSource(webServerTokenSource.Token, ShutdownCancellationToken);

                webServer = new MediaWebServer(address, port);
                webServerTask = webServer.StartListening(combinedWebServerTokenSource.Token);
                Port = port;
            }
            finally
            {
                webServerLock.Release();
            }
        }

        public async Task<string> Add(byte[] buffer, string extension)
        {
            // This lock allows us to wait till server has started
            await webServerLock.WaitAsync(ShutdownCancellationToken);
            try
            {
                if ((webServer == null) || (!webServer.IsListening))
                {
                    throw new HspiException("Server is not running.");
                }
                string path = Invariant($"path{pathNumber}.{extension}");
                pathNumber++;
                webServer.Add(buffer, DateTimeOffset.Now, path, DateTimeOffset.Now.Add(FileEntryExpiry));
                return extension;
            }
            finally
            {
                webServerLock.Release();
            }
        }

        public int Port { get; private set; }
        public static TimeSpan FileEntryExpiry => TimeSpan.FromSeconds(120);

        private CancellationToken ShutdownCancellationToken { get; }
        private readonly SemaphoreSlim webServerLock = new SemaphoreSlim(1);
        private MediaWebServer webServer;
        private Task webServerTask;
        private CancellationTokenSource webServerTokenSource;
        private CancellationTokenSource combinedWebServerTokenSource;
        private int pathNumber = 0;

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    webServerTokenSource?.Dispose();
                    combinedWebServerTokenSource?.Dispose();
                    webServer?.Dispose();
                    webServerLock.Dispose();
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