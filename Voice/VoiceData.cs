using NullGuard;

namespace Hspi.Voice
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class VoiceData
    {
        public VoiceData(byte[] data, [AllowNull]string mimeType, string extension, double duration)
        {
            Duration = duration;
            Extension = extension;
            MimeType = mimeType;
            Data = data;
        }

        public byte[] Data { get; }
        public string MimeType { get; }
        public string Extension { get; }
        public double Duration { get; }
    }
}