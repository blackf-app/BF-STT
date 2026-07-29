using System.IO;

namespace BF_STT.Tests.Helpers
{
    /// <summary>Builds minimal in-memory WAV files for unit tests.</summary>
    internal static class WavBuilder
    {
        // ── Public factory methods ───────────────────────────────────────────────

        /// <summary>WAV with all-zero samples (pure silence).</summary>
        public static byte[] BuildSilentWav(int durationMs = 500, int sampleRate = 16000)
        {
            int sampleCount = sampleRate * durationMs / 1000;
            return BuildWav(new short[sampleCount]);
        }

        /// <summary>WAV with alternating max/min samples (loud signal).</summary>
        public static byte[] BuildLoudWav(int durationMs = 500, int sampleRate = 16000)
        {
            int sampleCount = sampleRate * durationMs / 1000;
            var samples = new short[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                samples[i] = (i % 2 == 0) ? short.MaxValue : short.MinValue;
            return BuildWav(samples);
        }

        /// <summary>
        /// WAV whose every frame has the same RMS — what AGC turns room tone into.
        /// Loud enough to clear the energy threshold, but with no syllabic structure.
        /// </summary>
        public static byte[] BuildStationaryNoiseWav(
            int durationMs = 2000, float amplitude = 0.2f, int sampleRate = 16000)
        {
            int sampleCount = sampleRate * durationMs / 1000;
            short level = (short)(amplitude * short.MaxValue);
            var samples = new short[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                samples[i] = (i % 2 == 0) ? level : (short)-level;
            return BuildWav(samples, sampleRate);
        }

        /// <summary>
        /// WAV alternating loud and quiet stretches, mimicking the syllabic gaps of
        /// real speech. Same overall level as <see cref="BuildStationaryNoiseWav"/>.
        /// </summary>
        public static byte[] BuildModulatedSpeechWav(
            int durationMs = 2000, int segmentMs = 250, int sampleRate = 16000)
        {
            int sampleCount = sampleRate * durationMs / 1000;
            int segmentSamples = sampleRate * segmentMs / 1000;
            short loud = (short)(0.3f * short.MaxValue);
            short quiet = (short)(0.002f * short.MaxValue);

            var samples = new short[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                bool inLoudSegment = (i / segmentSamples) % 2 == 0;
                short level = inLoudSegment ? loud : quiet;
                samples[i] = (i % 2 == 0) ? level : (short)-level;
            }
            return BuildWav(samples, sampleRate);
        }

        /// <summary>Raw bytes that are not a valid WAV file (no RIFF header).</summary>
        public static byte[] BuildInvalidWav()
        {
            var data = new byte[100];
            for (int i = 0; i < data.Length; i++) data[i] = (byte)(i % 256);
            return data;
        }

        /// <summary>Builds a valid PCM WAV from the supplied 16-bit samples at 16 kHz mono.</summary>
        public static byte[] BuildWav(short[] samples, int sampleRate = 16000)
        {
            const int channels = 1;
            const int bitsPerSample = 16;
            int byteRate = sampleRate * channels * bitsPerSample / 8;
            int blockAlign = channels * bitsPerSample / 8;
            int dataBytes = samples.Length * 2;

            using var ms = new MemoryStream();
            using var w = new BinaryWriter(ms);

            // RIFF chunk
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + dataBytes);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            // fmt sub-chunk
            w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            w.Write(16);
            w.Write((short)1);        // PCM
            w.Write((short)channels);
            w.Write(sampleRate);
            w.Write(byteRate);
            w.Write((short)blockAlign);
            w.Write((short)bitsPerSample);

            // data sub-chunk
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(dataBytes);
            foreach (var s in samples) w.Write(s);

            return ms.ToArray();
        }
    }
}
