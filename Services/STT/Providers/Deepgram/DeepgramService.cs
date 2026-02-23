using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

using BF_STT.Services.STT.Abstractions;

namespace BF_STT.Services.STT.Providers.Deepgram
{
    public class DeepgramService : BaseBatchSttService
    {
        public DeepgramService(HttpClient httpClient, string apiKey, string baseUrl, string model)
            : base(httpClient, apiKey, baseUrl, "https://api.deepgram.com/v1/listen",
                   string.IsNullOrWhiteSpace(model) ? "nova-3" : model)
        { }

        protected override async Task<string> TranscribeCore(byte[] audioData, string language, CancellationToken ct)
        {
            var url = $"{BaseUrl}?model={Model}&language={language}&smart_format=true";

            int maxRetries = 1;
            for (int i = 0; i <= maxRetries; i++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Token", ApiKey);
                
                using var content = new ByteArrayContent(audioData);
                content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
                request.Content = content;

                try
                {
                    using var response = await HttpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync();

                    #if DEBUG
                    System.Diagnostics.Debug.WriteLine($"RAW RESPONSE: {json}");
                    #endif

                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var result = JsonSerializer.Deserialize<DeepgramResponse>(json, options);

                    var transcript = result?.Results?.Channels?.FirstOrDefault()?.Alternatives?.FirstOrDefault()?.Transcript;
                    return transcript ?? string.Empty;
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
                {
                    if (i == maxRetries)
                    {
                        throw new HttpRequestException($"Deepgram API Error after retry: {ex.Message}", ex);
                    }
                    
                    await Task.Delay(500);
                }
            }
            return string.Empty;
        }
    }
}
