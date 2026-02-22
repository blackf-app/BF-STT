using BF_STT.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;

namespace BF_STT.Services
{
    /// <summary>
    /// Coordinates the full recording lifecycle: hotkey handling, hybrid mode decisions,
    /// streaming, batch processing, and audio data routing.
    /// Fires events to let the ViewModel update UI state.
    /// </summary>
    public class RecordingCoordinator
    {
        #region Dependencies

        private readonly AudioRecordingService _audioService;
        private readonly SttProviderRegistry _registry;
        private readonly InputInjector _inputInjector;
        private readonly SoundService _soundService;
        private readonly SettingsService _settingsService;
        private readonly HistoryService _historyService;

        #endregion

        #region State

        private bool _isRecording;
        private bool _isStreaming;
        private bool _isBatchProcessing;
        private bool _isToggleMode;
        private bool _isHybridDecisionMade;
        private bool _shouldAutoSend;
        private DateTime _hotkeyDownTime;
        private string? _lastRecordedFilePath;
        private IntPtr _targetWindowHandle;
        private ConcurrentQueue<AudioDataEventArgs> _audioBuffer = new();
        private DispatcherTimer _recordingTimer;
        private DispatcherTimer _hybridTimer;
        private TimeSpan _recordingDuration;

        /// <summary>
        /// Per-provider transcript text for Test Mode display.
        /// </summary>
        private readonly Dictionary<string, string> _providerTranscripts = new();

        private const int HybridThresholdMs = 300;

        #endregion

        #region Events

        /// <summary>Fired when StatusText should change.</summary>
        public event Action<string>? StatusChanged;

        /// <summary>Fired when the main transcript text changes.</summary>
        public event Action<string>? TranscriptChanged;

        /// <summary>Fired when a per-provider transcript changes (Test Mode).</summary>
        public event Action<string, string>? ProviderTranscriptChanged;

        /// <summary>Fired when IsRecording changes.</summary>
        public event Action<bool>? RecordingStateChanged;

        /// <summary>Fired when IsSending (streaming or batch) changes.</summary>
        public event Action? SendingStateChanged;

        /// <summary>Fired when AudioLevel changes.</summary>
        public event Action<float>? AudioLevelChanged;

        /// <summary>Fired to request CommandManager.InvalidateRequerySuggested on UI thread.</summary>
        public event Action? CommandsInvalidated;

        #endregion

        #region Properties

        public bool IsRecording => _isRecording;
        public bool IsSending => _isStreaming || _isBatchProcessing;
        public bool CanResend => !string.IsNullOrEmpty(_lastRecordedFilePath) 
                                 && File.Exists(_lastRecordedFilePath) 
                                 && !_isRecording && !IsSending;

        public string BatchModeApi
        {
            get => _settingsService.CurrentSettings.BatchModeApi;
            set
            {
                if (_settingsService.CurrentSettings.BatchModeApi != value)
                {
                    _settingsService.CurrentSettings.BatchModeApi = value;
                    _settingsService.SaveSettings(_settingsService.CurrentSettings);
                }
            }
        }

        public string StreamingModeApi
        {
            get => _settingsService.CurrentSettings.StreamingModeApi;
            set
            {
                if (_settingsService.CurrentSettings.StreamingModeApi != value)
                {
                    _settingsService.CurrentSettings.StreamingModeApi = value;
                    _settingsService.SaveSettings(_settingsService.CurrentSettings);
                }
            }
        }

        public bool IsTestMode => _settingsService.CurrentSettings.TestMode;

        #endregion

        #region Constructor

