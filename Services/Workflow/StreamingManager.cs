using BF_STT.Models;
using BF_STT.Services.Audio;
using BF_STT.Services.Infrastructure;
using BF_STT.Services.Platform;
using BF_STT.Services.STT;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace BF_STT.Services.Workflow
{
    /// <summary>
    /// Manages the streaming STT lifecycle — WebSocket connections, audio buffering,
    /// real-time text injection, and finalization.
    /// </summary>
    public class StreamingManager
    {
        private readonly SttProviderRegistry _registry;
        private readonly InputInjector _inputInjector;
        private readonly HistoryService _historyService;
        private readonly SettingsService _settingsService;
        private readonly SoundService _soundService;

        private ConcurrentQueue<AudioDataEventArgs> _audioBuffer = new();

        #region Events

        /// <summary>Fired when status text should change.</summary>
        public event Action<string>? StatusChanged;

        /// <summary>Fired when the main transcript text changes.</summary>
        public event Action<string>? TranscriptChanged;

        /// <summary>Fired when a per-provider transcript changes (Test Mode).</summary>
        public event Action<string, string>? ProviderTranscriptChanged;

        #endregion

        public StreamingManager(
            SttProviderRegistry registry,
            InputInjector inputInjector,
            HistoryService historyService,
            SettingsService settingsService,
            SoundService soundService)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _inputInjector = inputInjector ?? throw new ArgumentNullException(nameof(inputInjector));
            _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _soundService = soundService ?? throw new ArgumentNullException(nameof(soundService));
        }

        /// <summary>
        /// Wires streaming events for all registered providers.
        /// Call once during initialization.
        /// </summary>
        public void WireProviderEvents()
        {
            foreach (var provider in _registry.GetAllProviders())
            {
                provider.StreamingService.TranscriptReceived += OnTranscriptReceived;
                provider.StreamingService.UtteranceEndReceived += OnUtteranceEndReceived;
                provider.StreamingService.Error += OnStreamingError;
            }
        }

        #region Audio Buffer

        public void EnqueueAudioBuffer(AudioDataEventArgs e) => _audioBuffer.Enqueue(e);

        public void ClearBuffer()
        {
            while (_audioBuffer.TryDequeue(out _)) { }
        }

        #endregion

        /// <summary>
        /// Enters streaming mode — starts WebSocket connections and flushes buffered audio.
        /// </summary>
        public async Task EnterStreamingAsync(bool isTestMode, string streamingApi)
        {
            try
            {
                var language = _settingsService.CurrentSettings.DefaultLanguage;
                if (isTestMode)
                {
                    var startTasks = _registry.GetAllProviders()
                        .Select(p => p.StreamingService.StartAsync(language));
                    await Task.WhenAll(startTasks);

                    // Flush buffer to all WebSockets
                    while (_audioBuffer.TryDequeue(out var args))
                    {
                        var sendTasks = _registry.GetAllProviders()
                            .Select(p => p.StreamingService.SendAudioAsync(args.Buffer, args.BytesRecorded));
                        await Task.WhenAll(sendTasks);
                    }
                }
                else
                {
                    var activeStreaming = _registry.GetStreamingService(streamingApi);
                    await activeStreaming.StartAsync(language);

                    while (_audioBuffer.TryDequeue(out var args))
                    {
                        await activeStreaming.SendAudioAsync(args.Buffer, args.BytesRecorded);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Stream Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops streaming, finalizes WebSocket, and performs cleanup.
        /// </summary>
        public async Task StopAndFinalizeAsync(bool isTestMode, bool autoSend, IntPtr targetWindow, string streamingApi)
        {
            if (!isTestMode)
            {
                _inputInjector.CommitCurrentText();
                await _registry.GetStreamingService(streamingApi).StopAsync();
                if (autoSend)
                {
                    await _inputInjector.PressEnterAsync(targetWindow);
                }
            }
            else
            {
                var stopTasks = _registry.GetAllProviders()
                    .Select(p => p.StreamingService.StopAsync());
                await Task.WhenAll(stopTasks);
            }
        }

        /// <summary>
        /// Cancels all active streaming connections.
        /// </summary>
        public async Task CancelStreamingAsync(bool isTestMode, string streamingApi)
        {
            if (isTestMode)
            {
                var cancelTasks = _registry.GetAllProviders()
                    .Select(p => p.StreamingService.CancelAsync());
                await Task.WhenAll(cancelTasks);
            }
            else
            {
                await _registry.GetStreamingService(streamingApi).CancelAsync();
            }
        }

        #region Event Handlers

        private void OnTranscriptReceived(object? sender, TranscriptEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    var isTestMode = _settingsService.CurrentSettings.TestMode;
                    var streamingApi = _settingsService.CurrentSettings.StreamingModeApi;

                    if (!string.IsNullOrEmpty(e.Text))
                    {
                        if (isTestMode)
                        {
                            foreach (var provider in _registry.GetAllProviders())
                            {
                                if (sender == provider.StreamingService)
                                {
                                    ProviderTranscriptChanged?.Invoke(provider.Name, e.Text);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            TranscriptChanged?.Invoke(e.Text);
                        }
                    }

                    if (!isTestMode && e.IsFinal && !string.IsNullOrEmpty(e.Text))
                    {
                        _historyService.AddEntry(e.Text, streamingApi);
                        // TargetWindowHandle is managed by the coordinator
                        await _inputInjector.InjectStreamingTextAsync(e.Text, true);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[StreamingManager] Streaming inject error: {ex.Message}");
                }
            });
        }

        private void OnUtteranceEndReceived(object? sender, EventArgs e)
        {
            Debug.WriteLine("[StreamingManager] UtteranceEnd received.");
        }

        private void OnStreamingError(object? sender, string errorMessage)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                StatusChanged?.Invoke($"Stream error: {errorMessage}");
            });
        }

        #endregion
    }
}
