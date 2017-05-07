using System.IO;
using System.Speech.AudioFormat;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using NullGuard;

namespace Hspi.Voice
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class VoiceGenerator
    {
        public VoiceGenerator(ILogger logger, string text)
        {
            this.logger = logger;
            promptBuilder = new PromptBuilder(System.Globalization.CultureInfo.CurrentCulture);
            promptBuilder.AppendText(text);
        }

        public async Task<MemoryStream> GenerateVoiceAsWavFile(CancellationToken token)
        {
            logger.DebugLog("Starting Generation of Wav using SAPI");
            var audioFormat = new SpeechAudioFormatInfo(16000, AudioBitsPerSample.Eight, AudioChannel.Mono);

            using (var speechSynthesizer = new SpeechSynthesizer())
            {
                MemoryStream streamAudio = new MemoryStream();
                speechSynthesizer.SetOutputToWaveStream(streamAudio);

                TaskCompletionSource<bool> finished = new TaskCompletionSource<bool>(token);
                speechSynthesizer.SpeakCompleted += (object sender, SpeakCompletedEventArgs e) =>
                {
                    finished.SetResult(true);
                };
                speechSynthesizer.SpeakAsync(this.promptBuilder);
                await finished.Task.ConfigureAwait(false);
                speechSynthesizer.SetOutputToNull();

                logger.DebugLog("Finished Generation of Wav using SAPI");

                return streamAudio;
            }
        }

        private readonly PromptBuilder promptBuilder;
        private readonly ILogger logger;
    }
}