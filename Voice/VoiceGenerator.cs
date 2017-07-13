using NullGuard;
using System;
using System.Diagnostics;
using System.IO;
using System.Speech.AudioFormat;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using Hspi.Exceptions;

namespace Hspi.Voice
{
    using static System.FormattableString;

    [NullGuard(ValidationFlags.Arguments | ValidationFlags.NonPublic)]
    internal class VoiceGenerator
    {
        public VoiceGenerator(ILogger logger, string text, [AllowNull]string sapiVoiceName)
        {
            this.sapiVoiceName = sapiVoiceName;
            this.logger = logger;
            promptBuilder = new PromptBuilder(System.Globalization.CultureInfo.CurrentCulture);
            promptBuilder.AppendSsmlMarkup(text);
        }

        public async Task<VoiceData> GenerateVoiceAsWavFile(CancellationToken token)
        {
            Trace.WriteLine("Starting Generation of Wav using SAPI");
            var audioFormat = new SpeechAudioFormatInfo(44100, AudioBitsPerSample.Sixteen, AudioChannel.Stereo);

            using (var speechSynthesizer = new SpeechSynthesizer())
            {
                SelectVoice(speechSynthesizer);

                using (MemoryStream streamAudio = new MemoryStream())
                {
                    SpeakProgressEventArgs progressEvents = null;
                    speechSynthesizer.SetOutputToWaveStream(streamAudio);
                    speechSynthesizer.SpeakProgress += (sender, e) => { progressEvents = e; };

                    TaskCompletionSource<bool> finished = new TaskCompletionSource<bool>(token);
                    speechSynthesizer.SpeakCompleted += (object sender, SpeakCompletedEventArgs e) =>
                    {
                        if (e.Error != null)
                        {
                            finished.TrySetException(e.Error);
                        }
                        else if (e.Cancelled)
                        {
                            finished.TrySetCanceled();
                        }
                        else
                        {
                            finished.TrySetResult(true);
                        }
                    };
                    token.Register(() => speechSynthesizer.SpeakAsyncCancelAll());
                    try
                    {
                        var prompt = speechSynthesizer.SpeakAsync(promptBuilder);
                        await finished.Task.ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        throw new VoiceGenerationException(Invariant($"Voice Generation Failed with {ex.GetFullMessage()}"));
                    }

                    speechSynthesizer.SetOutputToNull();

                    Trace.WriteLine("Finished Generation of Wav using SAPI");

                    return new VoiceData(streamAudio.ToArray(), "wav",
                                         progressEvents != null ? (TimeSpan?)progressEvents.AudioPosition : null);
                }
            }
        }

        private void SelectVoice(SpeechSynthesizer speechSynthesizer)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(sapiVoiceName))
                {
                    speechSynthesizer.SelectVoice(sapiVoiceName);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(Invariant($"Failed to select Voice {sapiVoiceName} with {ExceptionHelper.GetFullMessage(ex)}"));
            }
        }

        private readonly ILogger logger;
        private readonly PromptBuilder promptBuilder;
        private readonly string sapiVoiceName;
    }
}