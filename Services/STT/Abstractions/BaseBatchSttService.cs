using System.Net.Http;
using System.IO;

namespace BF_STT.Services.STT.Abstractions
{
    /// <summary>
    /// Base class for batch STT services, providing common boilerplate:
    /// field management, UpdateSettings, file-to-bytes delegation, and input validation.
    /// Subclasses only need to implement TranscribeCore().
    /// </summary>
    public abstract class BaseBatchSttService : IBatchSttService
    {
        protected readonly HttpClient HttpClient;
        protected string ApiKey;
        protected readonly string BaseUrl;
        protected string Model;

        protected BaseBatchSttService(HttpClient httpClient, string apiKey, string baseUrl,
                                      string defaultBaseUrl, string defaultModel)
        {
            HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? defaultBaseUrl : baseUrl;
            Model = string.IsNullOrWhiteSpace(defaultModel) ? "" : defaultModel;
        }

        public virtual void UpdateSettings(string apiKey, string model)
        {
            ApiKey = apiKey;
            if (!string.IsNullOrWhiteSpace(model)) Model = model;
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

            if (string.IsNullOrEmpty(ApiKey))
            {
                throw new InvalidOperationException($"{GetType().Name}: API Key is missing. Check Settings.");
            }

            return await TranscribeCore(audioData, language, ct);
        }

        /// <summary>
        /// Implement provider-specific transcription logic.
        /// Input is guaranteed non-null, non-empty, and ApiKey is validated.
        /// </summary>
        protected abstract Task<string> TranscribeCore(byte[] audioData, string language, CancellationToken ct);
    }
}
