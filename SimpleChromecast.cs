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
using SharpCaster.Channels;
using SharpCaster.Models.ChromecastStatus;
using SharpCaster.Models.Metadata;

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

        public async Task Play(Uri playUri, TimeSpan? duration, double? volume, CancellationToken cancellationToken)
        {
            Trace.WriteLine(Invariant($"Connecting to Chromecast {device.Name} on {device.DeviceIP}"));
            if (!Uri.TryCreate(Invariant($"https://{device.DeviceIP}/"), UriKind.Absolute, out Uri deviceUri))
            {
                throw new HspiException(Invariant($"Failed to create Uri for Chromecast {device.Name} on {device.DeviceIP}"));
            }

            using (var client = new ChromeCastClient(deviceUri))
            {
                await Connect(client, cancellationToken).ConfigureAwait(false);

                try
                {
                    var defaultApplication = await LaunchApplication(client, cancellationToken).ConfigureAwait(false);

                    await client.MediaChannel.GetMediaStatus(defaultApplication.TransportId, cancellationToken).ConfigureAwait(false);
                    var currentVolume = client.ChromecastStatus?.Volume;
                    if (volume.HasValue)
                    {
                        await client.ReceiverChannel.SetVolume(volume / 100, false, cancellationToken).ConfigureAwait(false);
                    }

                    await LoadMedia(client, defaultApplication, playUri, duration, cancellationToken).ConfigureAwait(false);

                    await WaitForPlayToFinish(client, defaultApplication, cancellationToken).ConfigureAwait(false);

                    // Restore the existing volume
                    if (volume.HasValue && (currentVolume != null))
                    {
                        await client.ReceiverChannel.SetVolume(currentVolume.level, currentVolume.muted, cancellationToken)
                                                    .ConfigureAwait(false);
                    }

                    this.logger.LogInfo(Invariant($"Played Speech on Chromecast {device.Name}"));
                }
                finally
                {
                    Trace.WriteLine(Invariant($"Disconnecting Chromecast {device.Name}"));
                    await client.Abort(cancellationToken).ConfigureAwait(false);
                    Trace.WriteLine(Invariant($"Disconnected Chromecast {device.Name}"));
                }
            }
        }

        private async Task WaitForPlayToFinish(ChromeCastClient client, ChromecastApplication defaultApplication, CancellationToken cancellationToken)
        {
            var itemId = client.MediaStatus?.CurrentItemId;

            bool played;
            bool aborted;
            do
            {
                played = (client.MediaStatus != null) &&
                          (client.MediaStatus.CurrentItemId >= itemId.Value) &&
                          (client.MediaStatus.IdleReason == SharpCaster.Models.MediaStatus.IdleReason.FINISHED);

                aborted = (client.ChromecastStatus.Applications?.FirstOrDefault()?.AppId != defaultAppId);

                if (played || aborted)
                {
                    break;
                }

                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                await client.MediaChannel.GetMediaStatus(defaultApplication.TransportId, cancellationToken).ConfigureAwait(false);
            } while (!played && !aborted);

            if (aborted)
            {
                Trace.WriteLine(Invariant($"Play on Chromecast {device.Name} was aborted."));
            }
        }

        private async Task LoadMedia(ChromeCastClient client, ChromecastApplication defaultApplication, Uri playUri,
            TimeSpan? duration, CancellationToken cancellationToken)
        {
            Trace.WriteLine(Invariant($"Loading Media in on Chromecast {device.Name}"));

            var metadata = new GenericMediaMetadata()
            {
                title = "Homeseer",
                metadataType = SharpCaster.Models.Enums.MetadataType.GENERIC,
            };

            await client.MediaChannel.LoadMedia(defaultApplication, playUri, null, cancellationToken,
                                                metadata: metadata,
                                                duration: duration.HasValue ? duration.Value.TotalSeconds : 0D);

            Trace.WriteLine(Invariant($"Loaded Media in on Chromecast {device.Name}"));
        }

        private async Task<ChromecastApplication> LaunchApplication(ChromeCastClient client, CancellationToken cancellationToken)
        {
            await client.ReceiverChannel.GetChromecastStatus(cancellationToken).ConfigureAwait(false);

            Trace.WriteLine(Invariant($"Launching default app on Chromecast {device.Name}"));

            var defaultApplication = client.ChromecastStatus?.Applications?.FirstOrDefault((app) => { return app.AppId == defaultAppId; });
            if (defaultApplication == null)
            {
                await client.ReceiverChannel.LaunchApplication(defaultAppId, cancellationToken);
                defaultApplication = client.ChromecastStatus?.Applications?.FirstOrDefault((app) => { return app.AppId == defaultAppId; });
            }
            else
            {
                Trace.WriteLine(Invariant($"Default app is already running on Chromecast {device.Name}"));
            }

            if (defaultApplication == null)
            {
                throw new ChromecastDeviceException(device.Name, "No default app found inspite of launching it");
            }

            await client.ConnectionChannel.ConnectWithDestination(defaultApplication.TransportId, cancellationToken);

            Trace.WriteLine(Invariant($"Launched default app on Chromecast {device.Name}"));
            return defaultApplication;
        }

        private async Task Connect(ChromeCastClient client, CancellationToken cancellationToken)
        {
            Trace.WriteLine(Invariant($"Connecting to Chromecast {device.Name} on {device.DeviceIP}"));
            connectedSource = new TaskCompletionSource<bool>(cancellationToken);
            client.ConnectedChanged += Client_ConnectedChanged;
            await client.ConnectChromecast(cancellationToken).ConfigureAwait(false);
            await WaitOnRequestCompletion(connectedSource.Task, cancellationToken).ConfigureAwait(false);
            client.ConnectedChanged -= Client_ConnectedChanged;

            Trace.WriteLine(Invariant($"Connected to Chromecast {device.Name} on {device.DeviceIP}"));
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

        private const string defaultAppId = "CC1AD845";

        private readonly ILogger logger;
        private readonly ChromecastDevice device;
        private TaskCompletionSource<bool> connectedSource;
    };
}