using System;

namespace Hspi.Pages
{
    [Serializable]
    internal sealed class ChromecastCastAction
    {
        public string ChromecastDeviceId = null;
        public string Url = null;
        public string ContentType = null;
        public bool Live = false;

        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(ChromecastDeviceId) &&
                   !string.IsNullOrWhiteSpace(Url);
        }
    }
}