using System.Collections.Generic;

namespace SharpCaster.Models.MediaStatus
{
    internal class MediaStatusResponse
    {
        public string type { get; set; }
        public List<MediaStatus> status { get; set; }
        public int requestId { get; set; }
    }
}