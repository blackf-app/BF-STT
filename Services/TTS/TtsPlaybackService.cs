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

            if (IsWave(contentType, audioData))
            {
                NormalizeWaveHeader(audioData);
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
                    && audioData[2] == '3')
                || LooksLikeMp3Frame(audioData);
        }

        private static bool LooksLikeMp3Frame(byte[] audioData)
        {
            if (audioData.Length < 2)
            {
                return false;
            }

            return audioData[0] == 0xFF && (audioData[1] & 0xE0) == 0xE0;
        }

        private static void NormalizeWaveHeader(byte[] audioData)
        {
            if (audioData.Length < 44)
            {
                return;
            }

            if (!(audioData[0] == 'R' && audioData[1] == 'I' && audioData[2] == 'F' && audioData[3] == 'F'))
            {
                return;
            }

            if (!(audioData[8] == 'W' && audioData[9] == 'A' && audioData[10] == 'V' && audioData[11] == 'E'))
            {
                return;
            }

            WriteInt32LittleEndian(audioData, 4, audioData.Length - 8);

            int offset = 12;
            while (offset + 8 <= audioData.Length)
            {
                int chunkDataOffset = offset + 8;
                int declaredSize = ReadInt32LittleEndian(audioData, offset + 4);
                if (declaredSize < 0)
                {
                    declaredSize = 0;
                }

                bool isDataChunk =
                    audioData[offset] == 'd' &&
                    audioData[offset + 1] == 'a' &&
                    audioData[offset + 2] == 't' &&
                    audioData[offset + 3] == 'a';

                if (isDataChunk)
                {
                    int remaining = Math.Max(0, audioData.Length - chunkDataOffset);
                    int normalizedSize = Math.Min(declaredSize == 0 ? remaining : declaredSize, remaining);
                    WriteInt32LittleEndian(audioData, offset + 4, normalizedSize);
                    break;
                }

                int advance = 8 + declaredSize;
                if ((declaredSize & 1) == 1)
                {
                    advance++;
                }

                if (advance <= 0)
                {
                    break;
                }

                offset += advance;
            }
        }

        private static int ReadInt32LittleEndian(byte[] data, int offset)
        {
            return data[offset]
                | (data[offset + 1] << 8)
                | (data[offset + 2] << 16)
                | (data[offset + 3] << 24);
        }

        private static void WriteInt32LittleEndian(byte[] data, int offset, int value)
        {
            data[offset] = (byte)(value & 0xFF);
            data[offset + 1] = (byte)((value >> 8) & 0xFF);
            data[offset + 2] = (byte)((value >> 16) & 0xFF);
            data[offset + 3] = (byte)((value >> 24) & 0xFF);
        }
    }
}
