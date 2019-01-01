using Hspi.Utils;
using NullGuard;
using SharpCaster;
using SharpCaster.Channels;
using SharpCaster.Exceptions;
using SharpCaster.Models.ChromecastStatus;
using SharpCaster.Models.MediaStatus;
using SharpCaster.Models.Metadata;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Hspi.Chromecast
{
    /// <summary>
    /// Class to play  Url on Chromecast Device
    /// </summary>
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class SimpleChromecast
    {
        public SimpleChromecast(ChromecastDevice device,
                                Uri playUri,
                                string contentType = null,
                                bool live = false,
                                [AllowNull]TimeSpan? duration = null,
                                [AllowNull]double? volume = null)
        {
            this.volume = volume;
            this.duration = duration;
            this.playUri = playUri;
            this.contentType = contentType;
            this.live = live;
            this.device = device;
        }

        public async Task Play(bool waitforCompletion, CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(Invariant($"https://{device.DeviceIP}/"), UriKind.Absolute, out Uri deviceUri))
            {
                throw new ChromecastException(Invariant($"Failed to create Uri for Chromecast {device.Name} on {device.DeviceIP}"));
            }

            using (var client = new ChromeCastClient(deviceUri))
            {
                await Connect(client, cancellationToken).ConfigureAwait(false);

                var status = await LaunchDefaultApplication(client, cancellationToken).ConfigureAwait(false);

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

                playbackFinished = new TaskCompletionSource<bool>(cancellationToken);
                loadedMediaStatus = await LoadMedia(client, defaultApplication, cancellationToken).ConfigureAwait(false);

                if (loadedMediaStatus == null)
                {
                    throw new MediaLoadException(device.DeviceIP.ToString(), "Failed to load");
                }

                if (waitforCompletion)
                {
                    await WaitForTaskCompleteOrDisconnect(client, playbackFinished.Task, cancellationToken).ConfigureAwait(false);

                    Trace.TraceInformation(Invariant($"Finished Playing Media on Chromecast {device.Name}"));
                    client.MediaChannel.MessageReceived -= MediaChannel_MessageReceived;

                    await client.ReceiverChannel.StopApplication(defaultAppId, cancellationToken).ConfigureAwait(false);

                    // Restore the existing volume
                    if (resetVolumeBack)
                    {
                        Trace.WriteLine(Invariant($"Restoring Volume on Chromecast {device.Name}"));
                        await client.ReceiverChannel.SetVolume(currentVolume.Level, currentVolume.Muted, cancellationToken)
                                                    .ConfigureAwait(false);
                        Trace.WriteLine(Invariant($"Finished Restoring Volume on Chromecast {device.Name}"));
                    }
                }

                Trace.WriteLine(Invariant($"Disconnecting Chromecast {device.Name}"));
                await client.Disconnect(cancellationToken).ConfigureAwait(false);
                Trace.WriteLine(Invariant($"Disconnected Chromecast {device.Name}"));
            }
        }

        private static ChromecastApplication GetDefaultApplication([AllowNull]ChromecastStatus status)
        {
            return status?.Applications?.FirstOrDefault((app) => { return app.AppId == defaultAppId; });
        }

        private void Client_ConnectedChanged(object sender, bool connected)
        {
            if (connected)
            {
                connectedSource.SetResult(true);
            }
            else
            {
                disConnectedSource.SetResult(false);
            }
        }

        private async Task Connect(ChromeCastClient client, CancellationToken cancellationToken)
        {
            Trace.WriteLine(Invariant($"Connecting to Chromecast {device.Name} on {device.DeviceIP}"));

            connectedSource = new TaskCompletionSource<bool>(cancellationToken);
            disConnectedSource = new TaskCompletionSource<bool>(cancellationToken);
            client.ConnectedChanged += Client_ConnectedChanged;

            await client.ConnectChromecast(cancellationToken).ConfigureAwait(false);
            await connectedSource.Task.WaitOnRequestCompletion(cancellationToken).ConfigureAwait(false);

            Trace.WriteLine(Invariant($"Connected to Chromecast {device.Name} on {device.DeviceIP}"));
        }

        private async Task<ChromecastStatus> LaunchDefaultApplication(ChromeCastClient client, CancellationToken cancellationToken)
        {
            Trace.WriteLine(Invariant($"Launching default app on Chromecast {device.Name}"));
            ChromecastStatus status = await client.ReceiverChannel.GetChromecastStatus(cancellationToken).ConfigureAwait(false);

            var defaultApplication = GetDefaultApplication(status);
            status = await client.ReceiverChannel.LaunchApplication(defaultAppId, cancellationToken).ConfigureAwait(false);
            defaultApplication = GetDefaultApplication(status);

            if (defaultApplication == null)
            {
                throw new ChromecastDeviceException(device.Name, "No default app found inspite of launching it");
            }

            await client.ConnectionChannel.ConnectWithDestination(defaultApplication.TransportId, cancellationToken)
                                          .ConfigureAwait(false);

            Trace.WriteLine(Invariant($"Launched default app on Chromecast {device.Name}"));
            return status;
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

            var mediaStatus = await client.MediaChannel.LoadMedia(defaultApplication, playUri, contentType, cancellationToken,
                                                streamType: live ? MediaChannel.StreamTypeLive : MediaChannel.StreamTypeBuffered,
                                                metadata: metadata,
                                                duration: duration.HasValue ? duration.Value.TotalSeconds : 0D).ConfigureAwait(false);

            Trace.WriteLine(Invariant($"Loaded Media [{playUri}] in on Chromecast {device.Name}"));
            return mediaStatus;
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
                                playbackFinished.SetException(new MediaLoadException(device.Name,
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

        private async Task WaitForTaskCompleteOrDisconnect(ChromeCastClient client, Task task, CancellationToken token)
        {
            var disconnectedTask = disConnectedSource.Task;
            var completedTask = await Task.WhenAny(disconnectedTask, task, Task.Delay(-1, token)).ConfigureAwait(false);

            if (completedTask == disconnectedTask)
            {
                throw new ChromecastDeviceException(device.Name, "Device got disconnected");
            }
            token.ThrowIfCancellationRequested();
        }

        private const string defaultAppId = "CC1AD845";
        private readonly string contentType;
        private readonly ChromecastDevice device;
        private readonly TimeSpan? duration;
        private readonly bool live;
        private readonly Uri playUri;
        private readonly double? volume;
        private TaskCompletionSource<bool> connectedSource;
        private TaskCompletionSource<bool> disConnectedSource;
        private MediaStatus loadedMediaStatus;
        private TaskCompletionSource<bool> playbackFinished;
    };
}