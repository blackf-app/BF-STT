using BF_STT.Services.Audio;
using BF_STT.Services.Platform;
using BF_STT.Services.STT;
using BF_STT.Services.STT.Providers.Deepgram;
using BF_STT.Services.STT.Providers.OpenAI;
using BF_STT.Services.STT.Providers.Soniox;
using BF_STT.Services.STT.Providers.ElevenLabs;
using BF_STT.Services.STT.Providers.Google;
using BF_STT.Services.STT.Providers.AssemblyAI;
using BF_STT.Services.STT.Providers.Azure;
using BF_STT.Services.STT.Providers.Speechmatics;
using BF_STT.Services.Workflow;
using BF_STT.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;

namespace BF_STT.Services.Infrastructure
{
    /// <summary>
    /// Centralizes all service registrations for the DI container.
    /// </summary>
    public static class ServiceRegistration
    {
        public static IServiceProvider Configure()
        {
            var services = new ServiceCollection();

            // ── Infrastructure ──
            services.AddSingleton<SettingsService>();
            services.AddSingleton<SoundService>();
            services.AddSingleton(sp =>
            {
                var settings = sp.GetRequiredService<SettingsService>().CurrentSettings;
                return new HistoryService(settings.MaxHistoryItems);
            });
            services.AddSingleton<HttpClient>();

            // ── Audio ──
            services.AddSingleton<AudioRecordingService>();

            // ── Platform ──
            services.AddSingleton<InputInjector>();

            // ── STT Provider Registry ──
            services.AddSingleton(sp =>
            {
                var httpClient = sp.GetRequiredService<HttpClient>();
                var settings = sp.GetRequiredService<SettingsService>().CurrentSettings;
                var registry = new SttProviderRegistry();

                // Deepgram
                var deepgramBatch = new DeepgramService(httpClient, settings.ApiKey, settings.BaseUrl, settings.Model);
                var deepgramStreaming = new DeepgramStreamingService(settings.ApiKey, settings.StreamingUrl, settings.Model);
                registry.Register("Deepgram", deepgramBatch, deepgramStreaming,
                    s => s.ApiKey, s => s.Model);

                // Speechmatics
                var speechmaticsBatch = new SpeechmaticsBatchService(httpClient, settings.SpeechmaticsApiKey, settings.SpeechmaticsBaseUrl);
                var speechmaticsStreaming = new SpeechmaticsStreamingService(settings.SpeechmaticsApiKey, settings.SpeechmaticsStreamingUrl);
                registry.Register("Speechmatics", speechmaticsBatch, speechmaticsStreaming,
                    s => s.SpeechmaticsApiKey, s => s.SpeechmaticsModel);

                // Soniox
                var sonioxBatch = new SonioxBatchService(httpClient, settings.SonioxApiKey, settings.SonioxBaseUrl);
                var sonioxStreaming = new SonioxStreamingService(settings.SonioxApiKey, settings.SonioxStreamingUrl);
                registry.Register("Soniox", sonioxBatch, sonioxStreaming,
                    s => s.SonioxApiKey, s => s.SonioxModel);

                // OpenAI (batch only — no native streaming support)
                var openaiBatch = new OpenAIBatchService(httpClient, settings.OpenAIApiKey, settings.OpenAIBaseUrl);
                registry.Register("OpenAI", openaiBatch, null,
                    s => s.OpenAIApiKey, s => s.OpenAIModel);

                // ElevenLabs
                var elevenLabsBatch = new ElevenLabsBatchService(httpClient, settings.ElevenLabsApiKey, settings.ElevenLabsBaseUrl, settings.ElevenLabsModel);
                var elevenLabsStreaming = new ElevenLabsStreamingService(settings.ElevenLabsApiKey, settings.ElevenLabsStreamingUrl, settings.ElevenLabsModel);
                registry.Register("ElevenLabs", elevenLabsBatch, elevenLabsStreaming,
                    s => s.ElevenLabsApiKey, s => s.ElevenLabsModel);

                // Google (batch only — no native streaming support)
                var googleBatch = new GoogleBatchService(httpClient, settings.GoogleApiKey, settings.GoogleBaseUrl, settings.GoogleModel);
                registry.Register("Google", googleBatch, null,
                    s => s.GoogleApiKey, s => s.GoogleModel);

                // AssemblyAI
                var assemblyAIBatch = new AssemblyAIBatchService(httpClient, settings.AssemblyAIApiKey, settings.AssemblyAIBaseUrl, settings.AssemblyAIModel);
                var assemblyAIStreaming = new AssemblyAIStreamingService(settings.AssemblyAIApiKey, settings.AssemblyAIStreamingUrl, settings.AssemblyAIModel);
                registry.Register("AssemblyAI", assemblyAIBatch, assemblyAIStreaming,
                    s => s.AssemblyAIApiKey, s => s.AssemblyAIModel);

                // Azure (batch only — streaming requires SDK)
                var azureBatch = new AzureBatchService(httpClient, settings.AzureApiKey, settings.AzureBaseUrl, settings.AzureModel);
                registry.Register("Azure", azureBatch, null,
                    s => s.AzureApiKey, s => s.AzureModel);

                return registry;
            });

            // ── Workflow ──
            services.AddSingleton<BatchProcessor>();
            services.AddSingleton<StreamingManager>();
            services.AddSingleton<RecordingCoordinator>();

            // ── ViewModels ──
            services.AddTransient<MainViewModel>();

            return services.BuildServiceProvider();
        }
    }
}
