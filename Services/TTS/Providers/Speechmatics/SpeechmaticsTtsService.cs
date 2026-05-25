using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BF_STT.Services.TTS.Abstractions;

namespace BF_STT.Services.TTS.Providers.Speechmatics
{
    public sealed class SpeechmaticsTtsService : BaseTtsService
    {
        public SpeechmaticsTtsService(HttpClient httpClient, string apiKey, string voice, string baseUrl)
            : base(httpClient, apiKey, "", voice, baseUrl,
                "", "sarah", "https://preview.tts.speechmatics.com/generate")
        {
        }

        protected override async Task<TtsAudioResult> SynthesizeCoreAsync(string text, CancellationToken ct)
        {
            var url = $"{BaseUrl}/{Uri.EscapeDataString(Voice)}?output_format=wav_16000";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(new { text }), Encoding.UTF8, "application/json");

            using var response = await HttpClient.SendAsync(request, ct);
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = TrimResponseBody(bytes);
                throw new HttpRequestException($"Speechmatics TTS request failed: {response.StatusCode}. {errorBody}");
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "audio/wav";
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
