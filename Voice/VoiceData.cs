using NullGuard;
using System;

namespace Hspi.Voice
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class VoiceData
    {
        public VoiceData(byte[] data, string extension, TimeSpan? duration)
        {
            Duration = duration;
            Extension = extension;
            Data = data;
        }

        public byte[] Data { get; }
        public string Extension { get; }
        public TimeSpan? Duration { get; }
    }
}