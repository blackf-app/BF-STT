using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

using BF_STT.Services.STT.Abstractions;

namespace BF_STT.Services.STT.Providers.OpenAI
{
    public class OpenAIBatchService : BaseBatchSttService
    {
        public OpenAIBatchService(HttpClient httpClient, string apiKey, string baseUrl)
            : base(httpClient, apiKey, baseUrl, "https://api.openai.com/v1/audio/transcriptions", "whisper-1")
        { }

        protected override async Task<string> TranscribeCore(byte[] audioData, string language, CancellationToken ct)
        {
            using var content = new MultipartFormDataContent();
            
            var streamContent = new ByteArrayContent(audioData);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            content.Add(streamContent, "file", "audio.wav");
            content.Add(new StringContent(Model), "model");
            if (!string.IsNullOrWhiteSpace(language))
            {
                content.Add(new StringContent(language), "language");
            }

            var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
            request.Content = content;

            using var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"OpenAI request failed: {response.StatusCode} - {errorBody}");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);
            
            if (document.RootElement.TryGetProperty("text", out var textElement))
            {
                return textElement.GetString() ?? "";
            }
            
            return "";
        }
    }
}
