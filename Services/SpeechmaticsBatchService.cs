using BF_STT.Models;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BF_STT.Services
{
    public class SpeechmaticsBatchService : IBatchSttService
    {
        private readonly HttpClient _httpClient;
        private string _apiKey;
        private readonly string _baseUrl;

        public SpeechmaticsBatchService(HttpClient httpClient, string apiKey, string baseUrl)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://asr.api.speechmatics.com/v2" : baseUrl.TrimEnd('/');
        }

        public void UpdateSettings(string apiKey, string model)
        {
            _apiKey = apiKey;
        }

        public async Task<string> TranscribeAsync(string audioFilePath, string language, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(audioFilePath) || !File.Exists(audioFilePath))
            {
                throw new FileNotFoundException("Audio file not found.", audioFilePath);
            }

            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new InvalidOperationException("Speechmatics API Key is missing. Check settings.");
            }

            string jobId = await SubmitJobAsync(audioFilePath, language);
            if (string.IsNullOrEmpty(jobId))
            {
                throw new Exception("Failed to submit Speechmatics batch job.");
            }

            bool isDone = await WaitForJobCompletionAsync(jobId);
            if (!isDone)
            {
                throw new Exception("Speechmatics batch job failed or timed out.");
            }

            return await GetTranscriptAsync(jobId);
        }

        private async Task<string> SubmitJobAsync(string filePath, string language)
        {
            var url = $"{_baseUrl}/jobs";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            var configJson = JsonSerializer.Serialize(new
            {
                type = "transcription",
                transcription_config = new
                {
                    language = language,
                    operating_point = "standard"
                }
            });

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(configJson, Encoding.UTF8, "application/json"), "config");

            byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
            var audioContent = new ByteArrayContent(fileBytes);
            audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav"); // assuming wav
            content.Add(audioContent, "data_file", "audio.wav");

            request.Content = content;

            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(json);
            
            if (result.TryGetProperty("id", out var idElement))
            {
                return idElement.GetString() ?? "";
            }
            return "";
        }

        private async Task<bool> WaitForJobCompletionAsync(string jobId)
        {
            var url = $"{_baseUrl}/jobs/{jobId}";
            
            // Poll for up to 60 seconds
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(1000); // 1 second polling interval

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

                using var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<JsonElement>(json);
                    
                    if (result.TryGetProperty("job", out var jobElement) && 
                        jobElement.TryGetProperty("status", out var statusElement))
                    {
                        var status = statusElement.GetString();
                        if (status == "done") return true;
                        if (status == "rejected" || status == "deleted") return false;
                    }
                }
            }

            return false;
        }

        private async Task<string> GetTranscriptAsync(string jobId)
        {
            var url = $"{_baseUrl}/jobs/{jobId}/transcript?format=txt";
            
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
    }
}
