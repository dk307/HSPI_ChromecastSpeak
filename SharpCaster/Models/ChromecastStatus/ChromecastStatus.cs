using Newtonsoft.Json;
using System.Collections.Generic;

namespace SharpCaster.Models.ChromecastStatus
{
    internal class ChromecastStatus
    {
        [JsonConstructor]
        public ChromecastStatus(IList<ChromecastApplication> applications, bool? isActiveInput, bool? isStandBy, Volume volume)
        {
            Volume = volume;
            IsStandBy = isStandBy;
            IsActiveInput = isActiveInput;
            Applications = applications;
        }

        public IList<ChromecastApplication> Applications { get; }
        public bool? IsActiveInput { get; }
        public bool? IsStandBy { get; }
        public Volume Volume { get; }
    }
}