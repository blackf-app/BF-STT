using System.IO;
using System.Media;
using System.Text;

namespace BF_STT.Services
{
    public class SoundService
    {
        private readonly byte[] _startSound;
        private readonly byte[] _stopSound;

        public SoundService()
        {
            // Create gentle beep files in memory
            _startSound = GenerateSineWave(880, 150); // A5, 150ms
            _stopSound = GenerateSineWave(440, 150);  // A4, 150ms
        }

        public void PlayStartSound()
        {
            PlaySound(_startSound);
        }

        public void PlayStopSound()
        {
            PlaySound(_stopSound);
        }

        private void PlaySound(byte[] audioData)
        {
            // Clone data to avoid closure issues if reused (though here it'sreadonly)
            var data = audioData; 
            Task.Run(() =>
            {
                using (var ms = new MemoryStream(data))
                using (var player = new SoundPlayer(ms))
                {
                    player.PlaySync(); // Block this background thread until done
                }
            });
        }

        private byte[] GenerateSineWave(int frequency, int durationMs)
        {
            const int sampleRate = 44100;
            const short bitsPerSample = 16;
            const short channels = 1;

            int numSamples = sampleRate * durationMs / 1000;
            int dataSize = numSamples * channels * (bitsPerSample / 8);
            int fileSize = 36 + dataSize;
            
            using (var memoryStream = new MemoryStream())
            using (var writer = new BinaryWriter(memoryStream))
            {
                // WAV Header
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(fileSize);
                writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16); // Subchunk1Size (16 for PCM)
                writer.Write((short)1); // AudioFormat (1 for PCM)
                writer.Write(channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * (bitsPerSample / 8)); // ByteRate
                writer.Write((short)(channels * (bitsPerSample / 8))); // BlockAlign
                writer.Write(bitsPerSample);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataSize);

                // Data
                double amplitude = 4000; // Half volume (was 8000, max 32767)
                double fadeDurationSamples = sampleRate * 0.02; // 20ms fade

                for (int i = 0; i < numSamples; i++)
                {
                    double t = (double)i / sampleRate;
                    double sampleValue = amplitude * Math.Sin(2 * Math.PI * frequency * t);

                    // Fade In
                    if (i < fadeDurationSamples)
                    {
                        sampleValue *= (i / fadeDurationSamples);
                    }
                    // Fade Out
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
}
