using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NullGuard;

namespace Hspi.Voice
{
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

            return new VoiceData(data, Path.GetExtension(filePath).Replace(".", string.Empty), null);
        }
    }
}