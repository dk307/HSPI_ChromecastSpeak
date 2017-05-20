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
        public MediaWebServerManager(ILogger logger, CancellationToken shutdownCancellationToken)
        {
            this.logger = logger;
            ShutdownCancellationToken = shutdownCancellationToken;
        }

        public async Task StartupServer(string address, ushort port)
        {
            await webServerLock.WaitAsync(ShutdownCancellationToken).ConfigureAwait(false);
            try
            {
                if (webServer != null)
                {
                    logger.DebugLog("Stopping old webserver if running.");
                }
                // Stop any existing server
                webServerTokenSource?.Cancel();
                webServerTask?.Wait(ShutdownCancellationToken);
                webServerTokenSource?.Dispose();
                combinedWebServerTokenSource?.Dispose();
                webServer?.Dispose();

                webServerTokenSource = new CancellationTokenSource();
                combinedWebServerTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(webServerTokenSource.Token, ShutdownCancellationToken);

                webServer = new MediaWebServer(address, port);
                logger.LogInfo(Invariant($"Starting Web Server on {address}:{port}"));

                webServerTask = webServer.StartListening(combinedWebServerTokenSource.Token);
            }
            finally
            {
                webServerLock.Release();
            }
        }

        public async Task<Uri> Add(byte[] buffer, string extension, TimeSpan? duration)
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
                logger.DebugLog(Invariant($"Adding {path} to Web Server with {buffer.Length} Audio bytes"));

                pathNumber++;
                var expiry = DateTimeOffset.Now.Add(FileEntryExpiry);
                if (duration.HasValue)
                {
                    expiry.Add(duration.Value);
                }
                webServer.Add(buffer, DateTimeOffset.Now, path, expiry);

                UriBuilder builder = new UriBuilder(webServer.UrlPrefix);
                builder.Path = path;
                return builder.Uri;
            }
            finally
            {
                webServerLock.Release();
            }
        }

        public static TimeSpan FileEntryExpiry => TimeSpan.FromSeconds(120);

        private CancellationToken ShutdownCancellationToken { get; }
        private readonly SemaphoreSlim webServerLock = new SemaphoreSlim(1);
        private MediaWebServer webServer;
        private Task webServerTask;
        private CancellationTokenSource webServerTokenSource;
        private CancellationTokenSource combinedWebServerTokenSource;
        private int pathNumber = 0;
        private readonly ILogger logger;

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