using System;
using System.IO;

namespace BF_STT.Services
{
    /// <summary>
    /// Pure C# utility to analyze WAV PCM files for speech content.
    /// Reads 16-bit PCM samples, computes RMS energy per frame,
    /// and determines if enough frames exceed the speech threshold.
    /// </summary>
    public static class AudioSilenceDetector
    {
        // RMS energy threshold for a frame to be considered "speech"
        // ~-36dB normalized. Adjust if needed.
        private const float SpeechEnergyThreshold = 0.015f;

        // Minimum ratio of speech frames to total frames.
        // If less than 5% of frames contain speech, consider the file silent.
        private const float MinSpeechRatio = 0.05f;

        // Frame size in samples (50ms at 16kHz = 800 samples)
        private const int FrameSizeSamples = 800;

        /// <summary>
        /// Analyzes a WAV PCM file and returns true if it contains meaningful speech.
        /// Supports 16-bit PCM WAV files (mono or stereo).
        /// </summary>
        public static bool ContainsSpeech(string wavFilePath)
        {
            if (string.IsNullOrEmpty(wavFilePath) || !File.Exists(wavFilePath))
                return false;

            try
            {
                using var fs = File.OpenRead(wavFilePath);
                using var reader = new BinaryReader(fs);

                // --- Parse WAV header ---
                // RIFF header
                var riff = new string(reader.ReadChars(4));
                if (riff != "RIFF") return false;

                reader.ReadInt32(); // file size
                var wave = new string(reader.ReadChars(4));
                if (wave != "WAVE") return false;

                // Find "fmt " chunk
                short channels = 1;
                int sampleRate = 16000;
                short bitsPerSample = 16;
                bool fmtFound = false;

                while (fs.Position < fs.Length - 8)
                {
                    var chunkId = new string(reader.ReadChars(4));
                    int chunkSize = reader.ReadInt32();

                    if (chunkId == "fmt ")
                    {
                        short audioFormat = reader.ReadInt16(); // 1 = PCM
                        channels = reader.ReadInt16();
                        sampleRate = reader.ReadInt32();
                        reader.ReadInt32(); // byte rate
                        reader.ReadInt16(); // block align
                        bitsPerSample = reader.ReadInt16();

                        // Skip any extra fmt bytes
                        int remaining = chunkSize - 16;
                        if (remaining > 0) reader.ReadBytes(remaining);

                        fmtFound = true;

                        if (audioFormat != 1) return false; // Only support PCM
                    }
                    else if (chunkId == "data")
                    {
                        if (!fmtFound) return false;
                        return AnalyzeData(reader, chunkSize, channels, bitsPerSample);
                    }
                    else
                    {
                        // Skip unknown chunks
                        if (chunkSize > 0 && fs.Position + chunkSize <= fs.Length)
                            reader.ReadBytes(chunkSize);
                        else
                            break;
                    }
                }

                return false; // No data chunk found
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioSilenceDetector] Error: {ex.Message}");
                return true; // On error, err on the side of sending to API
            }
        }

        private static bool AnalyzeData(BinaryReader reader, int dataSize, short channels, short bitsPerSample)
        {
            int bytesPerSample = bitsPerSample / 8;
            int totalSamples = dataSize / (bytesPerSample * channels);

            // Adjust frame size for sample rate differences (assume 16kHz baseline)
            int frameSamples = FrameSizeSamples;

            int totalFrames = 0;
            int speechFrames = 0;

            double frameEnergy = 0;
            int frameSampleCount = 0;

            int samplesRead = 0;
            int maxBytes = dataSize;

            while (samplesRead < totalSamples && maxBytes > 0)
            {
                // Read one sample (first channel only for stereo)
                float sample = 0;
                if (bitsPerSample == 16)
                {
                    if (maxBytes < 2 * channels) break;
                    short raw = reader.ReadInt16();
                    sample = raw / 32768f; // Normalize to [-1.0, 1.0]
                    maxBytes -= 2;

                    // Skip extra channels
                    for (int c = 1; c < channels; c++)
                    {
                        reader.ReadInt16();
                        maxBytes -= 2;
                    }
                }
                else if (bitsPerSample == 8)
                {
                    if (maxBytes < channels) break;
                    byte raw = reader.ReadByte();
                    sample = (raw - 128) / 128f;
                    maxBytes -= 1;

                    for (int c = 1; c < channels; c++)
                    {
                        reader.ReadByte();
                        maxBytes -= 1;
                    }
                }
                else
                {
                    // Unsupported bit depth, assume speech to avoid false negatives
                    return true;
                }

                samplesRead++;

                // Accumulate energy
                frameEnergy += sample * sample;
                frameSampleCount++;

                // End of frame
                if (frameSampleCount >= frameSamples)
                {
                    float rms = (float)Math.Sqrt(frameEnergy / frameSampleCount);
                    totalFrames++;

                    if (rms > SpeechEnergyThreshold)
                    {
                        speechFrames++;
                    }

                    frameEnergy = 0;
                    frameSampleCount = 0;
                }
            }

            // Process last partial frame
            if (frameSampleCount > 0)
            {
                float rms = (float)Math.Sqrt(frameEnergy / frameSampleCount);
                totalFrames++;
                if (rms > SpeechEnergyThreshold)
                {
                    speechFrames++;
                }
            }

            if (totalFrames == 0) return false;

            float speechRatio = (float)speechFrames / totalFrames;
            System.Diagnostics.Debug.WriteLine(
                $"[AudioSilenceDetector] Frames: {totalFrames}, Speech: {speechFrames}, Ratio: {speechRatio:P1}");

            return speechRatio >= MinSpeechRatio;
        }
    }
}
