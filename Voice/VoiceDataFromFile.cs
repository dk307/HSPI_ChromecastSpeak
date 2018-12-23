using NullGuard;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Hspi.Voice
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal static class VoiceDataFromFile
    {
        public static async Task<VoiceData> LoadFromFile(string filePath, CancellationToken token)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(Invariant($"File Not Found:{filePath}"), filePath);
            }

            byte[] data;
            TimeSpan? length;
            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, true))
            {
                data = new byte[stream.Length];
                await stream.ReadAsync(data, 0, (int)stream.Length, token).ConfigureAwait(false);

                stream.Position = 0;
                length = GetMediaLength(filePath, stream);
            }

            return new VoiceData(data, Path.GetExtension(filePath).Replace(".", string.Empty), length);
        }

        private static TimeSpan? GetMediaLength(string filePath, Stream stream)
        {
            try
            {
                using (TagLib.File tagFile = TagLib.File.Create(new TagLib.StreamFileAbstraction(filePath, stream, stream)))
                {
                    return tagFile.Properties?.Duration;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(Invariant($" Failed to get Length of {filePath} with {ex.Message}"));
            }

            return null;
        }
    }
}