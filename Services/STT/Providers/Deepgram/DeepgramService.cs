using BF_STT.Models;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

using BF_STT.Services.STT.Abstractions;

namespace BF_STT.Services.STT.Providers.Deepgram
{
    public class DeepgramService : IBatchSttService
    {
        private readonly HttpClient _httpClient;
        private string _apiKey;
        private readonly string _baseUrl;
        private string _model;

        public DeepgramService(HttpClient httpClient, string apiKey, string baseUrl, string model)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            
            // Allow override but default to these if not provided in config
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.deepgram.com/v1/listen" : baseUrl;
            _model = string.IsNullOrWhiteSpace(model) ? "nova-3" : model;
        }

        public void UpdateSettings(string apiKey, string model)
        {
            _apiKey = apiKey;
            if (!string.IsNullOrWhiteSpace(model)) _model = model;
        }

        public async Task<string> TranscribeAsync(string audioFilePath, string language, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(audioFilePath) || !File.Exists(audioFilePath))
            {
                throw new FileNotFoundException("Audio file not found.", audioFilePath);
            }

            byte[] fileBytes = await File.ReadAllBytesAsync(audioFilePath);
            return await TranscribeAsync(fileBytes, language, ct);
        }

        public async Task<string> TranscribeAsync(byte[] audioData, string language, CancellationToken ct = default)
        {
            if (audioData == null || audioData.Length == 0)
            {
                throw new ArgumentException("Audio data is empty.", nameof(audioData));
            }

            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new InvalidOperationException("Deepgram API Key is missing. Check appsettings.json or DEEPGRAM_API_KEY env var.");
            }

            var url = $"{_baseUrl}?model={_model}&language={language}&smart_format=true";

            int maxRetries = 1;
            for (int i = 0; i <= maxRetries; i++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Token", _apiKey);
                
                using var content = new ByteArrayContent(audioData);
                content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
                request.Content = content;

                try
                {
                    using var response = await _httpClient.SendAsync(request);
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
