using BF_STT.Models;
using BF_STT.Services;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.IO;
using System.Diagnostics;

namespace BF_STT.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        #region Fields

        private readonly AudioRecordingService _audioService;
        private readonly SttProviderRegistry _registry;
        private readonly InputInjector _inputInjector;
        private readonly SoundService _soundService;
        private readonly SettingsService _settingsService;
        private readonly HistoryService _historyService;
        private bool _isHistoryVisible;
        private bool _isHistoryAtTop;
        
        private DispatcherTimer _recordingTimer;
        private TimeSpan _recordingDuration;

        private string _transcriptText = string.Empty;
        private string _statusText = "Ready";
        private bool _isRecording;
        private bool _isStreaming;
        private bool _isBatchProcessing;
        private float _audioLevel;
        private DateTime _f3DownTime;
        private bool _isToggleMode;
        private string? _lastRecordedFilePath;
        private DispatcherTimer _hybridTimer;
        private bool _isHybridDecisionMade;
        private ConcurrentQueue<AudioDataEventArgs> _audioBuffer = new();
        private IntPtr _targetWindowHandle;
        private bool _shouldAutoSend;
        private const int HybridThresholdMs = 300;

        /// <summary>
        /// Stores per-provider transcript text for Test Mode display.
        /// Key = provider name, Value = transcript text.
        /// </summary>
        private readonly Dictionary<string, string> _providerTranscripts = new();

        #endregion

        #region Constructor

        public MainViewModel(
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
                    AudioLevel = level;
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

            // Commands
            StartRecordingCommand = new RelayCommand(StartRecording, _ => true);
            StopRecordingCommand = new RelayCommand(StopRecording, _ => IsRecording);
            ResendAudioCommand = new RelayCommand(ResendAudio, CanResendAudio);
            CloseCommand = new RelayCommand(_ => {
                if (!string.IsNullOrEmpty(_lastRecordedFilePath))
                {
                    TryDeleteFile(_lastRecordedFilePath);
                }
                System.Windows.Application.Current.Shutdown();
            });
            OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
            ToggleHistoryCommand = new RelayCommand(_ => IsHistoryVisible = !IsHistoryVisible);
            ClearHistoryCommand = new RelayCommand(_ => _historyService.ClearHistory());
            CopyHistoryItemCommand = new RelayCommand(item => 
            {
                if (item is HistoryItem historyItem)
                {
                    System.Windows.Clipboard.SetText(historyItem.Text);
                    StatusText = "Copied to clipboard.";
                }
            });
        }

        #endregion

        #region Properties

        public ObservableCollection<string> AvailableApis { get; } = new ObservableCollection<string> { "Deepgram", "Speechmatics", "Soniox", "OpenAI" };

        public string BatchModeApi
        {
            get => _settingsService.CurrentSettings.BatchModeApi;
            set
            {
                if (_settingsService.CurrentSettings.BatchModeApi != value)
                {
                    _settingsService.CurrentSettings.BatchModeApi = value;
                    _settingsService.SaveSettings(_settingsService.CurrentSettings);
                    OnPropertyChanged();
                    CheckApiConfiguration();
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
                    OnPropertyChanged();
                    CheckApiConfiguration();
                }
            }
        }

        public string TranscriptText
        {
            get => _transcriptText;
            set => SetProperty(ref _transcriptText, value);
        }

        public bool IsTestMode => _settingsService.CurrentSettings.TestMode;

        // Per-provider transcripts for Test Mode UI panels
        public string DeepgramTranscript
        {
            get => GetProviderTranscript("Deepgram");
            set => SetProviderTranscript("Deepgram", value);
        }

        public string SpeechmaticsTranscript
        {
            get => GetProviderTranscript("Speechmatics");
            set => SetProviderTranscript("Speechmatics", value);
        }

        public string SonioxTranscript
        {
            get => GetProviderTranscript("Soniox");
            set => SetProviderTranscript("Soniox", value);
        }

        public string OpenAITranscript
        {
            get => GetProviderTranscript("OpenAI");
            set => SetProviderTranscript("OpenAI", value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public bool IsRecording
        {
            get => _isRecording;
            set
            {
                if (SetProperty(ref _isRecording, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public bool IsSending
        {
            get => _isStreaming || _isBatchProcessing;
        }

        public float AudioLevel
        {
            get => _audioLevel;
            set => SetProperty(ref _audioLevel, value);
        }

        public ICommand StartRecordingCommand { get; }
        public ICommand StopRecordingCommand { get; }
        public ICommand ResendAudioCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand ToggleHistoryCommand { get; }
        public ICommand ClearHistoryCommand { get; }
        public ICommand CopyHistoryItemCommand { get; }

        public HistoryService HistoryService => _historyService;

        public bool IsHistoryVisible
        {
            get => _isHistoryVisible;
            set
            {
                if (SetProperty(ref _isHistoryVisible, value))
                {
                    OnPropertyChanged(nameof(IsHistoryTopVisible));
                    OnPropertyChanged(nameof(IsHistoryBottomVisible));
                }
            }
        }

        public bool IsHistoryAtTop
        {
            get => _isHistoryAtTop;
            set
            {
                if (SetProperty(ref _isHistoryAtTop, value))
                {
                    OnPropertyChanged(nameof(IsHistoryTopVisible));
                    OnPropertyChanged(nameof(IsHistoryBottomVisible));
                }
            }
        }

        public bool IsHistoryTopVisible => IsHistoryVisible && IsHistoryAtTop;
        public bool IsHistoryBottomVisible => IsHistoryVisible && !IsHistoryAtTop;

        #endregion

        #region Provider Transcript Helpers

        private string GetProviderTranscript(string providerName)
        {
            return _providerTranscripts.TryGetValue(providerName, out var val) ? val : string.Empty;
        }

        private void SetProviderTranscript(string providerName, string value)
        {
            _providerTranscripts[providerName] = value;
            OnPropertyChanged($"{providerName}Transcript");
        }

        #endregion

        #region Hotkey Handlers (F3)

        private void OnUtteranceEndReceived(object? sender, EventArgs e)
        {
            Debug.WriteLine("[MainViewModel] UtteranceEnd received.");
        }

        public void OnF3KeyDown() => HandleHotkeyKeyDown(false);
        public void OnStopAndSendKeyDown() => HandleHotkeyKeyDown(true);

        private void HandleHotkeyKeyDown(bool autoSend)
        {
            _f3DownTime = DateTime.Now;
            _shouldAutoSend = autoSend;

            if (IsRecording)
            {
                if (_isStreaming)
                {
                    // Streaming mode — ignore repeated KeyDown
                    return;
                }
                else if (_isToggleMode)
                {
                    // Batch mode — second press stops recording
                    if (StopRecordingCommand.CanExecute(null))
                    {
                        StopRecordingCommand.Execute(null);
                    }
                    _isToggleMode = false;
                }
            }
            else
            {
                // Start new recording session
                if (StartRecordingCommand.CanExecute("Key"))
                {
                    StartRecordingCommand.Execute("Key");
                }
            }
        }

        public void OnF3KeyUp() => HandleHotkeyKeyUp();
        public void OnStopAndSendKeyUp() => HandleHotkeyKeyUp();

        private void HandleHotkeyKeyUp()
        {
            if (IsRecording)
            {
                var duration = DateTime.Now - _f3DownTime;
                
                if (_isStreaming)
                {
                    // Streaming mode — release F3 stops
                    if (StopRecordingCommand.CanExecute(null))
                    {
                        StopRecordingCommand.Execute(null);
                    }
                }
                else if (!_isHybridDecisionMade && duration.TotalMilliseconds < HybridThresholdMs)
                {
                    // Short press → Batch Mode (Toggle)
                    _isToggleMode = true;
                    _isHybridDecisionMade = true;
                    _hybridTimer.Stop();
                    
                    while (_audioBuffer.TryDequeue(out _)) { }

                    _soundService.PlayStartSound();
                    StatusText = "Recording (Batch)...";
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

        private async void StartRecording(object? parameter)
        {
            try
            {
                if (IsRecording)
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
                    StatusText = "Cancelled.";
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
                        System.Windows.Application.Current.Dispatcher.Invoke(() => CommandManager.InvalidateRequerySuggested());
                    }
                    
                    while (_audioBuffer.TryDequeue(out _)) { }
                    
                    _isHybridDecisionMade = false;
                    _isStreaming = false;
                    _isToggleMode = false;
                    _isBatchProcessing = false;

                    _audioService.DeviceNumber = _settingsService.CurrentSettings.MicrophoneDeviceNumber;
                    _audioService.StartRecording();
                    
                    bool isKeyTrigger = parameter is string s && s == "Key";

                    IsRecording = true;
                    _recordingDuration = TimeSpan.Zero;
                    
                    if (isKeyTrigger)
                    {
                        _isHybridDecisionMade = false;
                        StatusText = "..."; // Pending decision
                        _hybridTimer.Start();
                    }
                    else
                    {
                        _isHybridDecisionMade = true;
                        _isToggleMode = true;
                        StatusText = "Recording (Batch)...";
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
                        TranscriptText = string.Empty;
                    }
                    AudioLevel = 0;
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
                ResetState();
            }
        }

        private async void StartStreamingMode()
        {
            _isStreaming = true;
            _isHybridDecisionMade = true;
            OnPropertyChanged(nameof(IsSending));
            
            _soundService.PlayStartSound();
            StatusText = "Streaming... 00:00";

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
                StatusText = $"Stream Error: {ex.Message}";
            }
        }

        private async void StopRecording(object? parameter)
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
                    StatusText = "Finalizing Stream...";
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
                    StatusText = "Done.";
                }
                else
                {
                    // Finish Batch
                    if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                    {
                        if (_audioService.HasMeaningfulAudio())
                        {
                            StatusText = "Processing Batch...";
                            _isBatchProcessing = true;
                            _lastRecordedFilePath = filePath;
                            System.Windows.Application.Current.Dispatcher.Invoke(() => CommandManager.InvalidateRequerySuggested());
                            OnPropertyChanged(nameof(IsSending));
                            
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
                            StatusText = "Silent — skipped.";
                            TryDeleteFile(filePath);
                        }
                    }
                    else
                    {
                        StatusText = "No recording.";
                    }
                }

                IsRecording = false;
                _isStreaming = false;
                OnPropertyChanged(nameof(IsSending));
                AudioLevel = 0;
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
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
                        StatusText = "Silent — skipped.";
                        TranscriptText = string.Empty;
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
                        StatusText = $"Hallucination — skipped. ({sw.ElapsedMilliseconds}ms)";
                        TranscriptText = string.Empty;
                        return;
                    }

                    var finalTranscript = FormatTranscript(transcript);
                    TranscriptText = finalTranscript;
                    StatusText = $"Done. ({sw.ElapsedMilliseconds}ms)";

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
                    StatusText = $"Error: {ex.Message}";
                    TranscriptText = "Failed to get transcript.";
                });
            }
            finally
            {
                _isBatchProcessing = false;
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(IsSending));
                    CommandManager.InvalidateRequerySuggested();
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
                    StatusText = "Silent — skipped.";
                    foreach (var p in _registry.GetAllProviders())
                    {
                        SetProviderTranscript(p.Name, "[Skipped] Silent audio");
                    }
                });
                _isBatchProcessing = false;
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(IsSending));
                    CommandManager.InvalidateRequerySuggested();
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
                    StatusText = $"Done. ({overallSw.ElapsedMilliseconds}ms)";
                });
            }
            finally
            {
                _isBatchProcessing = false;
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(IsSending));
                    CommandManager.InvalidateRequerySuggested();
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
                    Debug.WriteLine($"[MainVM] Audio send error: {ex.Message}");
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
                            TranscriptText = e.Text;
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
                    Debug.WriteLine($"[MainVM] Streaming inject error: {ex.Message}");
                }
            });
        }

        private void OnStreamingError(object? sender, string errorMessage)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                StatusText = $"Stream error: {errorMessage}";
            });
        }

        #endregion

        #region Settings & Configuration

        private bool CheckApiConfiguration()
        {
            var settings = _settingsService.CurrentSettings;

            var batchError = _registry.ValidateApiKey(BatchModeApi, settings);
            if (batchError != null)
            {
                StatusText = $"{batchError} for Batch.";
                return false;
            }

            var streamingError = _registry.ValidateApiKey(StreamingModeApi, settings);
            if (streamingError != null)
            {
                StatusText = $"{streamingError} for Streaming.";
                return false;
            }

            return true;
        }

        private void OpenSettings()
        {
            System.Windows.Application.Current.MainWindow.Hide();
            var settingsWindow = new SettingsWindow(_settingsService);
            if (settingsWindow.ShowDialog() == true)
            {
                var settings = _settingsService.CurrentSettings;
                OnPropertyChanged(nameof(BatchModeApi));
                OnPropertyChanged(nameof(StreamingModeApi));
                OnPropertyChanged(nameof(IsTestMode));

                _registry.UpdateAllSettings(settings);
                _historyService.UpdateMaxItems(settings.MaxHistoryItems);
                
                StatusText = "Settings updated.";
            }
            System.Windows.Application.Current.MainWindow.Show();
        }

        private bool CanResendAudio(object? parameter)
        {
            return !string.IsNullOrEmpty(_lastRecordedFilePath) && File.Exists(_lastRecordedFilePath) && !IsRecording && !IsSending;
        }

        private void ResendAudio(object? parameter)
        {
            if (!CanResendAudio(null)) return;
            
            StatusText = "Resending Batch...";
            _isBatchProcessing = true;
            OnPropertyChanged(nameof(IsSending));
            
            if (IsTestMode)
            {
                _ = ProcessBatchTestModeAsync(_lastRecordedFilePath!);
            }
            else
            {
                _ = ProcessBatchRecordingAsync(_lastRecordedFilePath!, _targetWindowHandle);
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
            StatusText = $"{mode}... {_recordingDuration:mm\\:ss}";
        }

        private void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) { Debug.WriteLine($"[MainVM] Failed to delete file: {ex.Message}"); }
        }

        private void ResetState()
        {
            IsRecording = false;
            _isStreaming = false;
            _isBatchProcessing = false;
            _isToggleMode = false;
            _isHybridDecisionMade = false;
            _shouldAutoSend = false;
            OnPropertyChanged(nameof(IsSending));
            AudioLevel = 0;
            foreach (var p in _registry.GetAllProviders())
            {
                SetProviderTranscript(p.Name, string.Empty);
            }
            TranscriptText = string.Empty;
            while (_audioBuffer.TryDequeue(out _)) { }
        }

        #endregion
    }
}
