using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BF_STT.Services.TTS.Abstractions;

namespace BF_STT.Services.TTS.Providers.Soniox
{
    public sealed class SonioxTtsService : BaseTtsService
    {
        public SonioxTtsService(HttpClient httpClient, string apiKey, string model, string voice, string baseUrl)
            : base(httpClient, apiKey, model, voice, baseUrl,
                "tts-rt-v1", "Adrian", "https://tts-rt.soniox.com/tts")
        {
        }

        protected override async Task<TtsAudioResult> SynthesizeCoreAsync(string text, CancellationToken ct)
        {
            var body = new
            {
                model = Model,
                language = "vi",
                voice = Voice,
                // Soniox REST returns a streamed audio body. MP3 is more tolerant here than WAV,
                // whose header lengths can be awkward for in-memory parsing after streaming.
                audio_format = "mp3",
                text
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            using var response = await HttpClient.SendAsync(request, ct);
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = TrimResponseBody(bytes);
                throw new HttpRequestException($"Soniox TTS request failed: {response.StatusCode}. {errorBody}");
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "audio/mpeg";
            return new TtsAudioResult(bytes, contentType);
        }

        private static string TrimResponseBody(byte[] bytes)
        {
            if (bytes.Length == 0)
            {
                return "Empty response body.";
            }

            var text = Encoding.UTF8.GetString(bytes);
            text = text.ReplaceLineEndings(" ").Trim();
            if (text.Length > 300)
            {
                text = text[..300] + "...";
            }

            return text;
        }
    }
}
