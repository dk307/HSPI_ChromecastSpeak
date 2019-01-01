using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;

namespace SharpCaster.Models.ChromecastStatus
{
    internal class ChromecastApplication
    {
        [JsonConstructor]
        public ChromecastApplication(string appId, string displayName, IList<Namespace> namespaces,
                                     string sessionId, string statusText, string transportId,
                                     bool isIdleScreen)
        {
            TransportId = transportId;
            IsIdleScreen = isIdleScreen;
            StatusText = statusText;
            SessionId = sessionId;
            Namespaces = namespaces;
            DisplayName = displayName;
            AppId = appId;
        }

        public string AppId { get; }
        public string DisplayName { get; }

        [DefaultValue(false)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool IsIdleScreen { get; }

        public IList<Namespace> Namespaces { get; }
        public string SessionId { get; }
        public string StatusText { get; }
        public string TransportId { get; }
    }
}