        public RecordingCoordinator(
            AudioRecordingService audioService,
            SttProviderRegistry registry,
            InputInjector inputInjector,
            SoundService soundService,
            SettingsService settingsService,
            HistoryService historyService)
        {
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _inputInjector = inputInjector ?? throw new ArgumentNullException(nameof(inputInjector));
            _soundService = soundService ?? throw new ArgumentNullException(nameof(soundService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));

            // Initialize per-provider transcript storage
            foreach (var p in _registry.GetAllProviders())
            {
                _providerTranscripts[p.Name] = string.Empty;
            }

            // Wire audio level updates
            _audioService.AudioLevelUpdated += (s, level) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    AudioLevelChanged?.Invoke(level);
                });
            };

            // Wire audio data to hybrid buffer / streaming
            _audioService.AudioDataAvailable += OnAudioDataAvailable;

            // Wire streaming events for all providers
            foreach (var provider in _registry.GetAllProviders())
            {
                provider.StreamingService.TranscriptReceived += OnTranscriptReceived;
                provider.StreamingService.UtteranceEndReceived += OnUtteranceEndReceived;
                provider.StreamingService.Error += OnStreamingError;
            }

            _recordingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _recordingTimer.Tick += RecordingTimer_Tick;

            _hybridTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(HybridThresholdMs)
            };
            _hybridTimer.Tick += HybridTimer_Tick;
        }

        #endregion

        #region Provider Transcript Helpers

        public string GetProviderTranscript(string providerName)
        {
            return _providerTranscripts.TryGetValue(providerName, out var val) ? val : string.Empty;
        }

        private void SetProviderTranscript(string providerName, string value)
        {
            _providerTranscripts[providerName] = value;
            ProviderTranscriptChanged?.Invoke(providerName, value);
        }

        #endregion

        #region Hotkey Handlers

        public void HandleHotkeyDown(bool autoSend)
        {
            _hotkeyDownTime = DateTime.Now;
            _shouldAutoSend = autoSend;

            if (_isRecording)
            {
                if (_isStreaming)
                {
                    // Streaming mode — ignore repeated KeyDown
                    return;
                }
                else if (_isToggleMode)
                {
                    // Batch mode — second press stops recording
                    StopRecording();
                    _isToggleMode = false;
                }
            }
            else
            {
                // Start new recording session
                StartRecording("Key");
            }
        }

        public void HandleHotkeyUp()
        {
            if (_isRecording)
            {
                var duration = DateTime.Now - _hotkeyDownTime;

                if (_isStreaming)
                {
                    // Streaming mode — release stops
                    StopRecording();
                }
                else if (!_isHybridDecisionMade && duration.TotalMilliseconds < HybridThresholdMs)
                {
                    // Short press → Batch Mode (Toggle)
                    _isToggleMode = true;
                    _isHybridDecisionMade = true;
                    _hybridTimer.Stop();

                    while (_audioBuffer.TryDequeue(out _)) { }

                    _soundService.PlayStartSound();
                    StatusChanged?.Invoke("Recording (Batch)...");
                }
            }
        }

        private void HybridTimer_Tick(object? sender, EventArgs e)
        {
            _hybridTimer.Stop();
            StartStreamingMode();
        }

        #endregion

        #region Recording Control

        public async void StartRecording(string? parameter = null)
        {
            try
            {
                if (_isRecording)
                {
                    // Cancel logic
                    _recordingTimer.Stop();
                    _hybridTimer.Stop();
                    await _audioService.StopRecordingAsync(discard: true);

                    if (IsTestMode)
                    {
                        var cancelTasks = _registry.GetAllProviders()
                            .Select(p => p.StreamingService.CancelAsync());
                        await Task.WhenAll(cancelTasks);
                    }
                    else
                    {
                        await _registry.GetStreamingService(StreamingModeApi).CancelAsync();
                    }
                    _soundService.PlayStopSound();

                    ResetState();
                    StatusChanged?.Invoke("Cancelled.");
                }
                else
                {
                    // Start new session
                    _targetWindowHandle = _inputInjector.LastExternalWindowHandle;
                    _inputInjector.ResetStreamingState();

                    if (!string.IsNullOrEmpty(_lastRecordedFilePath))
                    {
                        TryDeleteFile(_lastRecordedFilePath);
                        _lastRecordedFilePath = null;
                        CommandsInvalidated?.Invoke();
                    }

                    while (_audioBuffer.TryDequeue(out _)) { }

                    _isHybridDecisionMade = false;
                    _isStreaming = false;
                    _isToggleMode = false;
                    _isBatchProcessing = false;

                    _audioService.DeviceNumber = _settingsService.CurrentSettings.MicrophoneDeviceNumber;
                    _audioService.StartRecording();

                    bool isKeyTrigger = parameter is string s && s == "Key";

                    _isRecording = true;
                    RecordingStateChanged?.Invoke(true);
                    _recordingDuration = TimeSpan.Zero;

                    if (isKeyTrigger)
                    {
                        _isHybridDecisionMade = false;
                        StatusChanged?.Invoke("..."); // Pending decision
                        _hybridTimer.Start();
                    }
                    else
                    {
                        _isHybridDecisionMade = true;
                        _isToggleMode = true;
                        StatusChanged?.Invoke("Recording (Batch)...");
                        _soundService.PlayStartSound();
                    }

                    _recordingTimer.Start();

                    if (IsTestMode)
                    {
                        foreach (var p in _registry.GetAllProviders())
                        {
                            SetProviderTranscript(p.Name, string.Empty);
                        }
                    }
                    else
                    {
                        TranscriptChanged?.Invoke(string.Empty);
                    }
                    AudioLevelChanged?.Invoke(0);
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Error: {ex.Message}");
                ResetState();
            }
        }

        private async void StartStreamingMode()
        {
            _isStreaming = true;
            _isHybridDecisionMade = true;
            SendingStateChanged?.Invoke();

            _soundService.PlayStartSound();
            StatusChanged?.Invoke("Streaming... 00:00");

            try
            {
                var language = _settingsService.CurrentSettings.DefaultLanguage;
                if (IsTestMode)
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
                    var activeStreaming = _registry.GetStreamingService(StreamingModeApi);
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

        public async void StopRecording()
        {
            try
            {
                _recordingTimer.Stop();
                _hybridTimer.Stop();

                bool discardFile = _isStreaming;
                var filePath = await _audioService.StopRecordingAsync(discard: discardFile);

                _soundService.PlayStopSound();

                if (_isStreaming)
                {
                    StatusChanged?.Invoke("Finalizing Stream...");
                    if (!IsTestMode)
                    {
                        _inputInjector.CommitCurrentText();
                        await _registry.GetStreamingService(StreamingModeApi).StopAsync();
                        if (_shouldAutoSend)
                        {
                            await _inputInjector.PressEnterAsync(_targetWindowHandle);
                        }
                    }
                    else
                    {
                        var stopTasks = _registry.GetAllProviders()
                            .Select(p => p.StreamingService.StopAsync());
                        await Task.WhenAll(stopTasks);
                    }
                    StatusChanged?.Invoke("Done.");
                }
                else
                {
                    // Finish Batch
                    if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                    {
                        if (_audioService.HasMeaningfulAudio())
                        {
                            StatusChanged?.Invoke("Processing Batch...");
                            _isBatchProcessing = true;
                            _lastRecordedFilePath = filePath;
                            CommandsInvalidated?.Invoke();
                            SendingStateChanged?.Invoke();

                            if (IsTestMode)
                            {
                                _ = ProcessBatchTestModeAsync(filePath);
                            }
                            else
                            {
                                _ = ProcessBatchRecordingAsync(filePath, _targetWindowHandle);
                            }
                        }
                        else
                        {
                            StatusChanged?.Invoke("Silent — skipped.");
                            TryDeleteFile(filePath);
                        }
                    }
                    else
                    {
                        StatusChanged?.Invoke("No recording.");
                    }
                }

                _isRecording = false;
                RecordingStateChanged?.Invoke(false);
                _isStreaming = false;
                SendingStateChanged?.Invoke();
                AudioLevelChanged?.Invoke(0);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Error: {ex.Message}");
                ResetState();
            }
        }

        #endregion

        #region Batch Processing

        private async Task ProcessBatchRecordingAsync(string filePath, IntPtr targetWindow)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                if (!AudioSilenceDetector.ContainsSpeech(filePath))
                {
                    sw.Stop();
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        StatusChanged?.Invoke("Silent — skipped.");
                        TranscriptChanged?.Invoke(string.Empty);
                    });
                    return;
                }

                var activeBatch = _registry.GetBatchService(BatchModeApi);
                var language = _settingsService.CurrentSettings.DefaultLanguage;
                var transcript = await activeBatch.TranscribeAsync(filePath, language);
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
                        _historyService.AddEntry(finalTranscript, BatchModeApi);
                        await _inputInjector.InjectTextAsync(finalTranscript, targetWindow);
                        if (_shouldAutoSend)
                        {
                            await _inputInjector.PressEnterAsync(targetWindow);
                        }
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
            finally
            {
                _isBatchProcessing = false;
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    SendingStateChanged?.Invoke();
                    CommandsInvalidated?.Invoke();
                });
            }
        }

        private async Task ProcessBatchTestModeAsync(string filePath)
        {
            // Pre-API: File-level silence detection
            if (!AudioSilenceDetector.ContainsSpeech(filePath))
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    StatusChanged?.Invoke("Silent — skipped.");
                    foreach (var p in _registry.GetAllProviders())
                    {
                        SetProviderTranscript(p.Name, "[Skipped] Silent audio");
                    }
                });
                _isBatchProcessing = false;
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    SendingStateChanged?.Invoke();
                    CommandsInvalidated?.Invoke();
                });
                return;
            }

            var language = _settingsService.CurrentSettings.DefaultLanguage;

            // Create independent tasks for each provider using registry
            var providerTasks = _registry.GetAllProviders().Select(provider => Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var result = await provider.BatchService.TranscribeAsync(filePath, language);
                    sw.Stop();
                    var label = HallucinationFilter.IsHallucination(result) ? "[Hallucination]" : "";
                    var formatted = label == "" ? FormatTranscript(result) : $"{label} {result}";
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        SetProviderTranscript(provider.Name, $"[{sw.ElapsedMilliseconds}ms]\n{formatted}");
                    });
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        SetProviderTranscript(provider.Name, $"[{sw.ElapsedMilliseconds}ms] Failed: {ex.Message}");
                    });
                }
            })).ToArray();

            var overallSw = Stopwatch.StartNew();
            try
            {
                await Task.WhenAll(providerTasks);
                overallSw.Stop();
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    StatusChanged?.Invoke($"Done. ({overallSw.ElapsedMilliseconds}ms)");
                });
            }
            finally
            {
                _isBatchProcessing = false;
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    SendingStateChanged?.Invoke();
                    CommandsInvalidated?.Invoke();
                });
            }
        }

        #endregion

        #region Audio & Streaming Events

        private async void OnAudioDataAvailable(object? sender, AudioDataEventArgs e)
        {
            if (_isStreaming)
            {
                if (!_audioService.IsSpeaking) return;

                try
                {
                    if (IsTestMode)
                    {
                        var sendTasks = _registry.GetAllProviders()
                            .Select(p => p.StreamingService.SendAudioAsync(e.Buffer, e.BytesRecorded));
                        await Task.WhenAll(sendTasks);
                    }
                    else
                    {
                        await _registry.GetStreamingService(StreamingModeApi).SendAudioAsync(e.Buffer, e.BytesRecorded);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Coordinator] Audio send error: {ex.Message}");
                }
            }
            else if (!_isHybridDecisionMade)
            {
                _audioBuffer.Enqueue(e);
            }
        }

        private void OnTranscriptReceived(object? sender, TranscriptEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    if (!string.IsNullOrEmpty(e.Text))
                    {
                        if (IsTestMode)
                        {
                            // Identify which provider sent this transcript
                            foreach (var provider in _registry.GetAllProviders())
                            {
                                if (sender == provider.StreamingService)
                                {
                                    SetProviderTranscript(provider.Name, e.Text);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            TranscriptChanged?.Invoke(e.Text);
                        }
                    }

                    if (!IsTestMode && e.IsFinal && !string.IsNullOrEmpty(e.Text))
                    {
                        _historyService.AddEntry(e.Text, StreamingModeApi);
                        await _inputInjector.InjectStreamingTextAsync(
                            e.Text, true, _targetWindowHandle);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Coordinator] Streaming inject error: {ex.Message}");
                }
            });
        }

        private void OnUtteranceEndReceived(object? sender, EventArgs e)
        {
            Debug.WriteLine("[Coordinator] UtteranceEnd received.");
        }

        private void OnStreamingError(object? sender, string errorMessage)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                StatusChanged?.Invoke($"Stream error: {errorMessage}");
            });
        }

        #endregion

        #region Settings & Configuration

        public bool CheckApiConfiguration()
        {
            var settings = _settingsService.CurrentSettings;

            var batchError = _registry.ValidateApiKey(BatchModeApi, settings);
            if (batchError != null)
            {
                StatusChanged?.Invoke($"{batchError} for Batch.");
                return false;
            }

            var streamingError = _registry.ValidateApiKey(StreamingModeApi, settings);
            if (streamingError != null)
            {
                StatusChanged?.Invoke($"{streamingError} for Streaming.");
                return false;
            }

            return true;
        }

        public void UpdateSettingsFromRegistry()
        {
            var settings = _settingsService.CurrentSettings;
            _registry.UpdateAllSettings(settings);
            _historyService.UpdateMaxItems(settings.MaxHistoryItems);
        }

        public void ResendAudio()
        {
            if (!CanResend) return;

            StatusChanged?.Invoke("Resending Batch...");
            _isBatchProcessing = true;
            SendingStateChanged?.Invoke();

            if (IsTestMode)
            {
                _ = ProcessBatchTestModeAsync(_lastRecordedFilePath!);
            }
            else
            {
                _ = ProcessBatchRecordingAsync(_lastRecordedFilePath!, _targetWindowHandle);
            }
        }

        public void CleanupLastFile()
        {
            if (!string.IsNullOrEmpty(_lastRecordedFilePath))
            {
                TryDeleteFile(_lastRecordedFilePath);
            }
        }

        #endregion

        #region Utilities

        private string FormatTranscript(string transcript)
        {
            if (string.IsNullOrWhiteSpace(transcript)) return string.Empty;
            var trimmed = transcript.TrimEnd();
            return trimmed.EndsWith(".") ? trimmed + " " : trimmed + ". ";
        }

        private void RecordingTimer_Tick(object? sender, EventArgs e)
        {
            _recordingDuration = _recordingDuration.Add(TimeSpan.FromSeconds(1));
            string mode = _isStreaming ? "Streaming" : (_isToggleMode ? "Recording" : "Listening");
            StatusChanged?.Invoke($"{mode}... {_recordingDuration:mm\\:ss}");
        }

        private void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) { Debug.WriteLine($"[Coordinator] Failed to delete file: {ex.Message}"); }
        }

        public void ResetState()
        {
            _isRecording = false;
            RecordingStateChanged?.Invoke(false);
            _isStreaming = false;
            _isBatchProcessing = false;
            _isToggleMode = false;
            _isHybridDecisionMade = false;
            _shouldAutoSend = false;
            SendingStateChanged?.Invoke();
            AudioLevelChanged?.Invoke(0);
            foreach (var p in _registry.GetAllProviders())
            {
                SetProviderTranscript(p.Name, string.Empty);
            }
            TranscriptChanged?.Invoke(string.Empty);
            while (_audioBuffer.TryDequeue(out _)) { }
        }

        #endregion
    }
}
