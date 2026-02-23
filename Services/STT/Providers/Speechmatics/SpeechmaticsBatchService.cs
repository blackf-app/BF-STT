using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using BF_STT.Services.STT.Abstractions;

namespace BF_STT.Services.STT.Providers.Speechmatics
{
    public class SpeechmaticsBatchService : BaseBatchSttService
    {
        public SpeechmaticsBatchService(HttpClient httpClient, string apiKey, string baseUrl)
            : base(httpClient, apiKey, baseUrl?.TrimEnd('/') ?? "", "https://asr.api.speechmatics.com/v2", "")
        { }

        public override void UpdateSettings(string apiKey, string model)
        {
            ApiKey = apiKey;
            // Speechmatics doesn't use a model field in the same way
        }

        protected override async Task<string> TranscribeCore(byte[] audioData, string language, CancellationToken ct)
        {
            string jobId = await SubmitJobAsync(audioData, language);
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

        private async Task<string> SubmitJobAsync(byte[] audioData, string language)
        {
            var url = $"{BaseUrl}/jobs";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

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

            var audioContent = new ByteArrayContent(audioData);
            audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            content.Add(audioContent, "data_file", "audio.wav");

            request.Content = content;

            using var response = await HttpClient.SendAsync(request);
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
            var url = $"{BaseUrl}/jobs/{jobId}";
            
            // Poll for up to 60 seconds
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(1000); // 1 second polling interval

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

                using var response = await HttpClient.SendAsync(request);
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
            var url = $"{BaseUrl}/jobs/{jobId}/transcript?format=txt";
            
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

            using var response = await HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
    }
}
