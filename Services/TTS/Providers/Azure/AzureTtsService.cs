using System.Net.Http;
using System.Text;
using BF_STT.Services.TTS.Abstractions;

namespace BF_STT.Services.TTS.Providers.Azure
{
    public sealed class AzureTtsService : BaseTtsService
    {
        public AzureTtsService(HttpClient httpClient, string apiKey, string voice, string region)
            : base(httpClient, apiKey, "", voice, region,
                "", "vi-VN-HoaiMyNeural", "eastus")
        {
        }

        protected override async Task<TtsAudioResult> SynthesizeCoreAsync(string text, CancellationToken ct)
        {
            var region = ExtractRegion(BaseUrl);
            var url = $"https://{region}.tts.speech.microsoft.com/cognitiveservices/v1";
            var ssml =
                "<speak version=\"1.0\" xml:lang=\"vi-VN\">" +
                $"<voice xml:lang=\"vi-VN\" name=\"{SecurityElementEscape(Voice)}\">" +
                SecurityElementEscape(text) +
                "</voice></speak>";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("Ocp-Apim-Subscription-Key", ApiKey);
            request.Headers.Add("X-Microsoft-OutputFormat", "riff-16khz-16bit-mono-pcm");
            request.Headers.Add("User-Agent", "BF-STT");
            request.Content = new StringContent(ssml, Encoding.UTF8, "application/ssml+xml");

            using var response = await HttpClient.SendAsync(request, ct);
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Azure TTS request failed: {response.StatusCode}");
            }

            return new TtsAudioResult(bytes, "audio/wav");
        }

        private static string ExtractRegion(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "eastus";
            }

            if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                return uri.Host.Split('.')[0];
            }

            return value.Trim();
        }

        private static string SecurityElementEscape(string value)
        {
            return System.Security.SecurityElement.Escape(value) ?? string.Empty;
        }
    }
}
