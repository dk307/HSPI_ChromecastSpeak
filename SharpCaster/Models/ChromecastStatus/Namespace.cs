using Newtonsoft.Json;

namespace SharpCaster.Models.ChromecastStatus
{
    internal class Namespace
    {
        public Namespace(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}