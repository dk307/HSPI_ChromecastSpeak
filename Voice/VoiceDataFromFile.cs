using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NullGuard;
using MediaInfo;
using System.Diagnostics;

namespace Hspi.Voice
{
    using System;
    using static System.FormattableString;

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
            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, true))
            {
                data = new byte[stream.Length];
                await stream.ReadAsync(data, 0, (int)stream.Length);
            }

            var length = GetMediaLength(filePath);
            return new VoiceData(data, Path.GetExtension(filePath).Replace(".", string.Empty), length);
        }

        private static TimeSpan? GetMediaLength(string filePath)
        {
            try
            {
                using (MediaInfo.MediaInfo info = new MediaInfo.MediaInfo())
                {
                    info.Option("ParseSpeed", "0"); // Advanced information (e.g. GOP size, captions detection) not needed, request to scan as fast as possible
                    info.Option("ReadByHuman", "0"); // Human readable strings are not needed, no noeed to spend time on them
                    info.Open(filePath);

                    string duration = info.Get(StreamKind.General, 0, "Duration"); //Note: prefer Stream_General if you want the duration of the program (here, you select the duration of the video stream)
                    info.Close();

                    if (double.TryParse(duration, out var value))
                    {
                        return TimeSpan.FromMilliseconds(value);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(Invariant($" Failed to get Length of {filePath} with {ex.Message}"));
            }

            return null;
        }
    }
}