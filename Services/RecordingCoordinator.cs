using BF_STT.Models;
using BF_STT.Services.States;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;

namespace BF_STT.Services
{
    /// <summary>
    /// Coordinates the full recording lifecycle using the State Pattern.
    /// States: Idle → HybridPending → (BatchRecording | Streaming) → Processing → Idle
    /// Fires events to let the ViewModel update UI state.
    /// </summary>
    public class RecordingCoordinator
    {
        #region Dependencies (internal for state access)

        internal readonly AudioRecordingService AudioService;
        internal readonly SttProviderRegistry Registry;
        internal readonly InputInjector InputInjector;
        internal readonly SoundService SoundService;
        internal readonly SettingsService SettingsService;
        internal readonly HistoryService HistoryService;

        #endregion

        #region State

        private IRecordingState _currentState;

        /// <summary>Current auto-send flag for the active session.</summary>
        internal bool ShouldAutoSend { get; set; }

        /// <summary>Timestamp when the hotkey was pressed down.</summary>
        internal DateTime HotkeyDownTime { get; set; }

        /// <summary>Window handle captured at session start for text injection.</summary>
        internal IntPtr TargetWindowHandle { get; set; }

        /// <summary>Last recorded audio bytes for Resend feature.</summary>
        private byte[]? _lastAudioData;

        /// <summary>Audio buffer for hybrid pending state.</summary>
        private ConcurrentQueue<AudioDataEventArgs> _audioBuffer = new();

        private DispatcherTimer _recordingTimer;
        private DispatcherTimer _hybridTimer;
        private TimeSpan _recordingDuration;

        /// <summary>Per-provider transcript text for Test Mode display.</summary>
        private readonly Dictionary<string, string> _providerTranscripts = new();

        internal const int HybridThresholdMs = 300;

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

        public IRecordingState CurrentState => _currentState;
        public RecordingStateEnum State => _currentState.StateId;

        public bool IsRecording => State == RecordingStateEnum.HybridPending
                                || State == RecordingStateEnum.BatchRecording
                                || State == RecordingStateEnum.Streaming;

        public bool IsSending => State == RecordingStateEnum.Streaming
                              || State == RecordingStateEnum.Processing;

        public bool CanResend => _lastAudioData != null
                              && _lastAudioData.Length > 0
                              && State == RecordingStateEnum.Idle;

        public string BatchModeApi
        {
            get => SettingsService.CurrentSettings.BatchModeApi;
            set
            {
                if (SettingsService.CurrentSettings.BatchModeApi != value)
                {
                    SettingsService.CurrentSettings.BatchModeApi = value;
                    SettingsService.SaveSettings(SettingsService.CurrentSettings);
                }
            }
        }

        public string StreamingModeApi
        {
            get => SettingsService.CurrentSettings.StreamingModeApi;
            set
            {
                if (SettingsService.CurrentSettings.StreamingModeApi != value)
                {
                    SettingsService.CurrentSettings.StreamingModeApi = value;
                    SettingsService.SaveSettings(SettingsService.CurrentSettings);
                }
            }
        }

        public bool IsTestMode => SettingsService.CurrentSettings.TestMode;

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
            AudioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            InputInjector = inputInjector ?? throw new ArgumentNullException(nameof(inputInjector));
            SoundService = soundService ?? throw new ArgumentNullException(nameof(soundService));
            SettingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            HistoryService = historyService ?? throw new ArgumentNullException(nameof(historyService));

            _currentState = IdleState.Instance;

            // Initialize per-provider transcript storage
            foreach (var p in Registry.GetAllProviders())
            {
                _providerTranscripts[p.Name] = string.Empty;
            }

