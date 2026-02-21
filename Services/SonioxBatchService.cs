using BF_STT.Models;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Threading;

namespace BF_STT.Services
{
    public class SonioxBatchService : IBatchSttService
    {
        private readonly HttpClient _httpClient;
        private string _apiKey;
        private readonly string _baseUrl;
        private string _model;

        public SonioxBatchService(HttpClient httpClient, string apiKey, string baseUrl)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.soniox.com/v1" : baseUrl;
            _model = "stt-async-v4"; // Correct default model for Soniox async/files APIs
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

            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new InvalidOperationException("Soniox API Key is missing. Check Settings.");
            }

            // Upload the file to Soniox File API
            var uploadUrl = $"{_baseUrl}/files";
            var fileId = await UploadFileAsync(uploadUrl, audioFilePath);

            if (string.IsNullOrEmpty(fileId))
            {
                throw new Exception("Failed to upload file to Soniox.");
            }

            // Create transcription job
            var transcribeUrl = $"{_baseUrl}/transcriptions";
            var transcriptionId = await CreateTranscriptionAsync(transcribeUrl, fileId, language);

            if (string.IsNullOrEmpty(transcriptionId))
            {
                throw new Exception("Failed to create transcription job with Soniox.");
            }

            // Wait for completion
            var isCompleted = await WaitForJobCompletionAsync(transcribeUrl, transcriptionId);
            if (!isCompleted)
            {
                 throw new Exception("Soniox transcription job failed or timed out.");
            }

            // Get Transcript
            return await GetTranscriptAsync(transcribeUrl, transcriptionId);
        }

        private async Task<string> UploadFileAsync(string url, string filePath)
        {
            using var fileStream = File.OpenRead(filePath);
            var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream"); // or auto-detect
            content.Add(streamContent, "file", Path.GetFileName(filePath));

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = content;

            using var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Upload file failed: {response.StatusCode} - {json}");
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.TryGetProperty("id", out var idElement))
                {
                    return idElement.GetString() ?? "";
                }
                return "";
            }
            catch (Exception ex)
            {
                throw new Exception($"Upload file failed to parse. Response: {json}", ex);
            }
        }

        private async Task<string> CreateTranscriptionAsync(string url, string fileId, string language)
        {
            var requestBody = new
            {
                file_id = fileId,
                model = _model,
                transcription_config = new
                {
                    language = language
                }
            };
            
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Create transcription failed: {response.StatusCode} - {json}");
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.TryGetProperty("id", out var idElement))
                {
                    return idElement.GetString() ?? "";
                }
                return "";
            }
            catch (Exception ex)
            {
                throw new Exception($"Create transcription failed to parse. Response: {json}", ex);
            }
        }

        private async Task<bool> WaitForJobCompletionAsync(string baseUrl, string transcriptionId)
        {
            var url = $"{baseUrl}/{transcriptionId}";
            var maxAttempts = 600; // 10 minutes roughly
            var delayMs = 1000;

            for (int i = 0; i < maxAttempts; i++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                
                using var response = await _httpClient.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Poll transcription failed: {response.StatusCode} - {json}");
                }

                try
                {
                    using var document = JsonDocument.Parse(json);
                    
                    if (!document.RootElement.TryGetProperty("status", out var statusElement))
                    {
                        throw new KeyNotFoundException("Cannot find 'status' property in JSON.");
                    }
                    var status = statusElement.GetString();

                    if (string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    else if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "error", StringComparison.OrdinalIgnoreCase))
                    {
                        var errorMessage = "Soniox transcription failed.";
                        if (document.RootElement.TryGetProperty("error_message", out var errorMsgElement))
                        {
                            errorMessage = errorMsgElement.GetString() ?? errorMessage;
                        }
                        
                        throw new Exception($"{errorMessage} (Response: {json})");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Poll transcription failed on parse. Response: {json}", ex);
                }

                await Task.Delay(delayMs);
            }

            return false;
        }

        private async Task<string> GetTranscriptAsync(string baseUrl, string transcriptionId)
        {
            var url = $"{baseUrl}/{transcriptionId}/transcript";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            using var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Get transcript failed: {response.StatusCode} - {json}");
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.TryGetProperty("text", out var textElement))
                {
                    return textElement.GetString() ?? "";
                }
                throw new Exception($"Get transcript missing 'text'. Response: {json}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Get transcript failed to parse. Response: {json}", ex);
            }
        }
    }
}
