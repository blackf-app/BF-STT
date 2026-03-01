using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

using BF_STT.Services.STT.Abstractions;

namespace BF_STT.Services.STT.Providers.ElevenLabs
{
    public class ElevenLabsBatchService : BaseBatchSttService
    {
        public ElevenLabsBatchService(HttpClient httpClient, string apiKey, string baseUrl, string model)
            : base(httpClient, apiKey, baseUrl, "https://api.elevenlabs.io/v1/speech-to-text",
                   string.IsNullOrWhiteSpace(model) ? "scribe_v2" : model)
        { }

        protected override async Task<string> TranscribeCore(byte[] audioData, string language, CancellationToken ct)
        {
            int maxRetries = 2;
            for (int i = 0; i <= maxRetries; i++)
            {
                // Content must be created fresh each iteration because
                // HttpRequestMessage.Dispose() disposes the attached content.
                using var content = new MultipartFormDataContent();

                var audioContent = new ByteArrayContent(audioData);
                audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
                content.Add(audioContent, "file", "audio.wav");

                content.Add(new StringContent(Model), "model_id");

                if (!string.IsNullOrWhiteSpace(language))
                {
                    content.Add(new StringContent(language), "language_code");
                }

                using var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl);
                request.Headers.Add("xi-api-key", ApiKey);
                request.Content = content;

                try
                {
                    using var response = await HttpClient.SendAsync(request, ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync();

                        // Retry on server errors (5xx)
                        if ((int)response.StatusCode >= 500 && i < maxRetries)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"[ElevenLabs] Server error {response.StatusCode}, retrying ({i + 1}/{maxRetries})...");
                            await Task.Delay(1000 * (i + 1), ct);
                            continue;
                        }

                        throw new HttpRequestException($"ElevenLabs API Error: {response.StatusCode} - {errorBody}");
                    }

                    var json = await response.Content.ReadAsStringAsync();

#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[ElevenLabs] RAW RESPONSE: {json}");
#endif

                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var result = JsonSerializer.Deserialize<ElevenLabsResponse>(json, options);

                    return result?.Text ?? string.Empty;
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[ElevenLabs] Request failed (attempt {i + 1}/{maxRetries + 1}): {ex.Message}");

                    if (i == maxRetries)
                    {
                        throw new HttpRequestException($"ElevenLabs API Error after retry: {ex.Message}", ex);
                    }

                    await Task.Delay(1000 * (i + 1), ct);
                }
            }
            return string.Empty;
        }
    }
}
