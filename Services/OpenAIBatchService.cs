using BF_STT.Models;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System;

namespace BF_STT.Services
{
    public class OpenAIBatchService : IBatchSttService
    {
        private readonly HttpClient _httpClient;
        private string _apiKey;
        private readonly string _baseUrl;
        private string _model;

        public OpenAIBatchService(HttpClient httpClient, string apiKey, string baseUrl)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.openai.com/v1/audio/transcriptions" : baseUrl;
            _model = "whisper-1";
        }

        public void UpdateSettings(string apiKey, string model)
        {
            _apiKey = apiKey;
            if (!string.IsNullOrWhiteSpace(model)) _model = model;
        }

        public async Task<string> TranscribeAsync(string audioFilePath, string language)
        {
            if (string.IsNullOrEmpty(audioFilePath) || !File.Exists(audioFilePath))
            {
                throw new FileNotFoundException("Audio file not found.", audioFilePath);
            }

            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new InvalidOperationException("OpenAI API Key is missing. Check Settings.");
            }

            using var fileStream = File.OpenRead(audioFilePath);
            using var content = new MultipartFormDataContent();
            
            var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            content.Add(streamContent, "file", Path.GetFileName(audioFilePath));
            content.Add(new StringContent(_model), "model");
            // language is optional but helps performance. It requires ISO-639-1 (e.g., "en", "vi")
            if (!string.IsNullOrWhiteSpace(language))
            {
                content.Add(new StringContent(language), "language");
            }

            var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = content;

            using var response = await _httpClient.SendAsync(request);
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
