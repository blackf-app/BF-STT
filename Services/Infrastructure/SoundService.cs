using OpenTK.Audio.OpenAL;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace BF_STT.Services.Infrastructure
{
    /// <summary>
    /// Cross-platform sound service.
    /// On macOS: plays WAV files via afplay to avoid OpenAL context conflicts with capture.
    /// On Windows: plays via OpenAL.
    /// </summary>
    public class SoundService
    {
        private readonly byte[] _startSound;
        private readonly byte[] _stopSound;

        public SoundService()
        {
            _startSound = GenerateSineWave(880, 150);
            _stopSound = GenerateSineWave(440, 150);
        }

        public void PlayStartSound() => PlaySound(_startSound);
        public void PlayStopSound() => PlaySound(_stopSound);

        private static void PlaySound(byte[] wavBytes)
        {
            Task.Run(() =>
            {
                // On macOS, use afplay instead of OpenAL to avoid concurrent-context
                // crashes: Apple's OpenAL is not thread-safe when a playback context is
                // created/destroyed while a capture device is active on another thread.
                if (OperatingSystem.IsMacOS())
                {
                    PlaySoundMacOS(wavBytes);
                    return;
                }

                PlaySoundOpenAL(wavBytes);
            });
        }

        private static void PlaySoundMacOS(byte[] wavBytes)
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"bfstt_{Path.GetRandomFileName()}.wav");
            try
            {
                File.WriteAllBytes(tempFile, wavBytes);
                using var process = Process.Start(new ProcessStartInfo("afplay", $"\"{tempFile}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                process?.WaitForExit(2000);
            }
            catch (Exception ex)
            {
                Serilog.Log.Debug(ex, "afplay sound failed");
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }

        private static void PlaySoundOpenAL(byte[] wavBytes)
        {
            ALDevice device = default;
            ALContext context = default;
            int buffer = 0;
            int source = 0;
            try
            {
                device = ALC.OpenDevice(null);
                if (device.Handle == IntPtr.Zero) return;
                context = ALC.CreateContext(device, (int[]?)null);
                ALC.MakeContextCurrent(context);

                // Strip 44-byte WAV header from our generator output and load as PCM.
                int headerSize = 44;
                int pcmLen = wavBytes.Length - headerSize;
                if (pcmLen <= 0) return;

                var pcm = new byte[pcmLen];
                Array.Copy(wavBytes, headerSize, pcm, 0, pcmLen);

                buffer = AL.GenBuffer();
                AL.BufferData(buffer, ALFormat.Mono16, pcm, 44100);

                source = AL.GenSource();
                AL.Source(source, ALSourcei.Buffer, buffer);
                AL.SourcePlay(source);

                AL.GetSource(source, ALGetSourcei.SourceState, out int state);
                while ((ALSourceState)state == ALSourceState.Playing)
                {
                    Thread.Sleep(20);
                    AL.GetSource(source, ALGetSourcei.SourceState, out state);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Debug(ex, "OpenAL playback failed");
            }
            finally
            {
                try { if (source != 0) AL.DeleteSource(source); } catch { }
                try { if (buffer != 0) AL.DeleteBuffer(buffer); } catch { }
                try
                {
                    if (context.Handle != IntPtr.Zero)
                    {
                        ALC.MakeContextCurrent(ALContext.Null);
                        ALC.DestroyContext(context);
                    }
                }
                catch { }
                try { if (device.Handle != IntPtr.Zero) ALC.CloseDevice(device); } catch { }
            }
        }

        private static byte[] GenerateSineWave(int frequency, int durationMs)
        {
            const int sampleRate = 44100;
            const short bitsPerSample = 16;
            const short channels = 1;

            int numSamples = sampleRate * durationMs / 1000;
            int dataSize = numSamples * channels * (bitsPerSample / 8);
            int fileSize = 36 + dataSize;

            using var memoryStream = new MemoryStream();
            using var writer = new BinaryWriter(memoryStream);

            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(fileSize);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * (bitsPerSample / 8));
            writer.Write((short)(channels * (bitsPerSample / 8)));
            writer.Write(bitsPerSample);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);

            double amplitude = 4000;
            double fadeDurationSamples = sampleRate * 0.02;

            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / sampleRate;
                double sampleValue = amplitude * Math.Sin(2 * Math.PI * frequency * t);

                if (i < fadeDurationSamples)
                {
                    sampleValue *= (i / fadeDurationSamples);
                }
                else if (i > numSamples - fadeDurationSamples)
                {
                    sampleValue *= ((numSamples - i) / fadeDurationSamples);
                }

                writer.Write((short)sampleValue);
            }

            return memoryStream.ToArray();
        }
    }
}
