using Hspi.Exceptions;
using Hspi.Utils;
using NullGuard;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx.Synchronous;
using static System.FormattableString;
using Nito.AsyncEx;

namespace Hspi.Web
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class MediaWebServerManager : IDisposable
    {
        public MediaWebServerManager(CancellationToken shutdownCancellationToken)
        {
            this.shutdownCancellationToken = shutdownCancellationToken;
        }

        public static TimeSpan FileEntryExpiry => TimeSpan.FromSeconds(120);

        public async Task<Uri> Add(byte[] buffer, string extension, TimeSpan? duration)
        {
            // This lock allows us to wait till server has started
            using (var sync = await webServerLock.LockAsync(shutdownCancellationToken).ConfigureAwait(false))
            {
                if ((webServer == null) || (!webServer.IsListening))
                {
                    throw new HspiException("Server is not running.");
                }
                string path = Invariant($"path{pathNumber}.{extension}");
                Trace.WriteLine(Invariant($"Adding {path} to Web Server with {buffer.Length} Audio bytes"));

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
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            if (!disposedValue)
            {
                combinedWebServerTokenSource.Cancel();
                webServer?.Dispose();
                combinedWebServerTokenSource?.Dispose();

                disposedValue = true;
            }
        }

        public async Task StartupServer(IPAddress address, ushort port)
        {
            using (var sync = await webServerLock.LockAsync(shutdownCancellationToken).ConfigureAwait(false))
            {
                StopOldServer();

                combinedWebServerTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(shutdownCancellationToken);

                webServer = new MediaWebServer(address, port);
                Trace.TraceInformation(Invariant($"Starting Web Server on {address}:{port}"));

                webServerTask = webServer.StartListening(combinedWebServerTokenSource.Token)
                                         .ContinueWith((x) => WebServerFinished(x), combinedWebServerTokenSource.Token);
            }
        }

        private void StopOldServer()
        {
            try
            {
                if (webServer != null)
                {
                    Trace.WriteLine("Stopping old webserver if running.");
                }
                // Stop any existing server
                combinedWebServerTokenSource?.Cancel();
                webServerTask?.WaitWithoutException();
                combinedWebServerTokenSource?.Dispose();
                webServer?.Dispose();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(Invariant($"Exception in stopping old Server {ExceptionHelper.GetFullMessage(ex)}"));
            }
        }

        private static void WebServerFinished(Task task)
        {
            if (task.IsFaulted)
            {
                Trace.TraceWarning(Invariant($"WebServer Stopped with error {ExceptionHelper.GetFullMessage(task.Exception)}"));
            }
            else
            {
                Trace.WriteLine("Web Server Stopped.");
            }
        }

        private readonly AsyncLock webServerLock = new AsyncLock();
        private CancellationTokenSource combinedWebServerTokenSource;
        private bool disposedValue = false;
        private int pathNumber = 0;
        private CancellationToken shutdownCancellationToken;
        private MediaWebServer webServer;
        private Task webServerTask;
    }
}