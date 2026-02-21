using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace BF_STT.Services
{
    /// <summary>
    /// Centralizes STT provider management, eliminating provider-specific if/else chains
    /// throughout the codebase. Each provider is registered with its batch service,
    /// streaming service, and a function to retrieve its API key from settings.
    /// </summary>
    public class SttProviderRegistry
    {
        private readonly Dictionary<string, SttProviderEntry> _providers = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Registers a provider with its batch and streaming services.
        /// </summary>
        public void Register(
            string name,
            IBatchSttService batchService,
            IStreamingSttService streamingService,
            Func<AppSettings, string> getApiKey,
            Func<AppSettings, string> getModel)
        {
            _providers[name] = new SttProviderEntry(name, batchService, streamingService, getApiKey, getModel);
        }

        /// <summary>
        /// Returns the batch service for the given provider name.
        /// Defaults to the first registered provider if name is not found.
        /// </summary>
        public IBatchSttService GetBatchService(string providerName)
        {
            return GetEntry(providerName).BatchService;
        }

        /// <summary>
        /// Returns the streaming service for the given provider name.
        /// Defaults to the first registered provider if name is not found.
        /// </summary>
        public IStreamingSttService GetStreamingService(string providerName)
        {
            return GetEntry(providerName).StreamingService;
        }

        /// <summary>
        /// Returns all registered provider entries (for Test Mode iteration).
        /// </summary>
        public IReadOnlyList<SttProviderEntry> GetAllProviders()
        {
            return _providers.Values.ToList().AsReadOnly();
        }

        /// <summary>
        /// Returns the registered provider names (for UI display).
        /// </summary>
        public IReadOnlyList<string> GetProviderNames()
        {
            return _providers.Keys.ToList().AsReadOnly();
        }

        /// <summary>
        /// Validates that the API key for the given provider is configured.
        /// Returns null if valid, or an error message if the key is missing.
        /// </summary>
        public string? ValidateApiKey(string providerName, AppSettings settings)
        {
            if (!_providers.TryGetValue(providerName, out var entry))
            {
                return $"Unknown provider: {providerName}";
            }

            var apiKey = entry.GetApiKey(settings);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return $"{entry.Name} API Key missing.";
            }

            return null;
        }

        /// <summary>
        /// Updates settings (API key and model) for all registered providers.
        /// </summary>
        public void UpdateAllSettings(AppSettings settings)
        {
            foreach (var entry in _providers.Values)
            {
                var apiKey = entry.GetApiKey(settings);
                var model = entry.GetModel(settings);
                entry.BatchService.UpdateSettings(apiKey, model);
                entry.StreamingService.UpdateSettings(apiKey, model);
            }
        }

        private SttProviderEntry GetEntry(string providerName)
        {
            if (_providers.TryGetValue(providerName, out var entry))
            {
                return entry;
            }

            Debug.WriteLine($"[SttProviderRegistry] Provider '{providerName}' not found. Using first available.");
            return _providers.Values.First();
        }
    }

    /// <summary>
    /// Holds all services and metadata for a single STT provider.
    /// </summary>
    public record SttProviderEntry(
        string Name,
        IBatchSttService BatchService,
        IStreamingSttService StreamingService,
        Func<AppSettings, string> GetApiKey,
        Func<AppSettings, string> GetModel
    );
}
