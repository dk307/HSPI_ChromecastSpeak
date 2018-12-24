using Hspi.Exceptions;
using Hspi.Utils;
using NullGuard;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

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
            await webServerLock.WaitAsync(shutdownCancellationToken).ConfigureAwait(false);
            try
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
            finally
            {
                webServerLock.Release();
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            if (!disposedValue)
            {
                webServerTokenSource?.Dispose();
                combinedWebServerTokenSource?.Dispose();
                webServer?.Dispose();
                webServerLock.Dispose();

                disposedValue = true;
            }
        }

        public async Task StartupServer(IPAddress address, ushort port)
        {
            await webServerLock.WaitAsync(shutdownCancellationToken).ConfigureAwait(false);
            try
            {
                await StopOldServer().ConfigureAwait(false);

                webServerTokenSource = new CancellationTokenSource();
                combinedWebServerTokenSource =
                    CancellationTokenSource.CreateLinkedTokenSource(webServerTokenSource.Token, shutdownCancellationToken);

                webServer = new MediaWebServer(address, port);
                Trace.TraceInformation(Invariant($"Starting Web Server on {address}:{port}"));

                webServerTask = webServer.StartListening(combinedWebServerTokenSource.Token)
                                         .ContinueWith((x) => WebServerFinished(x), combinedWebServerTokenSource.Token);
            }
            finally
            {
                webServerLock.Release();
            }
        }

        private async Task StopOldServer()
        {
            try
            {
                if (webServer != null)
                {
                    Trace.WriteLine("Stopping old webserver if running.");
                }
                // Stop any existing server
                webServerTokenSource?.Cancel();
                if (webServerTask != null)
                {
                    try
                    {
                        await webServerTask.WaitForFinishNoCancelException().ConfigureAwait(false);
                    }
                    catch { }
                }
                webServerTokenSource?.Dispose();
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

        private readonly SemaphoreSlim webServerLock = new SemaphoreSlim(1);
        private CancellationTokenSource combinedWebServerTokenSource;
        private bool disposedValue = false;
        private int pathNumber = 0;
        private CancellationToken shutdownCancellationToken;
        private MediaWebServer webServer;
        private Task webServerTask;
        private CancellationTokenSource webServerTokenSource;
    }
}