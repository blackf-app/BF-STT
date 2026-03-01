using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

using BF_STT.Services.STT.Abstractions;

namespace BF_STT.Services.STT.Providers.Azure
{
    public class AzureBatchService : BaseBatchSttService
    {
        private readonly string _region;

        public AzureBatchService(HttpClient httpClient, string apiKey, string baseUrl, string model)
            : base(httpClient, apiKey, baseUrl, "", // default URL built dynamically from region
                   string.IsNullOrWhiteSpace(model) ? "" : model)
        {
            // Extract region from baseUrl or default to "eastus"
            // Expected format: "eastus" or "https://eastus.stt.speech.microsoft.com/..."
            _region = ExtractRegion(baseUrl);
        }

        private static string ExtractRegion(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                return "eastus";

            // If it looks like a full URL, extract region from it
            if (baseUrl.Contains(".stt.speech.microsoft.com"))
            {
                var uri = new Uri(baseUrl);
                var host = uri.Host; // e.g. "eastus.stt.speech.microsoft.com"
                return host.Split('.')[0];
            }

            // Otherwise treat as region name directly
            return baseUrl.Trim();
        }

        protected override async Task<string> TranscribeCore(byte[] audioData, string language, CancellationToken ct)
        {
            var lang = string.IsNullOrWhiteSpace(language) ? "vi-VN" : MapLanguageCode(language);
            var url = $"https://{_region}.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1?language={lang}";

            int maxRetries = 1;
            for (int i = 0; i <= maxRetries; i++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("Ocp-Apim-Subscription-Key", ApiKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using var content = new ByteArrayContent(audioData);
                content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
                content.Headers.ContentType.Parameters.Add(new NameValueHeaderValue("codecs", "audio/pcm"));
                content.Headers.ContentType.Parameters.Add(new NameValueHeaderValue("samplerate", "16000"));
                request.Content = content;

                try
                {
                    using var response = await HttpClient.SendAsync(request, ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync();

                        if ((int)response.StatusCode >= 500 && i < maxRetries)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"[Azure] Server error {response.StatusCode}, retrying ({i + 1}/{maxRetries})...");
                            await Task.Delay(1000 * (i + 1), ct);
                            continue;
                        }

                        throw new HttpRequestException($"Azure STT API Error: {response.StatusCode} - {errorBody}");
                    }

                    var json = await response.Content.ReadAsStringAsync();

#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[Azure] RAW RESPONSE: {json}");
#endif

                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var result = JsonSerializer.Deserialize<AzureRecognitionResponse>(json, options);

                    if (result?.RecognitionStatus == "Success")
                    {
                        return result.DisplayText ?? string.Empty;
                    }

                    if (result?.RecognitionStatus == "NoMatch")
                    {
                        return string.Empty;
                    }

                    // InitialSilenceTimeout, BabbleTimeout, etc.
                    System.Diagnostics.Debug.WriteLine($"[Azure] Recognition status: {result?.RecognitionStatus}");
                    return string.Empty;
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[Azure] Request failed (attempt {i + 1}/{maxRetries + 1}): {ex.Message}");

                    if (i == maxRetries)
                    {
                        throw new HttpRequestException($"Azure STT API Error after retry: {ex.Message}", ex);
                    }

                    await Task.Delay(1000 * (i + 1), ct);
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// Maps short language codes (e.g. "vi") to Azure BCP-47 locale codes (e.g. "vi-VN").
        /// </summary>
        private static string MapLanguageCode(string lang)
        {
            return lang.ToLowerInvariant() switch
            {
                "vi" => "vi-VN",
                "en" => "en-US",
                "ja" => "ja-JP",
                "ko" => "ko-KR",
                "zh" => "zh-CN",
                "fr" => "fr-FR",
                "de" => "de-DE",
                _ => lang // Pass through if already in BCP-47 format
            };
        }
    }
}
