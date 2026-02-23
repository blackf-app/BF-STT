using BF_STT.Services.Audio;
using BF_STT.Services.Infrastructure;
using BF_STT.Services.Platform;
using BF_STT.Services.STT;
using BF_STT.Services.STT.Filters;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace BF_STT.Services.Workflow
{
    /// <summary>
    /// Handles batch STT processing — sends recorded audio to batch APIs and processes results.
    /// Supports both single-provider mode and test mode (all providers in parallel).
    /// </summary>
    public class BatchProcessor
    {
        private readonly SttProviderRegistry _registry;
        private readonly InputInjector _inputInjector;
        private readonly HistoryService _historyService;
        private readonly SettingsService _settingsService;

        #region Events

        /// <summary>Fired when status text should change.</summary>
        public event Action<string>? StatusChanged;

        /// <summary>Fired when the main transcript text changes.</summary>
        public event Action<string>? TranscriptChanged;

        /// <summary>Fired when a per-provider transcript changes (Test Mode).</summary>
        public event Action<string, string>? ProviderTranscriptChanged;

        #endregion

        public BatchProcessor(
            SttProviderRegistry registry,
            InputInjector inputInjector,
            HistoryService historyService,
            SettingsService settingsService)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _inputInjector = inputInjector ?? throw new ArgumentNullException(nameof(inputInjector));
            _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        /// <summary>
        /// Processes a batch recording with a single provider.
        /// Detects silence, filters hallucinations, injects text into target window.
        /// </summary>
        public async Task ProcessAsync(byte[] audioData, IntPtr targetWindow, string apiName, bool autoSend)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                if (!AudioSilenceDetector.ContainsSpeech(audioData))
                {
                    sw.Stop();
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        StatusChanged?.Invoke("Silent — skipped.");
                        TranscriptChanged?.Invoke(string.Empty);
                    });
                    return;
                }

                var activeBatch = _registry.GetBatchService(apiName);
                var language = _settingsService.CurrentSettings.DefaultLanguage;
                var transcript = await activeBatch.TranscribeAsync(audioData, language);
                sw.Stop();

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    if (HallucinationFilter.IsHallucination(transcript))
                    {
                        StatusChanged?.Invoke($"Hallucination — skipped. ({sw.ElapsedMilliseconds}ms)");
                        TranscriptChanged?.Invoke(string.Empty);
                        return;
                    }

                    var finalTranscript = FormatTranscript(transcript);
                    TranscriptChanged?.Invoke(finalTranscript);
                    StatusChanged?.Invoke($"Done. ({sw.ElapsedMilliseconds}ms)");

                    if (!string.IsNullOrWhiteSpace(finalTranscript))
                    {
                        _historyService.AddEntry(finalTranscript, apiName);
                        await _inputInjector.InjectTextAsync(finalTranscript, targetWindow, autoSend);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusChanged?.Invoke($"Error: {ex.Message}");
                    TranscriptChanged?.Invoke("Failed to get transcript.");
                });
            }
        }

        /// <summary>
        /// Processes a batch recording with all providers in parallel (Test Mode).
        /// </summary>
        public async Task ProcessTestModeAsync(byte[] audioData)
        {
            // Pre-API: silence detection on in-memory WAV data
            if (!AudioSilenceDetector.ContainsSpeech(audioData))
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    StatusChanged?.Invoke("Silent — skipped.");
                    foreach (var p in _registry.GetAllProviders())
                    {
                        ProviderTranscriptChanged?.Invoke(p.Name, "[Skipped] Silent audio");
                    }
                });
                return;
            }

            var language = _settingsService.CurrentSettings.DefaultLanguage;

            // Create independent tasks for each provider
            var providerTasks = _registry.GetAllProviders().Select(provider => Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var result = await provider.BatchService.TranscribeAsync(audioData, language);
                    sw.Stop();
                    var label = HallucinationFilter.IsHallucination(result) ? "[Hallucination]" : "";
                    var formatted = label == "" ? FormatTranscript(result) : $"{label} {result}";
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ProviderTranscriptChanged?.Invoke(provider.Name, $"[{sw.ElapsedMilliseconds}ms]\n{formatted}");
                    });
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ProviderTranscriptChanged?.Invoke(provider.Name, $"[{sw.ElapsedMilliseconds}ms] Failed: {ex.Message}");
                    });
                }
            })).ToArray();

            var overallSw = Stopwatch.StartNew();
            await Task.WhenAll(providerTasks);
            overallSw.Stop();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                StatusChanged?.Invoke($"Done. ({overallSw.ElapsedMilliseconds}ms)");
            });
        }

        /// <summary>
        /// Formats a raw transcript: trims and ensures period + space at the end.
        /// </summary>
        public string FormatTranscript(string transcript)
        {
            if (string.IsNullOrWhiteSpace(transcript)) return string.Empty;
            var trimmed = transcript.TrimEnd();
            return trimmed.EndsWith(".") ? trimmed + " " : trimmed + ". ";
        }
    }
}
