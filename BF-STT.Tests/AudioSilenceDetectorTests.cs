using BF_STT.Services.Audio;
using BF_STT.Tests.Helpers;
using Xunit;

namespace BF_STT.Tests
{
    public class AudioSilenceDetectorTests
    {
        // ─── Null / empty inputs ──────────────────────────────────────────────

        [Fact]
        public void ContainsSpeech_NullData_ReturnsFalse()
        {
            Assert.False(AudioSilenceDetector.ContainsSpeech((byte[])null!));
        }

        [Fact]
        public void ContainsSpeech_EmptyArray_ReturnsFalse()
        {
            Assert.False(AudioSilenceDetector.ContainsSpeech(Array.Empty<byte>()));
        }

        [Fact]
        public void ContainsSpeech_TooShortForHeader_ReturnsFalse()
        {
            // < 44 bytes is less than minimal WAV header
            Assert.False(AudioSilenceDetector.ContainsSpeech(new byte[43]));
        }

        // ─── Invalid WAV ──────────────────────────────────────────────────────

        [Fact]
        public void ContainsSpeech_InvalidRiffHeader_ReturnsFalse()
        {
            // Build 44 bytes that don't start with "RIFF"
            var data = new byte[44];
            Assert.False(AudioSilenceDetector.ContainsSpeech(data));
        }

        [Fact]
        public void ContainsSpeech_GarbageBytes_ReturnsFalse()
        {
            var garbage = WavBuilder.BuildInvalidWav();
            Assert.False(AudioSilenceDetector.ContainsSpeech(garbage));
        }

        // ─── Silent audio ─────────────────────────────────────────────────────

        [Fact]
        public void ContainsSpeech_AllZeroSamples_ReturnsFalse()
        {
            var silent = WavBuilder.BuildSilentWav(durationMs: 500);
            Assert.False(AudioSilenceDetector.ContainsSpeech(silent));
        }

        [Fact]
        public void ContainsSpeech_ShortSilence_ReturnsFalse()
        {
            var silent = WavBuilder.BuildSilentWav(durationMs: 100);
            Assert.False(AudioSilenceDetector.ContainsSpeech(silent));
        }

        // ─── Audio with signal ────────────────────────────────────────────────

        [Fact]
        public void ContainsSpeech_LoudAlternatingSignal_ReturnsTrue()
        {
            var loud = WavBuilder.BuildLoudWav(durationMs: 500);
            Assert.True(AudioSilenceDetector.ContainsSpeech(loud));
        }

        [Fact]
        public void ContainsSpeech_LoudShortSignal_ReturnsTrue()
        {
            var loud = WavBuilder.BuildLoudWav(durationMs: 200);
            Assert.True(AudioSilenceDetector.ContainsSpeech(loud));
        }

        // ─── Stationary noise (AGC-flattened room tone) ──────────────────────

        [Fact]
        public void ContainsSpeech_LongStationaryNoise_ReturnsFalse()
        {
            // Auto Gain Control amplifies a silent room up to 30x, so the recorded
            // WAV is loud but perfectly uniform — no syllabic gaps anywhere.
            var noise = WavBuilder.BuildStationaryNoiseWav(durationMs: 2000);
            Assert.False(AudioSilenceDetector.ContainsSpeech(noise));
        }

        [Fact]
        public void ContainsSpeech_ModulatedSignal_ReturnsTrue()
        {
            // Same overall level as the stationary case, but with loud/quiet
            // alternation like real speech.
            var speech = WavBuilder.BuildModulatedSpeechWav(durationMs: 2000);
            Assert.True(AudioSilenceDetector.ContainsSpeech(speech));
        }

        [Fact]
        public void ContainsSpeech_ShortUniformSignal_ReturnsTrue()
        {
            // Under ~1s there aren't enough frames to judge structure, so the
            // stationarity check must stay out of the way.
            var shortUniform = WavBuilder.BuildStationaryNoiseWav(durationMs: 500);
            Assert.True(AudioSilenceDetector.ContainsSpeech(shortUniform));
        }

        // ─── Edge: exactly 44 bytes (valid header, no data) ──────────────────

        [Fact]
        public void ContainsSpeech_HeaderOnlyNoDataChunk_ReturnsFalse()
        {
            // A WAV with valid RIFF/fmt but no data chunk
            var noData = WavBuilder.BuildWav(Array.Empty<short>());
            Assert.False(AudioSilenceDetector.ContainsSpeech(noData));
        }
    }
}
