using System;

namespace SharpCaster.Models
{
    internal class ChromecastSSLClientDataReceivedArgs : EventArgs
    {
        public ChromecastSSLClientDataReceivedArgs(CastMessage message)
        {
            Message = message;
        }

        public CastMessage Message { get; set; }
    }
}