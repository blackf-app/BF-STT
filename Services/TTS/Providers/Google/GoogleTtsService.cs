using System.Net.Http;
using System.Text;
using System.Text.Json;
using BF_STT.Services.TTS.Abstractions;

namespace BF_STT.Services.TTS.Providers.Google
{
    public sealed class GoogleTtsService : BaseTtsService
    {
        public GoogleTtsService(HttpClient httpClient, string apiKey, string voice, string baseUrl)
            : base(httpClient, apiKey, "", voice, baseUrl,
                "", "vi-VN-Standard-A", "https://texttospeech.googleapis.com/v1/text:synthesize")
        {
        }

        protected override async Task<TtsAudioResult> SynthesizeCoreAsync(string text, CancellationToken ct)
        {
            var languageCode = Voice.Contains('-') ? string.Join("-", Voice.Split('-').Take(2)) : "vi-VN";
            var body = new
            {
                input = new { text },
                voice = new
                {
                    languageCode,
                    name = Voice
                },
                audioConfig = new
                {
                    audioEncoding = "LINEAR16"
                }
            };

            var url = $"{BaseUrl}?key={Uri.EscapeDataString(ApiKey)}";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            using var response = await HttpClient.SendAsync(request, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Google TTS request failed: {response.StatusCode}");
            }

            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("audioContent", out var audioContent))
            {
                throw new InvalidOperationException("Google TTS response did not include audio content.");
            }

            return new TtsAudioResult(Convert.FromBase64String(audioContent.GetString() ?? ""), "audio/wav");
        }
    }
}
