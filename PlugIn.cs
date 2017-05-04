using HomeSeerAPI;
using Hspi.Exceptions;
using NullGuard;
using Scheduler.Classes;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hspi
{
    using Hspi.Voice;
    using static System.FormattableString;

    /// <summary>
    /// Plugin class for Weather Underground
    /// </summary>
    /// <seealso cref="Hspi.HspiBase" />
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class Plugin : HspiBase
    {
        public Plugin()
            : base(PluginData.PlugInName, supportConfigDevice: true)
        {
        }

        public override string InitIO(string port)
        {
            string result = string.Empty;
            try
            {
                pluginConfig = new PluginConfig(HS);
                configPage = new ConfigPage(HS, pluginConfig);
                LogInfo("Starting Plugin");
#if DEBUG
                pluginConfig.DebugLogging = true;
#endif
                pluginConfig.ConfigChanged += PluginConfig_ConfigChanged;

                RegisterConfigPage();

                Callback.RegisterProxySpeakPlug(PluginData.PlugInName, string.Empty);

                DebugLog("Plugin Started");
            }
            catch (Exception ex)
            {
                result = Invariant($"Failed to initialize PlugIn With {ex.Message}");
                LogError(result);
            }

            return result;
        }

        private void PluginConfig_ConfigChanged(object sender, EventArgs e)
        {
            // RestartMPowerConnections();
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

                if (chromecastDevices.Count > 0)
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
                LogWarning(Invariant($"Failed to Speak  With {ex.Message}"));
            }

            if (pluginConfig.ForwardSpeach)
            {
                HS.SpeakProxy(deviceId, text, wait, host);
            }
        }

        private async Task Speak(string text, IEnumerable<ChromecastDevice> devices)
        {
            var voiceGenerator = new VoiceGenerator(text);
            Task voiceStreamTask = voiceGenerator.GenerateVoiceBytes(ShutdownCancellationToken);

            await voiceStreamTask;
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

                disposedValue = true;
            }

            base.Dispose(disposing);
        }

        private ConfigPage configPage;
        private PluginConfig pluginConfig;
        private bool disposedValue = false;
    }
}