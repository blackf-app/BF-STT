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

        public async Task<string> TranscribeAsync(string audioFilePath, string language)
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
            var transcriptionId = await CreateTranscriptionAsync(transcribeUrl, fileId);

            if (string.IsNullOrEmpty(transcriptionId))
            {
                throw new Exception("Failed to create transcription job with Soniox.");
            }

            // Poll for result
            return await PollForTranscriptionAsync(transcribeUrl, transcriptionId);
        }

        private async Task<string> UploadFileAsync(string url, string filePath)
        {
            using var fileStream = File.OpenRead(filePath);
            var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav"); // or auto-detect
            content.Add(streamContent, "file", Path.GetFileName(filePath));

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = content;

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Upload file failed: {response.StatusCode} - {errorBody}");
            }

            var json = await response.Content.ReadAsStringAsync();
            try
            {
                using var document = JsonDocument.Parse(json);
                // Soniox usually returns {"file": {"id": "..."}}
                if (document.RootElement.TryGetProperty("file", out var fileElement) && fileElement.TryGetProperty("id", out var idElement))
                {
                    return idElement.GetString() ?? "";
                }
                // Or maybe just id or file_id at root
                if (document.RootElement.TryGetProperty("id", out var idRootElement))
                {
                    return idRootElement.GetString() ?? "";
                }
                return document.RootElement.GetProperty("file_id").GetString() ?? "";
            }
            catch (Exception ex)
            {
                throw new Exception($"Upload file failed to parse. Response: {json}", ex);
            }
        }

        private async Task<string> CreateTranscriptionAsync(string url, string fileId)
        {
            var requestBody = new
            {
                file_id = fileId,
                model = _model
            };
            
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Create transcription failed: {response.StatusCode} - {errorBody}");
            }

            var json = await response.Content.ReadAsStringAsync();
            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.TryGetProperty("transcription", out var transElement) && transElement.TryGetProperty("id", out var idElement))
                {
                    return idElement.GetString() ?? "";
                }
                if (document.RootElement.TryGetProperty("id", out idElement))
                {
                    return idElement.GetString() ?? "";
                }
                return document.RootElement.GetProperty("transcription_id").GetString() ?? "";
            }
            catch (Exception ex)
            {
                throw new Exception($"Create transcription failed to parse. Response: {json}", ex);
            }
        }

        private async Task<string> PollForTranscriptionAsync(string baseUrl, string transcriptionId)
        {
            var url = $"{baseUrl}/{transcriptionId}";
            var maxAttempts = 60; // 1 minute roughly
            var delayMs = 1000;

            for (int i = 0; i < maxAttempts; i++)
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                
                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Poll transcription failed: {response.StatusCode} - {errorBody}");
                }

                var json = await response.Content.ReadAsStringAsync();
                try
                {
                    using var document = JsonDocument.Parse(json);
                    
                    JsonElement transElement = document.RootElement;
                    if (document.RootElement.TryGetProperty("transcription", out var te))
                    {
                        transElement = te;
                    }

                    if (!transElement.TryGetProperty("status", out var statusElement))
                    {
                        throw new KeyNotFoundException("Cannot find 'status' property in JSON.");
                    }
                    var status = statusElement.GetString();

                    if (status == "COMPLETED")
                    {
                        // Some endpoints return words array, some return text.
                        if (transElement.TryGetProperty("text", out var textElement))
                        {
                            return textElement.GetString() ?? "";
                        }
                        return "Completed, but no text found in response.";
                    }
                    else if (status == "FAILED")
                    {
                        throw new Exception($"Soniox transcription failed. Response: {json}");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Poll transcription failed on parse. Response: {json}", ex);
                }

                await Task.Delay(delayMs);
            }

            throw new TimeoutException("Soniox transcription timed out.");
        }
    }
}
