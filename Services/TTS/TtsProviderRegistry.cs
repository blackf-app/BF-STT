using BF_STT.Services.Infrastructure;
using BF_STT.Services.TTS.Abstractions;

namespace BF_STT.Services.TTS
{
    public class TtsProviderRegistry
    {
        private readonly Dictionary<string, TtsProviderEntry> _providers = new(StringComparer.OrdinalIgnoreCase);

        public void Register(
            string name,
            ITtsService service,
            bool supportsSynthesis,
            Func<AppSettings, string> getApiKey,
            Func<AppSettings, string> getModel,
            Func<AppSettings, string> getVoice,
            Func<AppSettings, string> getBaseUrl,
            string? unavailableReason = null)
        {
            _providers[name] = new TtsProviderEntry(
                name,
                service,
                supportsSynthesis,
                getApiKey,
                getModel,
                getVoice,
                getBaseUrl,
                unavailableReason);
        }

        public IReadOnlyList<TtsProviderEntry> GetAllProviders()
        {
            return _providers.Values.ToList().AsReadOnly();
        }

        public TtsProviderEntry GetEntry(string providerName)
        {
            if (_providers.TryGetValue(providerName, out var entry))
            {
                return entry;
            }

            return _providers.Values.First();
        }

        public string? ValidateProvider(string providerName, AppSettings settings)
        {
            if (!_providers.TryGetValue(providerName, out var entry))
            {
                return $"Unknown TTS provider: {providerName}";
            }

            if (!entry.SupportsSynthesis)
            {
                return entry.UnavailableReason ?? $"{entry.Name} does not support standalone TTS.";
            }

            if (string.IsNullOrWhiteSpace(entry.GetApiKey(settings)))
            {
                return $"{entry.Name} TTS API Key missing.";
            }

            return null;
        }

        public void UpdateAllSettings(AppSettings settings)
        {
            foreach (var entry in _providers.Values)
            {
                entry.Service.UpdateSettings(
                    entry.GetApiKey(settings),
                    entry.GetModel(settings),
                    entry.GetVoice(settings),
                    entry.GetBaseUrl(settings));
            }
        }
    }

    public sealed record TtsProviderEntry(
        string Name,
        ITtsService Service,
        bool SupportsSynthesis,
        Func<AppSettings, string> GetApiKey,
        Func<AppSettings, string> GetModel,
        Func<AppSettings, string> GetVoice,
        Func<AppSettings, string> GetBaseUrl,
        string? UnavailableReason);
}
