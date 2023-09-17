using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NullGuard;
using Sharpcaster;
using Sharpcaster.Interfaces;
using Sharpcaster.Models;
using Sharpcaster.Models.ChromecastStatus;
using Sharpcaster.Models.Media;
using static System.FormattableString;

namespace Hspi.Chromecast
{
    /// <summary>
    /// Class to play Url on Chromecast Device
    /// </summary>
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal sealed class SimpleChromecast
    {
        public SimpleChromecast(ChromecastDevice device,
                                Uri playUri,
                                string contentType = null,
                                bool live = false,
                                [AllowNull] TimeSpan? duration = null,
                                [AllowNull] ushort? volume = null)
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
            var client = new ChromecastClient();
            var status = await Connect(client, cancellationToken).ConfigureAwait(false);

            bool resetVolumeBack = false;
            var restoreVolume = status?.Volume;
            if ((volume.HasValue) && (restoreVolume != null))
            {
                if (((ushort)volume.Value * 100 != volume.Value) || restoreVolume.Muted.Value)
                {
                    var chromecastVolume = volume.Value / 100D;
                    Trace.WriteLine(Invariant($"Setting Volume on Chromecast {device.Name} to {volume.Value}"));
                    status = await client.GetChannel<IReceiverChannel>().SetVolume(chromecastVolume).ConfigureAwait(false);
                    Trace.WriteLine(Invariant($"Finished Setting Volume on Chromecast {device.Name} to {volume.Value}"));
                    resetVolumeBack = true;
                }
            }

            await LaunchDefaultApplication(client, status, cancellationToken).ConfigureAwait(false);

            playbackFinished = new TaskCompletionSource<bool>(cancellationToken);
            var loadedMediaStatus = await LoadMedia(client, cancellationToken).ConfigureAwait(false);

            if (loadedMediaStatus == null)
            {
                throw new MediaLoadException(device.DeviceIP.ToString(), "Failed to load");
            }

            if (waitforCompletion)
            {
                await WaitForTaskCompleteOrDisconnect(client, playbackFinished.Task, cancellationToken).ConfigureAwait(false);
                client.Disconnected -= Client_Disconnected;

                Trace.TraceInformation(Invariant($"Finished Playing Media on Chromecast {device.Name}"));

                // Restore the existing volume
                if (resetVolumeBack && restoreVolume?.Level != null)
                {
                    Trace.WriteLine(Invariant($"Restoring Volume on Chromecast {device.Name}"));
                    await client.GetChannel<IReceiverChannel>().SetVolume(restoreVolume.Level.Value).ConfigureAwait(false);
                    Trace.WriteLine(Invariant($"Finished Restoring Volume on Chromecast {device.Name}"));
                }

                status = await client.GetChannel<IReceiverChannel>().GetChromecastStatusAsync().ConfigureAwait(false);
                var app = GetDefaultApplication(status);
                await client.GetChannel<IReceiverChannel>().StopApplication(app.SessionId).ConfigureAwait(false);

                Trace.WriteLine(Invariant($"Disconnecting Chromecast {device.Name}"));
                await client.DisconnectAsync().ConfigureAwait(false);
                Trace.WriteLine(Invariant($"Disconnected Chromecast {device.Name}"));
            }
        }

        private static ChromecastApplication GetDefaultApplication([AllowNull] ChromecastStatus status)
        {
            return status?.Applications?.FirstOrDefault((app) => { return app.AppId == defaultAppId; });
        }

        private void Client_Disconnected(object sender, EventArgs e)
        {
            disConnectedSource.SetResult(true);
        }

        private async Task<ChromecastStatus> Connect(ChromecastClient client, CancellationToken cancellationToken)
        {
            Trace.WriteLine(Invariant($"Connecting to Chromecast {device.Name} on {device.DeviceIP}"));

            if (!Uri.TryCreate(Invariant($"https://{device.DeviceIP}/"), UriKind.Absolute, out Uri deviceUri))
            {
                throw new ChromecastException(Invariant($"Failed to create Uri for Chromecast {device.Name} on {device.DeviceIP}"));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var receiver = new ChromecastReceiver
            {
                DeviceUri = deviceUri,
                Port = 8009
            };

            client.Disconnected += Client_Disconnected;
            disConnectedSource = new TaskCompletionSource<bool>(cancellationToken);

            var status = await client.ConnectChromecast(receiver).ConfigureAwait(false);
            Trace.WriteLine(Invariant($"Connected to Chromecast {device.Name} on {device.DeviceIP}"));
            return status;
        }

        private async Task LaunchDefaultApplication(ChromecastClient client, ChromecastStatus status, CancellationToken cancellationToken)
        {
            Trace.WriteLine(Invariant($"Launching default app on Chromecast {device.Name}"));

            var defaultApplication = GetDefaultApplication(status);
            status = await client.GetChannel<IReceiverChannel>().LaunchApplicationAsync(defaultAppId).ConfigureAwait(false);
            defaultApplication = GetDefaultApplication(status);

            if (defaultApplication == null)
            {
                throw new ChromecastDeviceException(device.Name, "No default app found inspite of launching it");
            }

            await client.GetChannel<IConnectionChannel>().ConnectAsync(defaultApplication.TransportId)
                                          .ConfigureAwait(false);

            Trace.WriteLine(Invariant($"Launched default app on Chromecast {device.Name}"));
        }

        private async Task<MediaStatus> LoadMedia(ChromecastClient client, CancellationToken cancellationToken)
        {
            Trace.WriteLine(Invariant($"Loading Media [{playUri}] in on Chromecast {device.Name}"));

            var metadata = new MediaMetadata()
            {
                Title = "Homeseer",
                MetadataType = MetadataType.Default,
            };

            var media = new Media()
            {
                ContentUrl = playUri.AbsoluteUri,
                ContentType = contentType,
                StreamType = live ? StreamType.Live : StreamType.Buffered,
                Metadata = metadata,
                Duration = duration.HasValue ? duration.Value.TotalSeconds : 0D,
            };

            var mediaChannel = client.GetChannel<IMediaChannel>();

            mediaChannel.StatusChanged += MediaChannel_StatusChanged;
            var mediaStatus = await mediaChannel.LoadAsync(media, true).ConfigureAwait(false);

            Trace.WriteLine(Invariant($"Loaded Media [{playUri}] in on Chromecast {device.Name}"));
            return mediaStatus;
        }

        private void MediaChannel_StatusChanged(object sender, EventArgs e)
        {
            var channel = sender as IMediaChannel;

            var playerState = channel.Status.FirstOrDefault();
            if (playerState?.PlayerState == PlayerStateType.Idle)
            {
                if (!playbackFinished.Task.IsCompleted)
                {
                    playbackFinished.SetResult(true);
                }
            }
        }

        private async Task WaitForTaskCompleteOrDisconnect(ChromecastClient client, Task task, CancellationToken token)
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
        private readonly ushort? volume;

        private TaskCompletionSource<bool> disConnectedSource;
        private TaskCompletionSource<bool> playbackFinished;
    }
}