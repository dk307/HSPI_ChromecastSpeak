using NullGuard;
using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace SharpCaster.Services
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class ChromecastTcpClient : IDisposable
    {
        public ChromecastTcpClient()
        {
            tcpClient.NoDelay = true;
        }

        public async Task ConnectAsync(string address, int port, CancellationToken cancellationToken)
        {
            var connectTask = tcpClient.ConnectAsync(address, port);

            // set up cancellation trigger
            var ret = new TaskCompletionSource<bool>();
            var canceller = cancellationToken.Register(() => ret.SetCanceled());

            // if cancellation comes before connect completes, we honour it
            var okOrCancelled = await Task.WhenAny(connectTask, ret.Task).ConfigureAwait(false);

            if (okOrCancelled == ret.Task)
            {
#pragma warning disable CS4014
                // ensure we observe the connectTask's exception in case downstream consumers throw on unobserved tasks
                connectTask.ContinueWith(t => $"{t.Exception}", TaskContinuationOptions.OnlyOnFaulted);
#pragma warning restore CS4014

                // reset the backing field.
                // depending on the state of the socket this may throw ODE which it is appropriate to ignore
                try { Disconnect(); } catch (ObjectDisposedException) { }

                // notify that we did cancel
                cancellationToken.ThrowIfCancellationRequested();
            }
            else
                canceller.Dispose();

            if (okOrCancelled.IsFaulted)
                throw okOrCancelled.Exception.InnerException;

            var secureStream = new SslStream(tcpClient.GetStream(), true, new RemoteCertificateValidationCallback(ValidateServerCertificate));
            secureStream.AuthenticateAsClient(address, null, System.Security.Authentication.SslProtocols.Tls, false);
            sslStream = secureStream;
        }

        private static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            switch (sslPolicyErrors)
            {
                case SslPolicyErrors.RemoteCertificateNameMismatch:
                    return false;

                case SslPolicyErrors.RemoteCertificateNotAvailable:
                    return false;

                case SslPolicyErrors.RemoteCertificateChainErrors:
                    return false;
            }
            return true;
        }

        public void Disconnect()
        {
            sslStream?.Close();
            tcpClient?.Close();
        }

        public Stream Stream => sslStream;

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Allows an object to try to free resources and perform other cleanup operations before it is reclaimed by garbage collection.
        /// </summary>
        ~ChromecastTcpClient()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (sslStream != null)
                    {
                        sslStream.Dispose();
                    }
                    if (tcpClient != null)
                    {
                        tcpClient.Dispose();
                    }
                }
                disposedValue = true;
            }
        }

        private readonly TcpClient tcpClient = new TcpClient();
        private SslStream sslStream;
        private bool disposedValue = false;
    }
}