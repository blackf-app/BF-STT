using System.Net.Http;
using System.Threading.Tasks;

using BF_STT.Services.Audio;
using BF_STT.Services.STT.Abstractions;
using BF_STT.Services.STT.Filters;
using BF_STT.Services.STT.Providers.Deepgram;
using BF_STT.Services.STT.Providers.ElevenLabs;
using BF_STT.Services.STT.Providers.OpenAI;

namespace BFSTT.Droid.Stt
{
    /// <summary>
    /// Bridges the Android app to the reused desktop STT provider services.
    /// Batch mode only (record fully, then one HTTP call) — matches the tap/tap UX.
    /// </summary>
    public static class TranscriptionEngine
    {
        private static readonly HttpClient Http = new() { Timeout = System.TimeSpan.FromSeconds(60) };

        public static async Task<string> TranscribeAsync(byte[] wav, string provider, string apiKey, string language)
        {
            // Never spend an API call on a recording with no speech — providers answer
            // silence with non-speech annotations ("[âm nhạc]", "[jingle]") rather than
            // an empty string. Android records raw PCM with no AGC, so frame energy is
            // a faithful measure here.
            if (!AudioSilenceDetector.ContainsSpeech(wav))
            {
                return string.Empty;
            }

            IBatchSttService service = provider switch
            {
                "OpenAI" => new OpenAIBatchService(Http, apiKey, "https://api.openai.com/v1/audio/transcriptions"),
                "ElevenLabs" => new ElevenLabsBatchService(Http, apiKey, "https://api.elevenlabs.io/v1/speech-to-text", "scribe_v2"),
                _ => new DeepgramService(Http, apiKey, "https://api.deepgram.com/v1/listen", "nova-3"),
            };

            string text = await service.TranscribeAsync(wav, language);

            if (HallucinationFilter.IsHallucination(text))
            {
                return string.Empty;
            }

            // Strip any audio-event tags mixed in with real speech.
            return HallucinationFilter.Sanitize(text);
        }
    }
}
