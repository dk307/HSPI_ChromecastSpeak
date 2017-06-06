using HomeSeerAPI;
using Hspi.Chromecast;
using Hspi.Voice;
using Hspi.Web;
using NullGuard;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hspi
{
    using static System.FormattableString;

    /// <summary>
    /// Plugin class for Chromecast Speak
    /// </summary>
    /// <seealso cref="Hspi.HspiBase" />
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class Plugin : HspiBase, ILogger
    {
        public Plugin()
            : base(PluginData.PlugInName)
        {
        }

        public override string InitIO(string port)
        {
            string result = string.Empty;
            try
            {
                pluginConfig = new PluginConfig(HS);
                configPage = new ConfigPage(HS, pluginConfig);
                webServerManager = new MediaWebServerManager(this, ShutdownCancellationToken);
                LogInfo("Starting Plugin");
#if DEBUG
                pluginConfig.DebugLogging = true;
#endif
                pluginConfig.ConfigChanged += PluginConfig_ConfigChanged;

                Task.Factory.StartNew(() => StartWebServer());

                RegisterConfigPage();

                Callback.RegisterProxySpeakPlug(PluginData.PlugInName, string.Empty);

                LogInfo("Plugin Started");
            }
            catch (Exception ex)
            {
                result = Invariant($"Failed to initialize PlugIn With {ex.GetFullMessage()}");
                LogError(result);
            }

            return result;
        }

        private async Task StartWebServer()
        {
            try
            {
                var address = pluginConfig.CalculateServerIPAddress();
                var port = pluginConfig.WebServerPort;
                await webServerManager.StartupServer(address, port);
            }
            catch (Exception ex)
            {
                LogError(Invariant($"Failed to start Web Server with Error:{ExceptionHelper.GetFullMessage(ex)}"));
            }
        }

        private void PluginConfig_ConfigChanged(object sender, EventArgs e)
        {
            Task.Factory.StartNew(() => StartWebServer());
        }

        public override void DebugLog(string message)
        {
            if (pluginConfig.DebugLogging)
            {
                base.DebugLog(message);
            }
        }

        public override string GetPagePlugin(string page, [AllowNull]string user, int userRights, [AllowNull]string queryString)
        {
            if (page == ConfigPage.Name)
            {
                return configPage.GetWebPage(queryString);
            }

            return string.Empty;
        }

        public override string PostBackProc(string page, string data, [AllowNull]string user, int userRights)
        {
            if (page == ConfigPage.Name)
            {
                return configPage.PostBackProc(data, user, userRights);
            }

            return string.Empty;
        }

        public override void SpeakIn(int deviceId, string text, bool wait, [AllowNull]string host)
        {
            try
            {
                host = (host != null) ? host.Trim() : string.Empty;
                var hostsList = host.Split(',');

                IDictionary<string, ChromecastDevice> selectedDevices = new Dictionary<string, ChromecastDevice>();

                var chromecastDevices = pluginConfig.Devices;
                foreach (var individualHost in hostsList)
                {
                    var individualHostReal = individualHost.Trim();

                    if (string.IsNullOrWhiteSpace(host) || individualHostReal == "*" || individualHostReal == "*:*")
                    {
                        foreach (var chromecastDevice in chromecastDevices)
                        {
                            selectedDevices.Add(chromecastDevice);
                        }
                        break;
                    }
                    else
                    {
                        foreach (var chromecastDevice in chromecastDevices)
                        {
                            string deviceName = chromecastDevice.Key;
                            if (string.Compare(individualHostReal, deviceName, StringComparison.OrdinalIgnoreCase) == 0)
                            {
                                selectedDevices.Add(deviceName, chromecastDevice.Value);
                            }
                        }
                    }
                }

                if (selectedDevices.Count > 0)
                {
                    Task speakTask = Speak(text, chromecastDevices.Values);

                    if (wait)
                    {
                        speakTask.Wait(ShutdownCancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                LogWarning(Invariant($"Failed to Speak [{text}] With: {ex.GetFullMessage()}"));
            }

            if (pluginConfig.ForwardSpeach)
            {
                HS.SpeakProxy(deviceId, text, wait, host);
            }
        }

        private async Task Speak(string text, IEnumerable<ChromecastDevice> devices)
        {
            var stopTokenSource = new CancellationTokenSource();

            try
            {
                bool isFileName = IsReferingToFile(text);

                VoiceData voiceData;
                if (isFileName)
                {
                    voiceData = await VoiceDataFromFile.LoadFromFile(text, ShutdownCancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var voiceGenerator = new VoiceGenerator(this, text, pluginConfig.SAPIVoice);
                    voiceData = await voiceGenerator.GenerateVoiceAsWavFile(ShutdownCancellationToken).ConfigureAwait(false);
                }

                var uri = await webServerManager.Add(voiceData.Data, voiceData.Extension, voiceData.Duration).ConfigureAwait(false);

                var combinedStopTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stopTokenSource.Token, ShutdownCancellationToken);

                TimeSpan timeout = MediaWebServerManager.FileEntryExpiry;
                if (voiceData.Duration.HasValue)
                {
                    timeout.Add(voiceData.Duration.Value);
                }

                stopTokenSource.CancelAfter(timeout);
                List<Task> playTasks = new List<Task>();
                foreach (var device in devices)
                {
                    SimpleChromecast chromecast = new SimpleChromecast(this, device, uri, voiceData.Duration, device.Volume);
                    playTasks.Add(chromecast.Play(combinedStopTokenSource.Token));
                }

                await Task.WhenAll(playTasks.ToArray()).ConfigureAwait(false);
            }
            catch (TaskCanceledException ex)
            {
                if (stopTokenSource.IsCancellationRequested)
                {
                    LogWarning(Invariant($"Failed to Speak [{text}] With Timeout Error: {ex.GetFullMessage()}"));
                }
                else
                {
                    LogWarning(Invariant($"Failed to Speak [{text}] With: {ex.GetFullMessage()}"));
                }
            }
            catch (Exception ex)
            {
                LogWarning(Invariant($"Failed to Speak [{text}] With: {ex.GetFullMessage()}"));
            }
        }

        private bool IsReferingToFile(string text)
        {
            switch (HS.GetOSType())
            {
                case eOSType.windows:
                    return (text.Length > 2) && text.Substring(1, 2).Equals(":\\");

                case eOSType.linux:
                    return (text.Length > 1) && text.Substring(0, 1).Equals("/");

                default:
                    throw new ArgumentException("Unknown OS Type");
            }
        }

        private void RegisterConfigPage()
        {
            string link = ConfigPage.Name;
            HS.RegisterPage(link, Name, string.Empty);

            HomeSeerAPI.WebPageDesc wpd = new HomeSeerAPI.WebPageDesc()
            {
                plugInName = Name,
                link = link,
                linktext = "Configuration",
                page_title = Invariant($"{PluginData.PlugInName} Configuration"),
            };
            Callback.RegisterConfigLink(wpd);
            Callback.RegisterLink(wpd);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (pluginConfig != null)
                {
                    pluginConfig.ConfigChanged -= PluginConfig_ConfigChanged;
                }
                if (configPage != null)
                {
                    configPage.Dispose();
                }

                if (pluginConfig != null)
                {
                    pluginConfig.Dispose();
                }

                if (webServerManager != null)
                {
                    webServerManager.Dispose();
                }

                disposedValue = true;
            }

            base.Dispose(disposing);
        }

        private MediaWebServerManager webServerManager;
        private ConfigPage configPage;
        private PluginConfig pluginConfig;
        private bool disposedValue = false;
    }
}