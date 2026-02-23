using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Linq;

using BF_STT.Services.STT.Abstractions;

namespace BF_STT.Services.STT.Providers.Soniox
{
    public class SonioxBatchService : BaseBatchSttService
    {
        public SonioxBatchService(HttpClient httpClient, string apiKey, string baseUrl)
            : base(httpClient, apiKey, baseUrl, "https://api.soniox.com/v1", "stt-async-v4")
        { }

        protected override async Task<string> TranscribeCore(byte[] audioData, string language, CancellationToken ct)
        {
            // Upload the audio data to Soniox File API
            var uploadUrl = $"{BaseUrl}/files";
            var fileId = await UploadDataAsync(uploadUrl, audioData);

            if (string.IsNullOrEmpty(fileId))
            {
                throw new Exception("Failed to upload file to Soniox.");
            }

            // Create transcription job
            var transcribeUrl = $"{BaseUrl}/transcriptions";
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

        private async Task<string> UploadDataAsync(string url, byte[] audioData)
        {
            var content = new MultipartFormDataContent();
            var byteContent = new ByteArrayContent(audioData);
            byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(byteContent, "file", "audio.wav");

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
            request.Content = content;

            using var response = await HttpClient.SendAsync(request);
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
                model = Model,
                transcription_config = new
                {
                    language = language
                }
            };
            
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json");

            using var response = await HttpClient.SendAsync(request);
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
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
                
                using var response = await HttpClient.SendAsync(request);
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
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

            using var response = await HttpClient.SendAsync(request);
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
