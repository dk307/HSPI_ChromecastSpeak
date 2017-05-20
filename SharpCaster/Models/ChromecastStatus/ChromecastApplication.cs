using System.Collections.Generic;
using Newtonsoft.Json;

namespace SharpCaster.Models.ChromecastStatus
{
    internal class ChromecastApplication
    {
        [JsonConstructor]
        public ChromecastApplication(string appId, string displayName, IList<Namespace> namespaces,
                                     string sessionId, string statusText, string transportId)
        {
            TransportId = transportId;
            StatusText = statusText;
            SessionId = sessionId;
            Namespaces = namespaces;
            DisplayName = displayName;
            AppId = appId;
        }

        public string AppId { get; }
        public string DisplayName { get; }
        public IList<Namespace> Namespaces { get; }
        public string SessionId { get; }
        public string StatusText { get; }
        public string TransportId { get; }
    }
}