using HomeSeerAPI;
using Hspi.Chromecast;
using Hspi.Exceptions;
using Hspi.Pages;
using Hspi.Utils;
using Hspi.Voice;
using Hspi.Web;
using NullGuard;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Hspi
{
    /// <summary>
    /// Plugin class for Chromecast Speak
    /// </summary>
    /// <seealso cref="Hspi.HspiBase" />
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class Plugin : HspiBase
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
                webServerManager = new MediaWebServerManager(ShutdownCancellationToken);
                LogInfo("Starting Plugin");
#if DEBUG
                pluginConfig.DebugLogging = true;
#endif
                pluginConfig.ConfigChanged += PluginConfig_ConfigChanged;

                Task.Factory.StartNew(StartWebServer);

                RegisterPages();

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
                await webServerManager.StartupServer(address, port).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogError(Invariant($"Failed to start Web Server with Error:{ExceptionHelper.GetFullMessage(ex)}"));
            }
        }

        private void PluginConfig_ConfigChanged(object sender, EventArgs e)
        {
            Task.Factory.StartNew(StartWebServer);
        }

        public override void LogDebug(string message)
        {
            if ((pluginConfig != null) && pluginConfig.DebugLogging)
            {
                base.LogDebug(message);
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
                Trace.TraceWarning(Invariant($"Failed to Speak [{text}] With: {ex.GetFullMessage()}"));
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
                    var voiceGenerator = new VoiceGenerator(text, pluginConfig.SAPIVoice);
                    voiceData = await voiceGenerator.GenerateVoiceAsWavFile(ShutdownCancellationToken).ConfigureAwait(false);
                }

                Trace.WriteLine(Invariant($"Voice for [{text}] is {voiceData.Data.Length} bytes with duration of {voiceData.Duration} of type {voiceData.Extension}"));

                if (voiceData.Data.Length == 0)
                {
                    throw new VoiceGenerationException(Invariant($"Data for [{text}] is Zero Bytes. Check Voice Text or File."));
                }

                var uri = await webServerManager.Add(voiceData.Data, voiceData.Extension, voiceData.Duration).ConfigureAwait(false);

                var combinedStopTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stopTokenSource.Token, ShutdownCancellationToken);

                TimeSpan timeout = MediaWebServerManager.FileEntryExpiry;
                if (voiceData.Duration.HasValue)
                {
                    timeout.Add(voiceData.Duration.Value);
                }

                stopTokenSource.CancelAfter(timeout);
                var playTasks = new List<Task>();
                foreach (var device in devices)
                {
                    var chromecast = new SimpleChromecast(device, uri, duration: voiceData.Duration, volume: device.Volume);
                    playTasks.Add(chromecast.Play(true, combinedStopTokenSource.Token));
                }

                await Task.WhenAll(playTasks.ToArray()).ConfigureAwait(false);
            }
            catch (TaskCanceledException ex)
            {
                if (stopTokenSource.IsCancellationRequested)
                {
                    Trace.TraceWarning(Invariant($"Failed to Speak [{text}] With Timeout Error: {ex.GetFullMessage()}"));
                }
                else
                {
                    Trace.TraceWarning(Invariant($"Failed to Speak [{text}] With: {ex.GetFullMessage()}"));
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(Invariant($"Failed to Speak [{text}] With: {ex.GetFullMessage()}"));
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

        private void RegisterPages()
        {
            string configLink = ConfigPage.Name;
            HS.RegisterPage(configLink, Name, string.Empty);

            HomeSeerAPI.WebPageDesc wpd = new HomeSeerAPI.WebPageDesc()
            {
                plugInName = Name,
                link = configLink,
                linktext = "Configuration",
                page_title = Invariant($"{PluginData.PlugInName} Configuration"),
            };
            Callback.RegisterConfigLink(wpd);
            Callback.RegisterLink(wpd);
        }

        #region "Action Override"

        private const int ActionChromecastCastTANumber = 1;

        public override string ActionBuildUI([AllowNull]string uniqueControlId, IPlugInAPI.strTrigActInfo actionInfo)
        {
            try
            {
                switch (actionInfo.TANumber)
                {
                    case ActionChromecastCastTANumber:
                        using (var actionPage = new ActionPage(HS, pluginConfig))
                        {
                            return actionPage.GetRefreshActionUI(uniqueControlId ?? string.Empty, actionInfo);
                        }

                    default:
                        return base.ActionBuildUI(uniqueControlId, actionInfo);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(Invariant($"Failed to give build Action UI with {ex.GetFullMessage()}"));
                throw;
            }
        }

        public override bool ActionConfigured(IPlugInAPI.strTrigActInfo actionInfo)
        {
            try
            {
                switch (actionInfo.TANumber)
                {
                    case ActionChromecastCastTANumber:
                        if (actionInfo.DataIn != null)
                        {
                            var action = ObjectSerialize.DeSerializeFromBytes(actionInfo.DataIn) as ChromecastCastAction;
                            return action != null && action.IsValid();
                        }

                        return false;

                    default:
                        return base.ActionConfigured(actionInfo);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(Invariant($"Failed to ActionConfigured with {ex.GetFullMessage()}"));
                return false;
            }
        }

        public override int ActionCount()
        {
            return 1;
        }

        public override string ActionFormatUI(IPlugInAPI.strTrigActInfo actionInfo)
        {
            try
            {
                switch (actionInfo.TANumber)
                {
                    case ActionChromecastCastTANumber:
                        if (actionInfo.DataIn != null)
                        {
                            var action = ObjectSerialize.DeSerializeFromBytes(actionInfo.DataIn) as ChromecastCastAction;
                            if (action != null)
                            {
                                StringBuilder stringBuilder = new StringBuilder();

                                stringBuilder.Append(@"Play ");
                                stringBuilder.Append(action.Url ?? string.Empty);
                                stringBuilder.Append(" on ");

                                if ((action != null) && pluginConfig.Devices.TryGetValue(action.ChromecastDeviceId, out var device))
                                {
                                    stringBuilder.Append(device.Name);
                                }
                                else
                                {
                                    stringBuilder.Append(@"Unknown");
                                }

                                return stringBuilder.ToString();
                            }
                        }
                        return Invariant($"{PluginData.PlugInName} Unknown Chromecast play");

                    default:
                        return base.ActionFormatUI(actionInfo);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(Invariant($"Failed to ActionFormatUI with {ex.GetFullMessage()}"));
                throw;
            }
        }

        public override IPlugInAPI.strMultiReturn ActionProcessPostUI([AllowNull] NameValueCollection postData, IPlugInAPI.strTrigActInfo actionInfo)
        {
            try
            {
                switch (actionInfo.TANumber)
                {
                    case ActionChromecastCastTANumber:
                        using (var actionPage = new ActionPage(HS, pluginConfig))
                        {
                            return actionPage.GetRefreshActionPostUI(postData, actionInfo);
                        }

                    default:
                        return base.ActionProcessPostUI(postData, actionInfo);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(Invariant($"Failed to ActionProcessPostUI with {ex.GetFullMessage()}"));
                throw;
            }
        }

        public override bool ActionReferencesDevice(IPlugInAPI.strTrigActInfo actionInfo, int deviceId)
        {
            return false;
        }

        public override string get_ActionName(int actionNumber)
        {
            try
            {
                switch (actionNumber)
                {
                    case ActionChromecastCastTANumber:
                        return @"Chromecast Cast Action";

                    default:
                        return base.get_ActionName(actionNumber);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(Invariant($"Failed to give Action Name with {ex.GetFullMessage()}"));
                throw;
            }
        }

        public override bool HandleAction(IPlugInAPI.strTrigActInfo actionInfo)
        {
            using (var stopTokenSource = new CancellationTokenSource())
            {
                try
                {
                    switch (actionInfo.TANumber)
                    {
                        case ActionChromecastCastTANumber:
                            if (actionInfo.DataIn != null)
                            {
                                var action = ObjectSerialize.DeSerializeFromBytes(actionInfo.DataIn) as ChromecastCastAction;
                                if ((action != null) && (action.IsValid()))
                                {
                                    if (pluginConfig.Devices.TryGetValue(action.ChromecastDeviceId, out var device))
                                    {
                                        using (var combinedStopTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stopTokenSource.Token, ShutdownCancellationToken))
                                        {
                                            stopTokenSource.CancelAfter(60 * 1000);

                                            var chromecast = new SimpleChromecast(device, new System.Uri(action.Url), action.ContentType, action.Live);
                                            chromecast.Play(false, combinedStopTokenSource.Token).Wait();
                                            return true;
                                        }
                                    }
                                }
                            }

                            Trace.TraceWarning(Invariant($"Failed to execute action with invalid action"));
                            return false;

                        default:
                            return base.HandleAction(actionInfo);
                    }
                }
                catch (TaskCanceledException ex)
                {
                    if (stopTokenSource.IsCancellationRequested)
                    {
                        Trace.TraceWarning(Invariant($"Failed to execute action with timeout Error: {ex.GetFullMessage()}"));
                    }
                    else
                    {
                        Trace.TraceWarning(Invariant($"Failed to execute action with: {ex.GetFullMessage()}"));
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning(Invariant($"Failed to execute action with {ex.GetFullMessage()}"));
                    return false;
                }
            }
        }

        #endregion "Action Override"

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