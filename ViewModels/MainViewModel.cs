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
        private readonly AudioRecordingService _audioService;
        private readonly IBatchSttService _deepgramBatchService;
        private readonly IStreamingSttService _deepgramStreamingService;
        private readonly IBatchSttService _speechmaticsBatchService;
        private readonly IStreamingSttService _speechmaticsStreamingService;
        private readonly IBatchSttService _sonioxBatchService;
        private readonly IStreamingSttService _sonioxStreamingService;
        private readonly IBatchSttService _openaiBatchService;
        private readonly IStreamingSttService _openaiStreamingService;

        private readonly InputInjector _inputInjector;
        private readonly SoundService _soundService;
        private readonly SettingsService _settingsService;
        
        private DispatcherTimer _recordingTimer;
        private TimeSpan _recordingDuration;

        private string _transcriptText = string.Empty;
        private string _deepgramTranscript = string.Empty;
        private string _speechmaticsTranscript = string.Empty;
        private string _sonioxTranscript = string.Empty;
        private string _openaiTranscript = string.Empty;
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
        private const int HybridThresholdMs = 300;

        public MainViewModel(
            AudioRecordingService audioService, 
            IBatchSttService deepgramBatchService,
            IStreamingSttService deepgramStreamingService, 
            IBatchSttService speechmaticsBatchService,
            IStreamingSttService speechmaticsStreamingService,
            IBatchSttService sonioxBatchService,
            IStreamingSttService sonioxStreamingService,
            IBatchSttService openaiBatchService,
            IStreamingSttService openaiStreamingService,
            InputInjector inputInjector, 
            SoundService soundService,
            SettingsService settingsService)
        {
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _deepgramBatchService = deepgramBatchService ?? throw new ArgumentNullException(nameof(deepgramBatchService));
            _deepgramStreamingService = deepgramStreamingService ?? throw new ArgumentNullException(nameof(deepgramStreamingService));
            _speechmaticsBatchService = speechmaticsBatchService ?? throw new ArgumentNullException(nameof(speechmaticsBatchService));
            _speechmaticsStreamingService = speechmaticsStreamingService ?? throw new ArgumentNullException(nameof(speechmaticsStreamingService));
            _sonioxBatchService = sonioxBatchService ?? throw new ArgumentNullException(nameof(sonioxBatchService));
            _sonioxStreamingService = sonioxStreamingService ?? throw new ArgumentNullException(nameof(sonioxStreamingService));
            _openaiBatchService = openaiBatchService ?? throw new ArgumentNullException(nameof(openaiBatchService));
            _openaiStreamingService = openaiStreamingService ?? throw new ArgumentNullException(nameof(openaiStreamingService));
            _inputInjector = inputInjector ?? throw new ArgumentNullException(nameof(inputInjector));
            _soundService = soundService ?? throw new ArgumentNullException(nameof(soundService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

            _audioService.AudioLevelUpdated += (s, level) =>
            {
                // Dispatch to UI thread
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    AudioLevel = level;
                });
            };

            // Wire audio data to hybrid buffer / streaming
            _audioService.AudioDataAvailable += OnAudioDataAvailable;

            // Wire streaming transcript results for both
            _deepgramStreamingService.TranscriptReceived += OnTranscriptReceived;
            _deepgramStreamingService.UtteranceEndReceived += OnUtteranceEndReceived;
            _deepgramStreamingService.Error += OnStreamingError;

            _speechmaticsStreamingService.TranscriptReceived += OnTranscriptReceived;
            _speechmaticsStreamingService.UtteranceEndReceived += OnUtteranceEndReceived;
            _speechmaticsStreamingService.Error += OnStreamingError;

            _sonioxStreamingService.TranscriptReceived += OnTranscriptReceived;
            _sonioxStreamingService.UtteranceEndReceived += OnUtteranceEndReceived;
            _sonioxStreamingService.Error += OnStreamingError;

            _openaiStreamingService.TranscriptReceived += OnTranscriptReceived;
            _openaiStreamingService.UtteranceEndReceived += OnUtteranceEndReceived;
            _openaiStreamingService.Error += OnStreamingError;

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

            // Allow Start command to execute even while streaming/recording (for cancel)
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
        }

        private void OnUtteranceEndReceived(object? sender, EventArgs e)
        {
            // Commit is now handled inside OnTranscriptReceived when IsFinal=true
            System.Diagnostics.Debug.WriteLine("[MainViewModel] UtteranceEnd received.");
        }

        public ObservableCollection<string> AvailableApis { get; } = new ObservableCollection<string> { "Deepgram", "Speechmatics", "Soniox", "OpenAI" };

        public string SelectedApi
        {
            get => _settingsService.CurrentSettings.SelectedApi;
            set
            {
                if (_settingsService.CurrentSettings.SelectedApi != value)
                {
                    _settingsService.CurrentSettings.SelectedApi = value;
                    _settingsService.SaveSettings(_settingsService.CurrentSettings);
                    OnPropertyChanged();
                    CheckApiConfiguration();
                }
            }
        }

        private IBatchSttService ActiveBatchService => SelectedApi switch
        {
            "Soniox" => _sonioxBatchService,
            "Speechmatics" => _speechmaticsBatchService,
            "OpenAI" => _openaiBatchService,
            _ => _deepgramBatchService,
        };
        private IStreamingSttService ActiveStreamingService => SelectedApi switch
        {
            "Soniox" => _sonioxStreamingService,
            "Speechmatics" => _speechmaticsStreamingService,
            "OpenAI" => _openaiStreamingService,
            _ => _deepgramStreamingService,
        };

        public string TranscriptText
        {
            get => _transcriptText;
            set => SetProperty(ref _transcriptText, value);
        }

        public bool IsTestMode => _settingsService.CurrentSettings.TestMode;

        public string DeepgramTranscript
        {
            get => _deepgramTranscript;
            set => SetProperty(ref _deepgramTranscript, value);
        }

        public string SpeechmaticsTranscript
        {
            get => _speechmaticsTranscript;
            set => SetProperty(ref _speechmaticsTranscript, value);
        }

        public string SonioxTranscript
        {
            get => _sonioxTranscript;
            set => SetProperty(ref _sonioxTranscript, value);
        }

        public string OpenAITranscript
        {
            get => _openaiTranscript;
            set
            {
                _openaiTranscript = value;
                OnPropertyChanged(nameof(OpenAITranscript));
            }
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

        private bool CheckApiConfiguration()
        {
            var settings = _settingsService.CurrentSettings;
            if (SelectedApi == "Deepgram" && string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                StatusText = "Deepgram API Key not configured. Please open Settings.";
                return false;
            }
            else if (SelectedApi == "Speechmatics" && string.IsNullOrWhiteSpace(settings.SpeechmaticsApiKey))
            {
                StatusText = "Speechmatics API Key not configured. Please open Settings.";
                return false;
            }
            else if (SelectedApi == "Soniox" && string.IsNullOrWhiteSpace(settings.SonioxApiKey))
            {
                StatusText = "Soniox API Key not configured. Please open Settings.";
                return false;
            }
            else if (SelectedApi == "OpenAI" && string.IsNullOrWhiteSpace(settings.OpenAIApiKey))
            {
                StatusText = "OpenAI API Key not configured. Please open Settings.";
                return false;
            }
            return true;
        }

        private void OpenSettings()
        {
            var settingsWindow = new SettingsWindow(_settingsService);
            if (settingsWindow.ShowDialog() == true)
            {
                // Reload settings
                var settings = _settingsService.CurrentSettings;
                OnPropertyChanged(nameof(SelectedApi)); // Update UI if it was changed in SettingsWindow
                OnPropertyChanged(nameof(IsTestMode)); // Update UI if Test Mode changed
                _deepgramBatchService.UpdateSettings(settings.ApiKey, settings.Model);
                _deepgramStreamingService.UpdateSettings(settings.ApiKey, settings.Model);
                _speechmaticsBatchService.UpdateSettings(settings.SpeechmaticsApiKey, settings.SpeechmaticsModel);
                _speechmaticsStreamingService.UpdateSettings(settings.SpeechmaticsApiKey, settings.SpeechmaticsModel);
                _sonioxBatchService.UpdateSettings(settings.SonioxApiKey, settings.SonioxModel);
                _sonioxStreamingService.UpdateSettings(settings.SonioxApiKey, settings.SonioxModel);
                _openaiBatchService.UpdateSettings(settings.OpenAIApiKey, settings.OpenAIModel);
                _openaiStreamingService.UpdateSettings(settings.OpenAIApiKey, settings.OpenAIModel);
                
                StatusText = "Settings updated.";
            }
        }

        public void OnF3KeyDown()
        {
            _f3DownTime = DateTime.Now;

            if (IsRecording)
            {
                // If recording is already active
                if (_isStreaming)
                {
                    // If running in Streaming Mode (Hold), ignore repeated KeyDown
                    return;
                }
                else
                {
                    // If running in Batch Mode (Toggle), check if this is the "Stop" press
                    // Note: In toggle mode, user presses F3 to start, then waits, then presses F3 again to stop.
                    // This second press triggers KeyDown.
                    if (_isToggleMode)
                    {
                        if (StopRecordingCommand.CanExecute(null))
                        {
                            StopRecordingCommand.Execute(null);
                        }
                        _isToggleMode = false;
                    }
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

        public void OnF3KeyUp()
        {
            if (IsRecording)
            {
                var duration = DateTime.Now - _f3DownTime;
                
                if (_isStreaming)
                {
                    // In streaming mode, releasing F3 stops recording immediately
                    if (StopRecordingCommand.CanExecute(null))
                    {
                        StopRecordingCommand.Execute(null);
                    }
                }
                else
                {
                    // Not in streaming mode yet
                    if (!_isHybridDecisionMade && duration.TotalMilliseconds < HybridThresholdMs)
                    {
                        // Short press (< 300ms) confirmed -> Batch Mode (Toggle)
                        _isToggleMode = true;
                        _isHybridDecisionMade = true;
                        _hybridTimer.Stop();
                        
                        // Clear buffer as we won't need it for streaming
                         while (_audioBuffer.TryDequeue(out _)) { }

                        // Play sound NOW (Batch started)
                        _soundService.PlayStartSound();

                        StatusText = "Recording (Batch)...";
                    }
                }
            }
        }

        private void HybridTimer_Tick(object? sender, EventArgs e)
        {
            _hybridTimer.Stop();

            // Timer fired means 300ms elapsed and key is still down (or logic hasn't solidified batch mode)
            // But we need to be careful: key might have been released just before timer tick
            // In F3KeyUp, we stop the timer. So if we are here, key is likely still down or released very close to threshold.
            // However, F3KeyUp logic handles the "Short press" case accurately.
            // If we are here, it means F3KeyUp hasn't fired yet OR fired > 300ms (impossible if timer stopped).
            // Actually, F3KeyUp stops the timer. So if this Tick fires, it implies User is HOLDING F3.
            
            // Switch to Streaming Mode
            StartStreamingMode();
        }

        private IntPtr _targetWindowHandle;

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
                        await Task.WhenAll(_deepgramStreamingService.CancelAsync(), _speechmaticsStreamingService.CancelAsync(), _sonioxStreamingService.CancelAsync(), _openaiStreamingService.CancelAsync());
                    }
                    else
                    {
                        await ActiveStreamingService.CancelAsync();
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
                    
                    // Reset buffer
                    while (_audioBuffer.TryDequeue(out _)) { }
                    
                    _isHybridDecisionMade = false;
                    _isStreaming = false;
                    _isToggleMode = false;
                    _isBatchProcessing = false;

                    // Start Audio (Dual Output: File + Event)
                    _audioService.StartRecording();
                    
                    bool isKeyTrigger = parameter is string s && s == "Key";

                    IsRecording = true;
                    _recordingDuration = TimeSpan.Zero;
                    
                    if (isKeyTrigger)
                    {
                        // Delayed decision logic
                        _isHybridDecisionMade = false;
                        StatusText = "..."; // Pending decision
                        _hybridTimer.Start(); // Start decision timer
                        
                        // DO NOT play sound yet
                    }
                    else
                    {
                        // Button click (or direct command) -> Assume Batch/Toggle immediately
                        _isHybridDecisionMade = true;
                        _isToggleMode = true;
                        StatusText = "Recording (Batch)...";
                        _soundService.PlayStartSound();
                    }

                    _recordingTimer.Start();
                    
                    if (IsTestMode)
                    {
                        DeepgramTranscript = string.Empty;
                        SpeechmaticsTranscript = string.Empty;
                        SonioxTranscript = string.Empty;
                        OpenAITranscript = string.Empty;
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
            
            // Play sound NOW (Streaming confirmed)
            _soundService.PlayStartSound();
            
            StatusText = "Streaming... 00:00";

            // Connect WebSocket
            try 
            {
                if (IsTestMode)
                {
                    var dTask = _deepgramStreamingService.StartAsync("vi");
                    var sTask = _speechmaticsStreamingService.StartAsync("vi");
                    var sonioxTask = _sonioxStreamingService.StartAsync("vi");
                    var openaiTask = _openaiStreamingService.StartAsync("vi");
                    await Task.WhenAll(dTask, sTask, sonioxTask, openaiTask);
                    
                    // Flush buffer to WebSocket
                    while (_audioBuffer.TryDequeue(out var args))
                    {
                       var sdTask = _deepgramStreamingService.SendAudioAsync(args.Buffer, args.BytesRecorded);
                       var ssTask = _speechmaticsStreamingService.SendAudioAsync(args.Buffer, args.BytesRecorded);
                       var ssonioxTask = _sonioxStreamingService.SendAudioAsync(args.Buffer, args.BytesRecorded);
                       var soaTask = _openaiStreamingService.SendAudioAsync(args.Buffer, args.BytesRecorded);
                       await Task.WhenAll(sdTask, ssTask, ssonioxTask, soaTask);
                    }
                }
                else
                {
                    await ActiveStreamingService.StartAsync("vi");
                    
                    // Flush buffer to WebSocket
                    while (_audioBuffer.TryDequeue(out var args))
                    {
                       await ActiveStreamingService.SendAudioAsync(args.Buffer, args.BytesRecorded);
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

                // Stop Mic
                // If Streaming -> Discard File
                // If Batch -> Keep File
                bool discardFile = _isStreaming;
                var filePath = await _audioService.StopRecordingAsync(discard: discardFile);

                _soundService.PlayStopSound();

                if (_isStreaming)
                {
                    // Finish Streaming
                    StatusText = "Finalizing Stream...";
                    if (!IsTestMode)
                    {
                        _inputInjector.CommitCurrentText(); // Lock in displayed text
                        await ActiveStreamingService.StopAsync();
                    }
                    else
                    {
                        var t1 = _deepgramStreamingService.StopAsync();
                        var t2 = _speechmaticsStreamingService.StopAsync();
                        var t3 = _sonioxStreamingService.StopAsync();
                        var t4 = _openaiStreamingService.StopAsync();
                        await Task.WhenAll(t1, t2, t3, t4);
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
                            
                            // Fire-and-forget batch processing
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

        private async Task ProcessBatchRecordingAsync(string filePath, IntPtr targetWindow)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var transcript = await ActiveBatchService.TranscribeAsync(filePath, "vi");
                sw.Stop();

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    var finalTranscript = transcript;
                    if (!string.IsNullOrWhiteSpace(transcript))
                    {
                        var trimmed = transcript.TrimEnd();
                        if (trimmed.EndsWith("."))
                        {
                            finalTranscript = trimmed + " ";
                        }
                        else
                        {
                            finalTranscript = trimmed + ". ";
                        }
                    }

                    TranscriptText = finalTranscript;
                    StatusText = $"Done. ({sw.ElapsedMilliseconds}ms)";

                    if (!string.IsNullOrWhiteSpace(finalTranscript))
                    {
                        // Use original clipboard injection for batch mode
                        await _inputInjector.InjectTextAsync(finalTranscript, targetWindow);
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
            // Create independent task for Deepgram
            var deepgramTask = Task.Run(async () => 
            {
                var sw = Stopwatch.StartNew();
                try 
                {
                    var result = await _deepgramBatchService.TranscribeAsync(filePath, "vi");
                    sw.Stop();
                    var formatted = FormatTranscript(result);
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        DeepgramTranscript = $"[{sw.ElapsedMilliseconds}ms]\n{formatted}";
                    });
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        DeepgramTranscript = $"[{sw.ElapsedMilliseconds}ms] Failed: {ex.Message}";
                    });
                }
            });

            // Create independent task for Speechmatics
            var speechmaticsTask = Task.Run(async () => 
            {
                var sw = Stopwatch.StartNew();
                try 
                {
                    var result = await _speechmaticsBatchService.TranscribeAsync(filePath, "vi");
                    sw.Stop();
                    var formatted = FormatTranscript(result);
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        SpeechmaticsTranscript = $"[{sw.ElapsedMilliseconds}ms]\n{formatted}";
                    });
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        SpeechmaticsTranscript = $"[{sw.ElapsedMilliseconds}ms] Failed: {ex.Message}";
                    });
                }
            });

            // Create independent task for Soniox
            var sonioxTask = Task.Run(async () => 
            {
                var sw = Stopwatch.StartNew();
                try 
                {
                    var result = await _sonioxBatchService.TranscribeAsync(filePath, "vi");
                    sw.Stop();
                    var formatted = FormatTranscript(result);
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        SonioxTranscript = $"[{sw.ElapsedMilliseconds}ms]\n{formatted}";
                    });
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        SonioxTranscript = $"[{sw.ElapsedMilliseconds}ms] Failed: {ex.Message}";
                    });
                }
            });

            // Create independent task for OpenAI
            var openaiTask = Task.Run(async () => 
            {
                var sw = Stopwatch.StartNew();
                try 
                {
                    var result = await _openaiBatchService.TranscribeAsync(filePath, "vi");
                    sw.Stop();
                    var formatted = FormatTranscript(result);
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        OpenAITranscript = $"[{sw.ElapsedMilliseconds}ms]\n{formatted}";
                    });
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        OpenAITranscript = $"[{sw.ElapsedMilliseconds}ms] Failed: {ex.Message}";
                    });
                }
            });

            var overallSw = Stopwatch.StartNew();
            try
            {
                // Wait for all to finish so we can clean up
                await Task.WhenAll(deepgramTask, speechmaticsTask, sonioxTask, openaiTask);
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

        private async void OnAudioDataAvailable(object? sender, AudioDataEventArgs e)
        {
            if (_isStreaming)
            {
                // Only send audio when voice activity is detected
                if (!_audioService.IsSpeaking) return;

                // Push directly to WebSocket
                try
                {
                    if (IsTestMode)
                    {
                        var sendD = _deepgramStreamingService.SendAudioAsync(e.Buffer, e.BytesRecorded);
                        var sendS = _speechmaticsStreamingService.SendAudioAsync(e.Buffer, e.BytesRecorded);
                        var sendSoniox = _sonioxStreamingService.SendAudioAsync(e.Buffer, e.BytesRecorded);
                        var sendOpenAI = _openaiStreamingService.SendAudioAsync(e.Buffer, e.BytesRecorded);
                        await Task.WhenAll(sendD, sendS, sendSoniox, sendOpenAI);
                    }
                    else
                    {
                        await ActiveStreamingService.SendAudioAsync(e.Buffer, e.BytesRecorded);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainVM] Audio send error: {ex.Message}");
                }
            }
            else if (!_isHybridDecisionMade)
            {
                // Buffer audio while waiting for decision
                _audioBuffer.Enqueue(e);
            }
            // If Batch Mode decision made (_isToggleMode), we ignore event data (file is being written)
        }

        private void OnTranscriptReceived(object? sender, TranscriptEventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    // Always update UI with latest text (partial or final)
                    if (!string.IsNullOrEmpty(e.Text))
                    {
                        if (IsTestMode)
                        {
                            if (sender == _deepgramStreamingService)
                            {
                                DeepgramTranscript = e.Text;
                            }
                            else if (sender == _speechmaticsStreamingService)
                            {
                                SpeechmaticsTranscript = e.Text;
                            }
                            else if (sender == _sonioxStreamingService)
                            {
                                SonioxTranscript = e.Text;
                            }
                            else if (sender == _openaiStreamingService)
                            {
                                OpenAITranscript = e.Text;
                            }
                        }
                        else
                        {
                            TranscriptText = e.Text;
                        }
                    }

                    // Only inject into target window when result is final
                    if (!IsTestMode && e.IsFinal && !string.IsNullOrEmpty(e.Text))
                    {
                        await _inputInjector.InjectStreamingTextAsync(
                            e.Text, true, _targetWindowHandle);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainVM] Streaming inject error: {ex.Message}");
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

        private void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private void ResetState()
        {
            IsRecording = false;
            _isStreaming = false;
            _isBatchProcessing = false;
            _isToggleMode = false;
            _isHybridDecisionMade = false;
            OnPropertyChanged(nameof(IsSending));
            AudioLevel = 0;
            DeepgramTranscript = string.Empty;
            SpeechmaticsTranscript = string.Empty;
            SonioxTranscript = string.Empty;
            OpenAITranscript = string.Empty;
            TranscriptText = string.Empty;
            while (_audioBuffer.TryDequeue(out _)) { }
        }
    }
}
