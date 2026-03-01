using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using BF_STT.Services.STT.Abstractions;

namespace BF_STT.Services.STT.Providers.AssemblyAI
{
    public class AssemblyAIBatchService : BaseBatchSttService
    {
        public AssemblyAIBatchService(HttpClient httpClient, string apiKey, string baseUrl, string model)
            : base(httpClient, apiKey, baseUrl, "https://api.assemblyai.com",
                   string.IsNullOrWhiteSpace(model) ? "best" : model)
        { }

        protected override async Task<string> TranscribeCore(byte[] audioData, string language, CancellationToken ct)
        {
            int maxRetries = 1;
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // Step 1: Upload audio to AssemblyAI
                    var uploadUrl = await UploadAudioAsync(audioData, ct);

                    // Step 2: Create transcript job
                    var transcriptId = await CreateTranscriptAsync(uploadUrl, language, ct);

                    // Step 3: Poll until completion
                    return await PollTranscriptAsync(transcriptId, ct);
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[AssemblyAI] Request failed (attempt {attempt + 1}/{maxRetries + 1}): {ex.Message}");

                    if (attempt == maxRetries)
                    {
                        throw new HttpRequestException($"AssemblyAI API Error after retry: {ex.Message}", ex);
                    }

                    await Task.Delay(1000 * (attempt + 1), ct);
                }
            }
            return string.Empty;
        }

        private async Task<string> UploadAudioAsync(byte[] audioData, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v2/upload");
            request.Headers.Add("Authorization", ApiKey);
            request.Content = new ByteArrayContent(audioData);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            using var response = await HttpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"AssemblyAI upload failed: {response.StatusCode} - {error}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<AssemblyAIUploadResponse>(json);
            return result?.UploadUrl ?? throw new InvalidOperationException("Upload returned no URL");
        }

        private async Task<string> CreateTranscriptAsync(string audioUrl, string language, CancellationToken ct)
        {
            var body = new Dictionary<string, object>
            {
                { "audio_url", audioUrl },
                { "speech_models", new[] { Model } }
            };

            // Map language code to AssemblyAI format (e.g., "vi" -> "vi")
            if (!string.IsNullOrWhiteSpace(language))
            {
                body["language_code"] = language;
            }

            var jsonBody = JsonSerializer.Serialize(body);

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v2/transcript");
            request.Headers.Add("Authorization", ApiKey);
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            using var response = await HttpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"AssemblyAI transcript create failed: {response.StatusCode} - {error}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<AssemblyAITranscriptResponse>(json);
            return result?.Id ?? throw new InvalidOperationException("Transcript returned no ID");
        }

        private async Task<string> PollTranscriptAsync(string transcriptId, CancellationToken ct)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            int maxPolls = 120; // 120 * 1s = 2 minutes max

            for (int i = 0; i < maxPolls; i++)
            {
                await Task.Delay(1000, ct);

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/v2/transcript/{transcriptId}");
                request.Headers.Add("Authorization", ApiKey);

                using var response = await HttpClient.SendAsync(request, ct);
                var json = await response.Content.ReadAsStringAsync();

#if DEBUG
                System.Diagnostics.Debug.WriteLine($"[AssemblyAI] Poll #{i + 1}: {json.Substring(0, Math.Min(200, json.Length))}");
#endif

                var result = JsonSerializer.Deserialize<AssemblyAITranscriptResponse>(json, options);

                if (result?.Status == "completed")
                {
                    return result.Text ?? string.Empty;
                }
                else if (result?.Status == "error")
                {
                    throw new HttpRequestException($"AssemblyAI transcription error: {result.Error}");
                }
                // else status is "queued" or "processing", continue polling
            }

            throw new TimeoutException("AssemblyAI transcription timed out after 2 minutes");
        }
    }
}
