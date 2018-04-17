using NullGuard;
using SharpCaster;
using SharpCaster.Exceptions;
using SharpCaster.Models.ChromecastStatus;
using SharpCaster.Models.MediaStatus;
using SharpCaster.Models.Metadata;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hspi.Chromecast
{
    using static System.FormattableString;

    /// <summary>
    /// Class to play  Url on Chromecast Device
    /// </summary>
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class SimpleChromecast
    {
        public SimpleChromecast(ChromecastDevice device, Uri playUri, TimeSpan? duration, double? volume)
        {
            this.volume = volume;
            this.duration = duration;
            this.playUri = playUri;
            this.device = device;
        }

        public async Task Play(CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(Invariant($"https://{device.DeviceIP}/"), UriKind.Absolute, out Uri deviceUri))
            {
                throw new ChromecastException(Invariant($"Failed to create Uri for Chromecast {device.Name} on {device.DeviceIP}"));
            }

            using (var client = new ChromeCastClient(deviceUri))
            {
                await Connect(client, cancellationToken).ConfigureAwait(false);

                var status = await LaunchApplication(client, cancellationToken).ConfigureAwait(false);

                bool resetVolumeBack = false;
                var currentVolume = status?.Volume;
                if (volume.HasValue)
                {
                    double chromecastVolume = volume.Value / 100;
                    if (currentVolume != null && ((currentVolume.Level != chromecastVolume) || currentVolume.Muted))
                    {
                        Trace.WriteLine(Invariant($"Setting Volume on Chromecast {device.Name} to {volume.Value}"));
                        status = await client.ReceiverChannel.SetVolume(chromecastVolume, false, cancellationToken).ConfigureAwait(false);
                        Trace.WriteLine(Invariant($"Finished Setting Volume on Chromecast {device.Name} to {volume.Value}"));
                        resetVolumeBack = true;
                    }
                }

                var defaultApplication = GetDefaultApplication(status);

                client.MediaChannel.MessageReceived += MediaChannel_MessageReceived;

                loadedMediaStatus = await LoadMedia(client, defaultApplication, cancellationToken).ConfigureAwait(false);

                if (loadedMediaStatus == null)
                {
                    throw new MediaLoadException(device.DeviceIP.ToString(), "Failed to load");
                }

                await playbackFinished.Task.WaitOnRequestCompletion(cancellationToken).ConfigureAwait(false);

                client.MediaChannel.MessageReceived -= MediaChannel_MessageReceived;

                // Restore the existing volume
                if (resetVolumeBack)
                {
                    Trace.WriteLine(Invariant($"Restoring Volume on Chromecast {device.Name}"));
                    await client.ReceiverChannel.SetVolume(currentVolume.Level, currentVolume.Muted, cancellationToken)
                                                .ConfigureAwait(false);
                    Trace.WriteLine(Invariant($"Finished Restoring Volume on Chromecast {device.Name}"));
                }

                Trace.TraceInformation(Invariant($"Played Speech on Chromecast {device.Name}"));
                Trace.WriteLine(Invariant($"Disconnecting Chromecast {device.Name}"));
                await client.Disconnect(cancellationToken).ConfigureAwait(false);
                Trace.WriteLine(Invariant($"Disconnected Chromecast {device.Name}"));
            }
        }

        private void MediaChannel_MessageReceived(object sender, [AllowNull]MediaStatus mediaStatus)
        {
            if (loadedMediaStatus != null)
            {
                if (mediaStatus != null)
                {
                    if ((mediaStatus.PlayerState == PlayerState.Idle) &&
                        (mediaStatus.MediaSessionId == loadedMediaStatus.MediaSessionId))
                    {
                        switch (mediaStatus.IdleReason)
                        {
                            case IdleReason.CANCELLED:
                            case IdleReason.ERROR:
                            case IdleReason.INTERRUPTED:
                                playbackFinished.SetException(new MediaLoadException(device.DeviceIP.ToString(),
                                                                                     mediaStatus.IdleReason.ToString()));
                                break;

                            case IdleReason.FINISHED:
                                playbackFinished.SetResult(true);
                                break;
                        }
                    }
                }
            }
        }

        private async Task<MediaStatus> LoadMedia(ChromeCastClient client, ChromecastApplication defaultApplication,
                                                  CancellationToken cancellationToken)
        {
            Trace.WriteLine(Invariant($"Loading Media [{playUri}] in on Chromecast {device.Name}"));

            var metadata = new GenericMediaMetadata()
            {
                title = "Homeseer",
                metadataType = SharpCaster.Models.Enums.MetadataType.GENERIC,
            };

            var mediaStatus = await client.MediaChannel.LoadMedia(defaultApplication, playUri, null, cancellationToken,
                                                metadata: metadata,
                                                duration: duration.HasValue ? duration.Value.TotalSeconds : 0D).ConfigureAwait(false);

            Trace.WriteLine(Invariant($"Loaded Media [{playUri}] in on Chromecast {device.Name}"));
            return mediaStatus;
        }

        private async Task<ChromecastStatus> LaunchApplication(ChromeCastClient client, CancellationToken cancellationToken)
        {
            Trace.WriteLine(Invariant($"Launching default app on Chromecast {device.Name}"));
            ChromecastStatus status = await client.ReceiverChannel.GetChromecastStatus(cancellationToken).ConfigureAwait(false);

            var defaultApplication = GetDefaultApplication(status);
            //if (defaultApplication == null)
            //{
            status = await client.ReceiverChannel.LaunchApplication(defaultAppId, cancellationToken).ConfigureAwait(false);
            defaultApplication = GetDefaultApplication(status);
            //}
            //else
            //{
            //    Trace.WriteLine(Invariant($"Default app is already running on Chromecast {device.Name}"));
            //}

            if (defaultApplication == null)
            {
                throw new ChromecastDeviceException(device.Name, "No default app found inspite of launching it");
            }

            await client.ConnectionChannel.ConnectWithDestination(defaultApplication.TransportId, cancellationToken)
                                          .ConfigureAwait(false);

            Trace.WriteLine(Invariant($"Launched default app on Chromecast {device.Name}"));
            return status;
        }

        private static ChromecastApplication GetDefaultApplication(ChromecastStatus status)
        {
            return status?.Applications?.FirstOrDefault((app) => { return app.AppId == defaultAppId; });
        }

        private async Task Connect(ChromeCastClient client, CancellationToken cancellationToken)
        {
            Trace.WriteLine(Invariant($"Connecting to Chromecast {device.Name} on {device.DeviceIP}"));
            connectedSource = new TaskCompletionSource<bool>(cancellationToken);
            client.ConnectedChanged += Client_ConnectedChanged;
            await client.ConnectChromecast(cancellationToken).ConfigureAwait(false);
            await connectedSource.Task.WaitOnRequestCompletion(cancellationToken).ConfigureAwait(false);
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

        private TaskCompletionSource<bool> playbackFinished = new TaskCompletionSource<bool>();

        private MediaStatus loadedMediaStatus;
        private const string defaultAppId = "CC1AD845";
        private readonly ChromecastDevice device;
        private readonly Uri playUri;
        private readonly TimeSpan? duration;
        private readonly double? volume;

        private TaskCompletionSource<bool> connectedSource;
    };
}