using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NullGuard;
using System.Threading;
using Hspi.Exceptions;
using SharpCaster;

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

        public async Task Play(Uri playUri, CancellationToken cancellationToken)
        {
            logger.DebugLog(Invariant($"Connecting to Chromecast {device.Name} on {device.DeviceIP}"));
            if (!Uri.TryCreate(Invariant($"https://{device.DeviceIP}/"), UriKind.Absolute, out Uri myUri))
            {
                throw new HspiException(Invariant($"Failed to create Uri for Chromecast {device.Name} on {device.DeviceIP}"));
            }

            using (var client = new ChromeCastClient())
            {
                var connectedSource = new TaskCompletionSource<bool>(cancellationToken);
                client.ConnectedChanged += (sender, e) => connectedSource.SetResult(true);
                await client.ConnectChromecast(myUri, cancellationToken).ConfigureAwait(false);
                await connectedSource.Task.ConfigureAwait(false);
                logger.DebugLog(Invariant($"Connected to Chromecast {device.Name} on {device.DeviceIP}"));

                await client.ReceiverChannel.GetChromecastStatus(cancellationToken).ConfigureAwait(false);

                logger.DebugLog(Invariant($"Launching default app on Chromecast {device.Name}"));
                const string defaultAppId = "CC1AD845";

                var defaultApplication = client.ChromecastStatus?.Applications?.FirstOrDefault((app) => { return app.AppId == defaultAppId; });

                if (defaultApplication == null)
                {
                    await client.ReceiverChannel.LaunchApplication(defaultAppId, cancellationToken);
                    defaultApplication = client.ChromecastStatus?.Applications?.FirstOrDefault((app) => { return app.AppId == defaultAppId; });
                }

                if (defaultApplication == null)
                {
                    throw new Exception(Invariant($"No default app found inspite of launching it on {device.Name}"));
                }

                await client.ConnectionChannel.ConnectWithDestination(defaultApplication.TransportId, cancellationToken);

                logger.DebugLog(Invariant($"Launched default app on Chromecast {device.Name}"));

                logger.DebugLog(Invariant($"Loading Media in on Chromecast {device.Name}"));
                await client.MediaChannel.LoadMedia(defaultApplication, playUri.ToString(), "audio/wav", cancellationToken);
                logger.DebugLog(Invariant($"Loaded Media in on Chromecast {device.Name}"));

                logger.DebugLog(Invariant($"Diconnecting Chromecast {device.Name}"));
                await client.Disconnect();
                logger.DebugLog(Invariant($"Disconnected Chromecast {device.Name}"));
            }
        }

        private readonly ILogger logger;
        private readonly ChromecastDevice device;
    };
}