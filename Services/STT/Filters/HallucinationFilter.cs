using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace BF_STT.Services.STT.Filters
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
        /// Non-speech audio-event annotations. Providers such as ElevenLabs Scribe emit
        /// these instead of words when the recording holds no speech — bracketed
        /// ("[âm nhạc]", "[jingle]") but sometimes bare. Matched only against a whole
        /// transcript, so dictating one of these words inside a sentence still works.
        /// </summary>
        private static readonly HashSet<string> AudioEventLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            // Vietnamese
            "âm nhạc", "nhạc", "tiếng nhạc", "nhạc nền", "nhạc dạo", "nhạc hiệu",
            "tiếng cười", "tiếng vỗ tay", "vỗ tay", "tiếng ồn", "im lặng", "yên lặng",
            "không có âm thanh", "không nghe rõ", "tiếng động",

            // English
            "music", "musique", "jingle", "intro music", "outro music", "background music",
            "instrumental", "applause", "clapping", "laughter", "laughs", "laughing",
            "silence", "blank_audio", "blank audio", "no speech", "inaudible",
            "coughing", "cough", "sighs", "breathing", "footsteps", "noise",
            "beep", "static", "sound effect", "sound effects", "theme music",
        };

        /// <summary>
        /// Bracket pairs providers use to wrap non-speech annotations.
        /// </summary>
        private static readonly Regex AudioEventTagPattern = new(
            @"[\[\(\{（【][^\[\]\(\)\{\}（）【】]*[\]\)\}）】]",
            RegexOptions.Compiled);

        /// <summary>
        /// Characters that carry no meaning on their own — what's left after stripping
        /// tags must contain something other than these to count as real speech.
        /// </summary>
        private const string FillerChars = " \t\r\n.,!?;:-–—…*\"'“”‘’(){}[]";

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
        /// Removes non-speech audio-event annotations ("[âm nhạc]", "(laughter)") from a
        /// transcript and normalises the leftover whitespace. Mixed content survives:
        /// "[âm nhạc] xin chào" becomes "xin chào". A transcript that was nothing but
        /// tags collapses to an empty string.
        /// </summary>
        public static string Sanitize(string transcript)
        {
            if (string.IsNullOrWhiteSpace(transcript))
                return string.Empty;

            var stripped = AudioEventTagPattern.Replace(transcript, " ");

            // Collapse runs of whitespace introduced by the removal.
            var sb = new StringBuilder(stripped.Length);
            bool lastWasSpace = false;
            foreach (var c in stripped)
            {
                bool isSpace = char.IsWhiteSpace(c);
                if (isSpace)
                {
                    if (!lastWasSpace && sb.Length > 0) sb.Append(' ');
                }
                else
                {
                    sb.Append(c);
                }
                lastWasSpace = isSpace;
            }

            var result = sb.ToString().Trim();

            // Nothing but punctuation left means the transcript was purely annotation.
            return HasMeaningfulContent(result) ? result : string.Empty;
        }

        /// <summary>True when the text holds at least one character that isn't filler.</summary>
        private static bool HasMeaningfulContent(string text) =>
            text.Any(c => !FillerChars.Contains(c));

        /// <summary>
        /// Returns true if the transcript appears to be a Whisper hallucination.
        /// </summary>
        public static bool IsHallucination(string transcript)
        {
            if (string.IsNullOrWhiteSpace(transcript))
                return true;

            // A transcript made up entirely of audio-event tags carries no speech.
            var sanitized = Sanitize(transcript);
            if (string.IsNullOrWhiteSpace(sanitized))
                return true;

            // A bare audio-event label as the whole transcript is an annotation too.
            var label = sanitized.Trim().Trim(FillerChars.ToCharArray());
            if (AudioEventLabels.Contains(label))
                return true;

            // Judge the remaining checks on the tag-free text so annotations can't
            // pad out a hallucination past the length or repetition heuristics.
            var trimmed = sanitized.Trim().TrimEnd('.', ' ', ',', '!', '?');

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
