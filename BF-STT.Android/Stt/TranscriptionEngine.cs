using System.Net.Http;
using System.Threading.Tasks;

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

            return (text ?? string.Empty).Trim();
        }
    }
}
