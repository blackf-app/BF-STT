using BF_STT.Services.STT.Filters;
using Xunit;

namespace BF_STT.Tests
{
    public class HallucinationFilterTests
    {
        // ─── Empty / whitespace ───────────────────────────────────────────────

        [Fact]
        public void IsHallucination_Null_ReturnsTrue()
        {
            Assert.True(HallucinationFilter.IsHallucination(null!));
        }

        [Fact]
        public void IsHallucination_EmptyString_ReturnsTrue()
        {
            Assert.True(HallucinationFilter.IsHallucination(""));
        }

        [Fact]
        public void IsHallucination_WhitespaceOnly_ReturnsTrue()
        {
            Assert.True(HallucinationFilter.IsHallucination("   "));
        }

        // ─── Too short ────────────────────────────────────────────────────────

        [Fact]
        public void IsHallucination_SingleChar_ReturnsTrue()
        {
            Assert.True(HallucinationFilter.IsHallucination("x"));
        }

        // ─── Known hallucination phrases ──────────────────────────────────────

        [Theory]
        [InlineData("Cảm ơn")]
        [InlineData("Cảm ơn đã xem")]
        [InlineData("Thank you for watching")]
        [InlineData("Thanks for watching")]
        [InlineData("Xin chào")]
        [InlineData("Goodbye")]
        [InlineData("...")]
        public void IsHallucination_KnownPhrase_ReturnsTrue(string phrase)
        {
            Assert.True(HallucinationFilter.IsHallucination(phrase));
        }

        [Theory]
        [InlineData("Cảm ơn.")]      // trailing period stripped before matching
        [InlineData("Cảm ơn ")]      // trailing space stripped
        [InlineData("Xin chào!")]    // trailing ! stripped
        public void IsHallucination_KnownPhraseWithPunctuation_ReturnsTrue(string phrase)
        {
            Assert.True(HallucinationFilter.IsHallucination(phrase));
        }

        // ─── Known hallucination substrings ───────────────────────────────────

        [Theory]
        [InlineData("subscribe to my channel")]
        [InlineData("please subscribe")]
        [InlineData("subtitles by someone")]
        [InlineData("translated by john")]
        [InlineData("visit www.example.com")]
        public void IsHallucination_ContainsHallucinationSubstring_ReturnsTrue(string text)
        {
            Assert.True(HallucinationFilter.IsHallucination(text));
        }

        // ─── Repetitive text ─────────────────────────────────────────────────

        [Fact]
        public void IsHallucination_RepetitiveWord_ReturnsTrue()
        {
            // "you" repeated 5x = 100% dominance
            Assert.True(HallucinationFilter.IsHallucination("you you you you you"));
        }

        [Fact]
        public void IsHallucination_HighlyRepetitive_ReturnsTrue()
        {
            // "Cảm ơn" repeated = >70% dominance
            Assert.True(HallucinationFilter.IsHallucination("Cảm Cảm Cảm Cảm ơn"));
        }

        // ─── Normal speech — should NOT be filtered ───────────────────────────

        [Theory]
        [InlineData("Hôm nay thời tiết đẹp lắm")]
        [InlineData("Tôi muốn đặt lịch họp vào thứ Hai")]
        [InlineData("Hello, how are you doing today?")]
        [InlineData("Please open the document and review the changes")]
        [InlineData("The meeting is scheduled for three o'clock")]
        public void IsHallucination_NormalSpeech_ReturnsFalse(string text)
        {
            Assert.False(HallucinationFilter.IsHallucination(text));
        }

        [Fact]
        public void IsHallucination_TwoUniqueWords_ReturnsFalse()
        {
            // Less than 3 words → repetition check skipped; not in known phrases
            Assert.False(HallucinationFilter.IsHallucination("hello world"));
        }
    }
}
