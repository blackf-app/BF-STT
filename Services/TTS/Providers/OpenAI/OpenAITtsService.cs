using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BF_STT.Services.TTS.Abstractions;

namespace BF_STT.Services.TTS.Providers.OpenAI
{
    public sealed class OpenAITtsService : BaseTtsService
    {
        public OpenAITtsService(HttpClient httpClient, string apiKey, string model, string voice, string baseUrl)
            : base(httpClient, apiKey, model, voice, baseUrl,
                "gpt-4o-mini-tts", "alloy", "https://api.openai.com/v1/audio/speech")
        {
        }

        protected override async Task<TtsAudioResult> SynthesizeCoreAsync(string text, CancellationToken ct)
        {
            var body = new
            {
                model = Model,
                voice = Voice,
                input = text,
                response_format = "wav"
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            using var response = await HttpClient.SendAsync(request, ct);
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"OpenAI TTS request failed: {response.StatusCode}");
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "audio/wav";
            return new TtsAudioResult(bytes, contentType);
        }
    }
}
