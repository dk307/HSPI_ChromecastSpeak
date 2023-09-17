using System;
using System.Runtime.Serialization;

namespace Hspi.Exceptions
{
    [Serializable]
    public class VoiceGenerationException : HspiException
    {
        public VoiceGenerationException(string message) : base(message)
        {
        }

        public VoiceGenerationException()
        {
        }

        protected VoiceGenerationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}