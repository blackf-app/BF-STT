using System.Net.Http;
using System.Text;
using System.Text.Json;
using BF_STT.Services.TTS.Abstractions;

namespace BF_STT.Services.TTS.Providers.ElevenLabs
{
    public sealed class ElevenLabsTtsService : BaseTtsService
    {
        public ElevenLabsTtsService(HttpClient httpClient, string apiKey, string model, string voice, string baseUrl)
            : base(httpClient, apiKey, model, voice, baseUrl,
                "eleven_multilingual_v2", "JBFqnCBsd6RMkjVDRZzb", "https://api.elevenlabs.io/v1/text-to-speech")
        {
        }

        protected override async Task<TtsAudioResult> SynthesizeCoreAsync(string text, CancellationToken ct)
        {
            var url = $"{BaseUrl}/{Uri.EscapeDataString(Voice)}?output_format=mp3_44100_128";
            var body = new
            {
                text,
                model_id = Model
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("xi-api-key", ApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            using var response = await HttpClient.SendAsync(request, ct);
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ElevenLabs TTS request failed: {response.StatusCode}");
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "audio/mpeg";
            return new TtsAudioResult(bytes, contentType);
        }
    }
}
