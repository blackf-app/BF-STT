using System.Net.Http;

namespace BF_STT.Services.TTS.Abstractions
{
    public abstract class BaseTtsService : ITtsService
    {
        protected readonly HttpClient HttpClient;
        protected string ApiKey;
        protected string Model;
        protected string Voice;
        protected string BaseUrl;

        protected BaseTtsService(
            HttpClient httpClient,
            string apiKey,
            string model,
            string voice,
            string baseUrl,
            string defaultModel,
            string defaultVoice,
            string defaultBaseUrl)
        {
            HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            ApiKey = apiKey ?? string.Empty;
            Model = string.IsNullOrWhiteSpace(model) ? defaultModel : model;
            Voice = string.IsNullOrWhiteSpace(voice) ? defaultVoice : voice;
            BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? defaultBaseUrl : baseUrl.TrimEnd('/');
        }

        public virtual void UpdateSettings(string apiKey, string model, string voice, string baseUrl)
        {
            ApiKey = apiKey ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(model)) Model = model;
            if (!string.IsNullOrWhiteSpace(voice)) Voice = voice;
            if (!string.IsNullOrWhiteSpace(baseUrl)) BaseUrl = baseUrl.TrimEnd('/');
        }

        public async Task<TtsAudioResult> SynthesizeAsync(string text, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("TTS text is empty.", nameof(text));
            }

            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                throw new InvalidOperationException($"{GetType().Name}: TTS API Key is missing. Check Settings.");
            }

            return await SynthesizeCoreAsync(text, ct);
        }

        protected abstract Task<TtsAudioResult> SynthesizeCoreAsync(string text, CancellationToken ct);
    }
}
