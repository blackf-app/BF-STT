using System.IO;
using System.Text;

namespace BFSTT.Droid.Audio
{
    /// <summary>
    /// Wraps raw 16-bit little-endian PCM into a canonical 44-byte-header WAV container,
    /// which every STT provider in the core accepts as "audio/wav".
    /// </summary>
    public static class WavWriter
    {
        public static byte[] Pcm16ToWav(byte[] pcm, int sampleRate, int channels)
        {
            int bitsPerSample = 16;
            int byteRate = sampleRate * channels * bitsPerSample / 8;
            int blockAlign = channels * bitsPerSample / 8;

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);

            // RIFF header
            w.Write(Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + pcm.Length);
            w.Write(Encoding.ASCII.GetBytes("WAVE"));

            // fmt chunk
            w.Write(Encoding.ASCII.GetBytes("fmt "));
            w.Write(16);                        // PCM fmt chunk size
            w.Write((short)1);                  // audio format = PCM
            w.Write((short)channels);
            w.Write(sampleRate);
            w.Write(byteRate);
            w.Write((short)blockAlign);
            w.Write((short)bitsPerSample);

            // data chunk
            w.Write(Encoding.ASCII.GetBytes("data"));
            w.Write(pcm.Length);
            w.Write(pcm);

            w.Flush();
            return ms.ToArray();
        }
    }
}