            // Wire audio level updates
            AudioService.AudioLevelUpdated += (s, level) =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    AudioLevelChanged?.Invoke(level);
                });
            };

            // Wire audio data to current state
            AudioService.AudioDataAvailable += OnAudioDataAvailable;

            // Wire streaming events for all providers
            foreach (var provider in Registry.GetAllProviders())
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

        #region State Transitions

        /// <summary>
        /// Transitions to a new state and fires relevant UI events.
        /// </summary>
        internal void TransitionTo(IRecordingState newState)
        {
            var oldState = _currentState;
            _currentState = newState;

            Debug.WriteLine($"[Coordinator] State: {oldState.StateId} → {newState.StateId}");

            // Fire recording state change if transitioning in/out of recording
            bool wasRecording = oldState.StateId == RecordingStateEnum.HybridPending
                             || oldState.StateId == RecordingStateEnum.BatchRecording
                             || oldState.StateId == RecordingStateEnum.Streaming;
            bool nowRecording = IsRecording;
            if (wasRecording != nowRecording)
            {
                RecordingStateChanged?.Invoke(nowRecording);
            }

            SendingStateChanged?.Invoke();
            CommandsInvalidated?.Invoke();
        }

        #endregion

        #region Public API (delegated to state)

        public void HandleHotkeyDown(bool autoSend)
        {
            HotkeyDownTime = DateTime.Now;
            ShouldAutoSend = autoSend;
            _currentState.HandleHotkeyDown(this, autoSend);
        }

        public void HandleHotkeyUp()
        {
            _currentState.HandleHotkeyUp(this);
        }

        public void StartRecording(string? parameter = null)
        {
            if (IsRecording)
            {
                // If already recording, treat as cancel
                CancelRecording();
            }
            else if (State == RecordingStateEnum.Idle || State == RecordingStateEnum.Failed)
            {
                _currentState.HandleStartButton(this);
            }
        }

        public void StopRecording()
        {
            if (State == RecordingStateEnum.BatchRecording)
            {
                StopAndProcessBatch();
            }
            else if (State == RecordingStateEnum.Streaming)
            {
                StopStreamingAndFinalize();
            }
        }

        #endregion

        #region Internal Methods (called by state classes)

        /// <summary>Begins a new recording session, resetting state and capturing target window.</summary>
        internal void BeginNewSession(bool autoSend)
        {
            ShouldAutoSend = autoSend;
            TargetWindowHandle = InputInjector.LastExternalWindowHandle;
            InputInjector.ResetStreamingState();

            // Clear previous audio data
            _lastAudioData = null;
            CommandsInvalidated?.Invoke();

            ClearAudioBuffer();

            if (IsTestMode)
            {
                foreach (var p in Registry.GetAllProviders())
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

        /// <summary>Starts audio capture and transitions to appropriate state.</summary>
        internal void StartAudioCapture(bool isKeyTrigger)
        {
            try
            {
                AudioService.DeviceNumber = SettingsService.CurrentSettings.MicrophoneDeviceNumber;
                AudioService.StartRecording();

                _recordingDuration = TimeSpan.Zero;
                _recordingTimer.Start();

                if (isKeyTrigger)
                {
                    FireStatusChanged("..."); // Pending hybrid decision
                    _hybridTimer.Start();
                    TransitionTo(HybridPendingState.Instance);
                }
                else
                {
                    PlayStartSound();
                    FireStatusChanged("Recording (Batch)...");
                    TransitionTo(BatchRecordingState.Instance);
                }
            }
            catch (Exception ex)
            {
                FireStatusChanged($"Error: {ex.Message}");
                TransitionTo(FailedState.Instance);
            }
        }

        /// <summary>Enters streaming mode — starts WebSocket connections and flushes buffered audio.</summary>
        internal async void EnterStreamingMode()
        {
            _hybridTimer.Stop();
            TransitionTo(StreamingState.Instance);

            PlayStartSound();
            FireStatusChanged("Streaming... 00:00");

            try
            {
                var language = SettingsService.CurrentSettings.DefaultLanguage;
                if (IsTestMode)
                {
                    var startTasks = Registry.GetAllProviders()
                        .Select(p => p.StreamingService.StartAsync(language));
                    await Task.WhenAll(startTasks);

                    // Flush buffer to all WebSockets
                    while (_audioBuffer.TryDequeue(out var args))
                    {
                        var sendTasks = Registry.GetAllProviders()
                            .Select(p => p.StreamingService.SendAudioAsync(args.Buffer, args.BytesRecorded));
                        await Task.WhenAll(sendTasks);
                    }
                }
                else
                {
                    var activeStreaming = Registry.GetStreamingService(StreamingModeApi);
                    await activeStreaming.StartAsync(language);

                    while (_audioBuffer.TryDequeue(out var args))
                    {
                        await activeStreaming.SendAudioAsync(args.Buffer, args.BytesRecorded);
                    }
                }
            }
            catch (Exception ex)
            {
                FireStatusChanged($"Stream Error: {ex.Message}");
            }
        }

        /// <summary>Stops recording and sends audio to batch API for processing.</summary>
        internal async void StopAndProcessBatch()
        {
            try
            {
                _recordingTimer.Stop();
                _hybridTimer.Stop();

                var audioData = await AudioService.StopRecordingAsync();

                SoundService.PlayStopSound();

                if (audioData != null && audioData.Length > 0)
                {
                    if (AudioService.HasMeaningfulAudio())
                    {
                        FireStatusChanged("Processing Batch...");
                        _lastAudioData = audioData;
                        TransitionTo(ProcessingState.Instance);

                        if (IsTestMode)
                        {
                            _ = ProcessBatchTestModeAsync(audioData);
                        }
                        else
                        {
                            _ = ProcessBatchRecordingAsync(audioData, TargetWindowHandle);
                        }
                    }
                    else
                    {
                        FireStatusChanged("Silent — skipped.");
                        TransitionTo(IdleState.Instance);
                    }
                }
                else
                {
                    FireStatusChanged("No recording.");
                    TransitionTo(IdleState.Instance);
                }

                AudioLevelChanged?.Invoke(0);
            }
            catch (Exception ex)
            {
                FireStatusChanged($"Error: {ex.Message}");
                TransitionTo(FailedState.Instance);
            }
        }

        /// <summary>Stops streaming, finalizes WebSocket, and transitions to Idle.</summary>
        internal async void StopStreamingAndFinalize()
        {
            try
            {
                _recordingTimer.Stop();
                _hybridTimer.Stop();

                // Discard the recording file — streaming doesn't need it
                await AudioService.StopRecordingAsync(discard: true);

                SoundService.PlayStopSound();

                FireStatusChanged("Finalizing Stream...");
                if (!IsTestMode)
                {
                    InputInjector.CommitCurrentText();
                    await Registry.GetStreamingService(StreamingModeApi).StopAsync();
                    if (ShouldAutoSend)
                    {
                        await InputInjector.PressEnterAsync(TargetWindowHandle);
                    }
                }
                else
                {
                    var stopTasks = Registry.GetAllProviders()
                        .Select(p => p.StreamingService.StopAsync());
                    await Task.WhenAll(stopTasks);
                }

                FireStatusChanged("Done.");
                TransitionTo(IdleState.Instance);
                AudioLevelChanged?.Invoke(0);
            }
            catch (Exception ex)
            {
                FireStatusChanged($"Error: {ex.Message}");
                TransitionTo(FailedState.Instance);
            }
        }

        /// <summary>Cancels the current recording session and returns to Idle.</summary>
        internal async void CancelRecording()
        {
            try
            {
                _recordingTimer.Stop();
                _hybridTimer.Stop();
                await AudioService.StopRecordingAsync(discard: true);

                if (State == RecordingStateEnum.Streaming || State == RecordingStateEnum.HybridPending)
                {
                    if (IsTestMode)
                    {
                        var cancelTasks = Registry.GetAllProviders()
                            .Select(p => p.StreamingService.CancelAsync());
                        await Task.WhenAll(cancelTasks);
                    }
                    else
                    {
                        await Registry.GetStreamingService(StreamingModeApi).CancelAsync();
                    }
                }

                SoundService.PlayStopSound();
                FireStatusChanged("Cancelled.");
                ResetState();
            }
            catch (Exception ex)
            {
                FireStatusChanged($"Error: {ex.Message}");
                ResetState();
            }
        }

        internal void PlayStartSound() => SoundService.PlayStartSound();
        internal void StopHybridTimer() => _hybridTimer.Stop();
        internal void ClearAudioBuffer()
        {
            while (_audioBuffer.TryDequeue(out _)) { }
        }
        internal void EnqueueAudioBuffer(AudioDataEventArgs e) => _audioBuffer.Enqueue(e);
        internal void FireStatusChanged(string status) => StatusChanged?.Invoke(status);

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

        #region Batch Processing

        private async Task ProcessBatchRecordingAsync(byte[] audioData, IntPtr targetWindow)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                if (!AudioSilenceDetector.ContainsSpeech(audioData))
                {
                    sw.Stop();
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        FireStatusChanged("Silent — skipped.");
                        TranscriptChanged?.Invoke(string.Empty);
                    });
                    return;
                }

                var activeBatch = Registry.GetBatchService(BatchModeApi);
                var language = SettingsService.CurrentSettings.DefaultLanguage;
                var transcript = await activeBatch.TranscribeAsync(audioData, language);
                sw.Stop();

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    if (HallucinationFilter.IsHallucination(transcript))
                    {
                        FireStatusChanged($"Hallucination — skipped. ({sw.ElapsedMilliseconds}ms)");
                        TranscriptChanged?.Invoke(string.Empty);
                        return;
                    }

                    var finalTranscript = FormatTranscript(transcript);
                    TranscriptChanged?.Invoke(finalTranscript);
                    FireStatusChanged($"Done. ({sw.ElapsedMilliseconds}ms)");

                    if (!string.IsNullOrWhiteSpace(finalTranscript))
                    {
                        HistoryService.AddEntry(finalTranscript, BatchModeApi);
                        await InputInjector.InjectTextAsync(finalTranscript, targetWindow, ShouldAutoSend);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    FireStatusChanged($"Error: {ex.Message}");
                    TranscriptChanged?.Invoke("Failed to get transcript.");
                });
            }
            finally
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    TransitionTo(IdleState.Instance);
                });
            }
        }

        private async Task ProcessBatchTestModeAsync(byte[] audioData)
        {
            // Pre-API: silence detection on in-memory WAV data
            if (!AudioSilenceDetector.ContainsSpeech(audioData))
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    FireStatusChanged("Silent — skipped.");
                    foreach (var p in Registry.GetAllProviders())
                    {
                        SetProviderTranscript(p.Name, "[Skipped] Silent audio");
                    }
                });
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    TransitionTo(IdleState.Instance);
                });
                return;
            }

            var language = SettingsService.CurrentSettings.DefaultLanguage;

            // Create independent tasks for each provider
            var providerTasks = Registry.GetAllProviders().Select(provider => Task.Run(async () =>
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
                    FireStatusChanged($"Done. ({overallSw.ElapsedMilliseconds}ms)");
                });
            }
            finally
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    TransitionTo(IdleState.Instance);
                });
            }
        }

        #endregion

        #region Audio & Streaming Events

        private void OnAudioDataAvailable(object? sender, AudioDataEventArgs e)
        {
            _currentState.HandleAudioData(this, e);
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
                            foreach (var provider in Registry.GetAllProviders())
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
                        HistoryService.AddEntry(e.Text, StreamingModeApi);
                        await InputInjector.InjectStreamingTextAsync(
                            e.Text, true, TargetWindowHandle);
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
                FireStatusChanged($"Stream error: {errorMessage}");
            });
        }

        #endregion

        #region Settings & Configuration

        public bool CheckApiConfiguration()
        {
            var settings = SettingsService.CurrentSettings;

            var batchError = Registry.ValidateApiKey(BatchModeApi, settings);
            if (batchError != null)
            {
                FireStatusChanged($"{batchError} for Batch.");
                return false;
            }

            var streamingError = Registry.ValidateApiKey(StreamingModeApi, settings);
            if (streamingError != null)
            {
                FireStatusChanged($"{streamingError} for Streaming.");
                return false;
            }

            return true;
        }

        public void UpdateSettingsFromRegistry()
        {
            var settings = SettingsService.CurrentSettings;
            Registry.UpdateAllSettings(settings);
            HistoryService.UpdateMaxItems(settings.MaxHistoryItems);
        }

        public void ResendAudio()
        {
            if (!CanResend || _lastAudioData == null) return;

            FireStatusChanged("Resending Batch...");
            TransitionTo(ProcessingState.Instance);

            if (IsTestMode)
            {
                _ = ProcessBatchTestModeAsync(_lastAudioData);
            }
            else
            {
                _ = ProcessBatchRecordingAsync(_lastAudioData, TargetWindowHandle);
            }
        }

        public async Task SendHistoryItemAsync(HistoryItem item)
        {
            if (item == null) return;
            
            FireStatusChanged("Sending history item...");
            // Use the last known external window handle (the one focused before clicking the app)
            var handleToUse = InputInjector.LastExternalWindowHandle;
            await InputInjector.InjectTextAsync(item.Text, handleToUse);
            FireStatusChanged("Sent.");
        }

        public void CleanupLastFile()
        {
            _lastAudioData = null;
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
            string mode = State switch
            {
                RecordingStateEnum.Streaming => "Streaming",
                RecordingStateEnum.BatchRecording => "Recording",
                _ => "Listening"
            };
            FireStatusChanged($"{mode}... {_recordingDuration:mm\\:ss}");
        }

        private void HybridTimer_Tick(object? sender, EventArgs e)
        {
            _hybridTimer.Stop();
            _currentState.HandleHybridTimeout(this);
        }

        public void ResetState()
        {
            TransitionTo(IdleState.Instance);
            ShouldAutoSend = false;
            AudioLevelChanged?.Invoke(0);
            foreach (var p in Registry.GetAllProviders())
            {
                SetProviderTranscript(p.Name, string.Empty);
            }
            TranscriptChanged?.Invoke(string.Empty);
            ClearAudioBuffer();
        }

        #endregion
    }
}
