using NullGuard;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Unosquare.Labs.EmbedIO;

namespace Hspi.Web
{
    using static System.FormattableString;

    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class MediaWebServer : IDisposable
    {
        public MediaWebServer(IPAddress ipAddress, ushort port)
        {
            UrlPrefix = Invariant($"http://{ipAddress}:{port}/");
            server = new WebServer(UrlPrefix, RoutingStrategy.Wildcard);
            server.RegisterModule(inMemoryFileSystem);
        }

        public async Task StartListening(CancellationToken token)
        {
            await server.RunAsync(token);
        }

        public void Add(byte[] buffer, DateTimeOffset lastModified, string path, DateTimeOffset expiry)
        {
            inMemoryFileSystem.AddCacheFile(buffer, lastModified, path, expiry);
        }

        public string UrlPrefix { get; }
        public bool IsListening => server.Listener.IsListening;

        private readonly WebServer server;
        private readonly InMemoryFileSystemModule inMemoryFileSystem = new InMemoryFileSystemModule();

        #region IDisposable Support

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (server != null)
                    {
                        server.Dispose();
                    }
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