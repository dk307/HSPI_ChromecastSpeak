using System.IO;
using System.Speech.AudioFormat;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using NullGuard;
using System;

namespace Hspi.Voice
{
    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class VoiceGenerator
    {
        public VoiceGenerator(ILogger logger, string text)
        {
            this.logger = logger;
            promptBuilder = new PromptBuilder(System.Globalization.CultureInfo.CurrentCulture);
            promptBuilder.AppendSsmlMarkup(text);
        }

        public async Task<VoiceData> GenerateVoiceAsWavFile(CancellationToken token)
        {
            logger.DebugLog("Starting Generation of Wav using SAPI");
            var audioFormat = new SpeechAudioFormatInfo(44100, AudioBitsPerSample.Sixteen, AudioChannel.Stereo);

            using (var speechSynthesizer = new SpeechSynthesizer())
            {
                using (MemoryStream streamAudio = new MemoryStream())
                {
                    SpeakProgressEventArgs progressEvents = null;
                    speechSynthesizer.SetOutputToWaveStream(streamAudio);
                    speechSynthesizer.SpeakProgress += (sender, e) => { progressEvents = e; };

                    TaskCompletionSource<bool> finished = new TaskCompletionSource<bool>(token);
                    speechSynthesizer.SpeakCompleted += (object sender, SpeakCompletedEventArgs e) =>
                    {
                        finished.SetResult(true);
                    };
                    token.Register(() => speechSynthesizer.SpeakAsyncCancelAll());
                    speechSynthesizer.SpeakAsync(this.promptBuilder);
                    await finished.Task.ConfigureAwait(false);
                    speechSynthesizer.SetOutputToNull();

                    logger.DebugLog("Finished Generation of Wav using SAPI");

                    return new VoiceData(streamAudio.ToArray(), "wav",
                                         progressEvents != null ? (TimeSpan?)progressEvents.AudioPosition : null);
                }
            }
        }

        private readonly PromptBuilder promptBuilder;
        private readonly ILogger logger;
    }
}