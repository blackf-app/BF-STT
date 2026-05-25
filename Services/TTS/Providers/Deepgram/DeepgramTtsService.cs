using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BF_STT.Services.TTS.Abstractions;

namespace BF_STT.Services.TTS.Providers.Deepgram
{
    public sealed class DeepgramTtsService : BaseTtsService
    {
        public DeepgramTtsService(HttpClient httpClient, string apiKey, string model, string baseUrl)
            : base(httpClient, apiKey, model, "", baseUrl,
                "aura-2-thalia-en", "", "https://api.deepgram.com/v1/speak")
        {
        }

        protected override async Task<TtsAudioResult> SynthesizeCoreAsync(string text, CancellationToken ct)
        {
            var url = $"{BaseUrl}?model={Uri.EscapeDataString(Model)}&encoding=linear16&container=wav";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Token", ApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(new { text }), Encoding.UTF8, "application/json");

            using var response = await HttpClient.SendAsync(request, ct);
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Deepgram TTS request failed: {response.StatusCode}");
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "audio/wav";
            return new TtsAudioResult(bytes, contentType);
        }
    }
}
