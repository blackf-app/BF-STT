using System.IO;
using NAudio.Wave;

namespace BF_STT.Services.TTS
{
    public sealed class TtsPlaybackService
    {
        public async Task PlayAsync(byte[] audioData, string contentType, CancellationToken ct = default)
        {
            if (audioData.Length == 0)
            {
                throw new ArgumentException("TTS audio data is empty.", nameof(audioData));
            }

            var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var stream = new MemoryStream(audioData);
            WaveStream reader;

            if (IsWave(contentType, audioData))
            {
                reader = new WaveFileReader(stream);
            }
            else if (IsMp3(contentType, audioData))
            {
                reader = new Mp3FileReader(stream);
            }
            else
            {
                stream.Dispose();
                throw new InvalidOperationException($"Unsupported TTS audio format: {contentType}");
            }

            var output = new WaveOutEvent();
            output.Init(reader);
            output.PlaybackStopped += (_, args) =>
            {
                output.Dispose();
                reader.Dispose();
                stream.Dispose();

                if (args.Exception != null)
                {
                    completion.TrySetException(args.Exception);
                }
                else
                {
                    completion.TrySetResult(null);
                }
            };

            using var registration = ct.Register(() =>
            {
                output.Stop();
                completion.TrySetCanceled(ct);
            });

            output.Play();
            await completion.Task;
        }

        private static bool IsWave(string contentType, byte[] audioData)
        {
            return contentType.Contains("wav", StringComparison.OrdinalIgnoreCase)
                || (audioData.Length >= 4
                    && audioData[0] == 'R'
                    && audioData[1] == 'I'
                    && audioData[2] == 'F'
                    && audioData[3] == 'F');
        }

        private static bool IsMp3(string contentType, byte[] audioData)
        {
            return contentType.Contains("mpeg", StringComparison.OrdinalIgnoreCase)
                || contentType.Contains("mp3", StringComparison.OrdinalIgnoreCase)
                || (audioData.Length >= 3
                    && audioData[0] == 'I'
                    && audioData[1] == 'D'
                    && audioData[2] == '3');
        }
    }
}
