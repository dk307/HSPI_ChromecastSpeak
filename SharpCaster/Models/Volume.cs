using Newtonsoft.Json;

namespace SharpCaster.Models
{
    internal class Volume
    {
        [JsonConstructor]
        public Volume(float level, bool muted)
        {
            Muted = muted;
            Level = level;
        }

        public float Level { get; }
        public bool Muted { get; }
    }
}