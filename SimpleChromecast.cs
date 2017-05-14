using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NullGuard;
using System.Threading;
using Hspi.Exceptions;
using SharpCaster;
using SharpCaster.Exceptions;
using System.Diagnostics;

namespace Hspi.Chromecast
{
    using static System.FormattableString;

    /// <summary>
    /// Not Thread Safe
    /// </summary>
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class SimpleChromecast
    {
        public SimpleChromecast(ILogger logger, ChromecastDevice device)
        {
            this.device = device;
            this.logger = logger;
        }

        public async Task Play(Uri playUri, [AllowNull]string mimeType, double duration, double? volume, CancellationToken cancellationToken)
        {
            Debug.WriteLine(Invariant($"Connecting to Chromecast {device.Name} on {device.DeviceIP}"));
            if (!Uri.TryCreate(Invariant($"https://{device.DeviceIP}/"), UriKind.Absolute, out Uri deviceUri))
            {
                throw new HspiException(Invariant($"Failed to create Uri for Chromecast {device.Name} on {device.DeviceIP}"));
            }

            using (var client = new ChromeCastClient(deviceUri))
            {
                Debug.WriteLine(Invariant($"Connecting to Chromecast {device.Name} on {device.DeviceIP}"));
                connectedSource = new TaskCompletionSource<bool>(cancellationToken);
                client.ConnectedChanged += Client_ConnectedChanged;
                await client.ConnectChromecast(cancellationToken).ConfigureAwait(false);
                await WaitOnRequestCompletion(connectedSource.Task, cancellationToken).ConfigureAwait(false);
                client.ConnectedChanged -= Client_ConnectedChanged;

                Debug.WriteLine(Invariant($"Connected to Chromecast {device.Name} on {device.DeviceIP}"));

                try
                {
                    await client.ReceiverChannel.GetChromecastStatus(cancellationToken).ConfigureAwait(false);

                    Debug.WriteLine(Invariant($"Launching default app on Chromecast {device.Name}"));
                    const string defaultAppId = "CC1AD845";

                    var defaultApplication = client.ChromecastStatus?.Applications?.FirstOrDefault((app) => { return app.AppId == defaultAppId; });
                    if (defaultApplication == null)
                    {
                        await client.ReceiverChannel.LaunchApplication(defaultAppId, cancellationToken);
                        defaultApplication = client.ChromecastStatus?.Applications?.FirstOrDefault((app) => { return app.AppId == defaultAppId; });
                    }
                    else
                    {
                        Debug.WriteLine(Invariant($"Default app is already running on Chromecast {device.Name}"));
                    }

                    if (defaultApplication == null)
                    {
                        throw new ChromecastDeviceException(device.Name, "No default app found inspite of launching it");
                    }

                    if (volume.HasValue)
                    {
                        await client.ReceiverChannel.SetVolume(volume / 100, false, cancellationToken);
                    }

                    await client.ConnectionChannel.ConnectWithDestination(defaultApplication.TransportId, cancellationToken);

                    Debug.WriteLine(Invariant($"Launched default app on Chromecast {device.Name}"));

                    Debug.WriteLine(Invariant($"Loading Media in on Chromecast {device.Name}"));
                    await client.MediaChannel.LoadMedia(defaultApplication, playUri, mimeType, cancellationToken,
                                                        duration: duration);
                    Debug.WriteLine(Invariant($"Loaded Media in on Chromecast {device.Name}"));

                    Debug.WriteLine(Invariant($"Disconnecting Chromecast {device.Name}"));
                    await client.Disconnect(cancellationToken).ConfigureAwait(false);
                    Debug.WriteLine(Invariant($"Disconnected Chromecast {device.Name}"));
                }
                catch
                {
                    await client.Abort().ConfigureAwait(false);
                    throw;
                }
            }
        }

        private void Client_ConnectedChanged(object sender, bool connected)
        {
            if (connected)
            {
                connectedSource.SetResult(true);
            }
        }

        protected static async Task WaitOnRequestCompletion(Task requestCompletedTask, CancellationToken token)
        {
            await Task.WhenAny(requestCompletedTask, Task.Delay(-1, token));
            token.ThrowIfCancellationRequested();
        }

        private readonly ILogger logger;
        private readonly ChromecastDevice device;
        private TaskCompletionSource<bool> connectedSource;
    };
}