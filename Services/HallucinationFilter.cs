using System;
using System.Collections.Generic;
using System.Linq;

namespace BF_STT.Services
{
    /// <summary>
    /// Filters out common Whisper hallucination patterns.
    /// When audio contains silence or noise, Whisper often generates
    /// repetitive or well-known phrases that are not actual speech.
    /// </summary>
    public static class HallucinationFilter
    {
        /// <summary>
        /// Known hallucination phrases that Whisper commonly generates
        /// from silent or noisy audio. Case-insensitive matching.
        /// </summary>
        private static readonly HashSet<string> HallucinationPhrases = new(StringComparer.OrdinalIgnoreCase)
        {
            // Vietnamese hallucinations
            "Cảm ơn đã xem",
            "Cảm ơn các bạn đã xem",
            "Cảm ơn các bạn đã theo dõi",
            "Hẹn gặp lại",
            "Hẹn gặp lại các bạn",
            "Đăng ký kênh",
            "Nhớ đăng ký kênh",
            "Xin chào",
            "Xin cảm ơn",
            "Tạm biệt",
            "Cảm ơn",
            "Chào các bạn",

            // English hallucinations  
            "Thank you for watching",
            "Thanks for watching",
            "Thank you",
            "Subscribe",
            "Please subscribe",
            "Like and subscribe",
            "See you next time",
            "Goodbye",
            "Thank you so much",
            "Thanks for listening",
            "Please like and subscribe",

            // Other common patterns
            "...",
            "Subtitles by",
            "Translated by",
            "Copyright",
        };

        /// <summary>
        /// Substrings that indicate hallucination when found within a transcript.
        /// </summary>
        private static readonly string[] HallucinationSubstrings = new[]
        {
            "đăng ký kênh",
            "subscribe",
            "subtitles by",
            "translated by",
            "amara.org",
            "www.",
        };

        /// <summary>
        /// Returns true if the transcript appears to be a Whisper hallucination.
        /// </summary>
        public static bool IsHallucination(string transcript)
        {
            if (string.IsNullOrWhiteSpace(transcript))
                return true;

            var trimmed = transcript.Trim().TrimEnd('.', ' ', ',', '!', '?');

            // Too short to be real speech (less than 2 meaningful chars)
            if (trimmed.Length < 2)
                return true;

            // Exact match with known hallucination phrases
            if (HallucinationPhrases.Contains(trimmed))
                return true;

            // Check for hallucination substrings
            var lower = trimmed.ToLowerInvariant();
            if (HallucinationSubstrings.Any(s => lower.Contains(s, StringComparison.OrdinalIgnoreCase)))
                return true;

            // Repetition detection: same word/phrase repeated many times
            // e.g. "Cảm ơn Cảm ơn Cảm ơn" or "you you you you"
            if (IsRepetitive(trimmed))
                return true;

            return false;
        }

        /// <summary>
        /// Detects if the text is mostly repetitive (same token repeated).
        /// </summary>
        private static bool IsRepetitive(string text)
        {
            var words = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            if (words.Length < 3) return false;

            // Check if a single word makes up >70% of the text
            var wordCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var word in words)
            {
                wordCounts.TryGetValue(word, out int count);
                wordCounts[word] = count + 1;
            }

            int maxCount = wordCounts.Values.Max();
            float dominance = (float)maxCount / words.Length;

            // If one word appears in >70% of positions, it's likely repetitive hallucination
            return dominance > 0.7f && words.Length >= 3;
        }
    }
}
