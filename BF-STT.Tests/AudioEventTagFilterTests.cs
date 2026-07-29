using BF_STT.Services.STT.Filters;
using Xunit;

namespace BF_STT.Tests
{
    /// <summary>
    /// Covers the non-speech annotation handling added after ElevenLabs Scribe
    /// returned "[âm nhạc]. [jingle]. [intro music]." for a recording with no speech.
    /// </summary>
    public class AudioEventTagFilterTests
    {
        // ─── Transcripts that are nothing but audio-event tags ────────────────

        [Theory]
        [InlineData("[âm nhạc].")]
        [InlineData("[jingle].")]
        [InlineData("[intro music].")]
        [InlineData("[âm nhạc]. [jingle]. [intro music].")]
        [InlineData("(laughter)")]
        [InlineData("(music) (applause)")]
        [InlineData("[BLANK_AUDIO]")]
        [InlineData("【音楽】")]
        [InlineData("（笑）")]
        [InlineData("{noise}")]
        public void IsHallucination_OnlyAudioEventTags_ReturnsTrue(string transcript)
        {
            Assert.True(HallucinationFilter.IsHallucination(transcript));
        }

        // ─── Bare audio-event labels as the whole transcript ──────────────────

        [Theory]
        [InlineData("âm nhạc")]
        [InlineData("Âm nhạc.")]
        [InlineData("music")]
        [InlineData("Intro music")]
        [InlineData("silence")]
        [InlineData("inaudible")]
        public void IsHallucination_BareAudioEventLabel_ReturnsTrue(string transcript)
        {
            Assert.True(HallucinationFilter.IsHallucination(transcript));
        }

        // ─── Real speech must survive ─────────────────────────────────────────

        [Theory]
        [InlineData("Tôi muốn nghe âm nhạc vào buổi sáng")]
        [InlineData("Bật nhạc lên giúp tôi")]
        [InlineData("Play some intro music for the video")]
        public void IsHallucination_AudioEventWordInsideSentence_ReturnsFalse(string transcript)
        {
            Assert.False(HallucinationFilter.IsHallucination(transcript));
        }

        [Fact]
        public void IsHallucination_SpeechMixedWithTag_ReturnsFalse()
        {
            Assert.False(HallucinationFilter.IsHallucination("[âm nhạc] Xin chào các bạn hôm nay"));
        }

        // ─── Sanitize ─────────────────────────────────────────────────────────

        [Fact]
        public void Sanitize_OnlyTags_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, HallucinationFilter.Sanitize("[âm nhạc]. [jingle]."));
        }

        [Fact]
        public void Sanitize_StripsTagAndKeepsSpeech()
        {
            Assert.Equal(
                "Xin chào các bạn",
                HallucinationFilter.Sanitize("[âm nhạc] Xin chào các bạn"));
        }

        [Fact]
        public void Sanitize_StripsTagInTheMiddle()
        {
            Assert.Equal(
                "Hôm nay trời đẹp",
                HallucinationFilter.Sanitize("Hôm nay (laughter) trời đẹp"));
        }

        [Fact]
        public void Sanitize_CollapsesWhitespace()
        {
            Assert.Equal("một hai", HallucinationFilter.Sanitize("một   \n hai"));
        }

        [Fact]
        public void Sanitize_PlainSpeech_IsUnchanged()
        {
            Assert.Equal("Xin chào các bạn", HallucinationFilter.Sanitize("Xin chào các bạn"));
        }

        [Fact]
        public void Sanitize_NullOrWhitespace_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, HallucinationFilter.Sanitize(null!));
            Assert.Equal(string.Empty, HallucinationFilter.Sanitize("   "));
        }
    }
}
