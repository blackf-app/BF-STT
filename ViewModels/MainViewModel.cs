using BF_STT.Models;
using BF_STT.Services;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.IO;

namespace BF_STT.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly AudioRecordingService _audioService;
        private readonly DeepgramService _deepgramService;
        private readonly DeepgramStreamingService _streamingService;
        private readonly InputInjector _inputInjector;
        private readonly SoundService _soundService;
        private readonly SettingsService _settingsService;
        
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

        // Hybrid mode state
        private DispatcherTimer _hybridTimer;
        private bool _isHybridDecisionMade;
        private ConcurrentQueue<AudioDataEventArgs> _audioBuffer = new();
        private const int HybridThresholdMs = 300;

        public MainViewModel(
            AudioRecordingService audioService, 
            DeepgramService deepgramService,
            DeepgramStreamingService streamingService, 
            InputInjector inputInjector, 
            SoundService soundService,
            SettingsService settingsService)
        {
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _deepgramService = deepgramService ?? throw new ArgumentNullException(nameof(deepgramService));
            _streamingService = streamingService ?? throw new ArgumentNullException(nameof(streamingService));
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

            // Wire streaming transcript results
            _streamingService.TranscriptReceived += OnTranscriptReceived;
            _streamingService.UtteranceEndReceived += (s, e) =>
            {
                _inputInjector.CommitCurrentText();
                System.Diagnostics.Debug.WriteLine("[MainViewModel] UtteranceEnd - Text Committed.");
            };
            _streamingService.Error += OnStreamingError;

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
            SendToDeepgramCommand = new RelayCommand(async _ => await Task.CompletedTask, _ => false); // Disabled
            CloseCommand = new RelayCommand(_ => {
                System.Windows.Application.Current.Shutdown();
            });
            OpenSettingsCommand = new RelayCommand(_ => OpenSettings());
        }

        public string TranscriptText
        {
            get => _transcriptText;
            set => SetProperty(ref _transcriptText, value);
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
        public ICommand SendToDeepgramCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand OpenSettingsCommand { get; }

        private void OpenSettings()
        {
            var settingsWindow = new SettingsWindow(_settingsService);
            if (settingsWindow.ShowDialog() == true)
            {
                // Reload settings
                var settings = _settingsService.CurrentSettings;
                _deepgramService.UpdateSettings(settings.ApiKey, settings.Model);
                _streamingService.UpdateSettings(settings.ApiKey, settings.Model);
                
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
                    await _streamingService.CancelAsync();
                    _soundService.PlayStopSound();
                    
                    ResetState();
                    StatusText = "Cancelled.";
                }
                else
                {
                    // Start new session
                    _targetWindowHandle = _inputInjector.LastExternalWindowHandle;
                    _inputInjector.ResetStreamingState();
                    
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
                    
                    TranscriptText = string.Empty;
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
                await _streamingService.StartAsync("vi");
                
                // Flush buffer to WebSocket
                while (_audioBuffer.TryDequeue(out var args))
                {
                   await _streamingService.SendAudioAsync(args.Buffer, args.BytesRecorded);
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
                    _inputInjector.CommitCurrentText(); // Lock in displayed text
                    await _streamingService.StopAsync();
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
                            OnPropertyChanged(nameof(IsSending));
                            
                            // Fire-and-forget batch processing
                            _ = ProcessBatchRecordingAsync(filePath, _targetWindowHandle);
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
            try
            {
                var transcript = await _deepgramService.TranscribeAsync(filePath, "vi");

                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    var finalTranscript = transcript;
                    if (!string.IsNullOrWhiteSpace(transcript))
                    {
                        finalTranscript = transcript.TrimEnd() + ". ";
                    }

                    TranscriptText = finalTranscript;
                    StatusText = "Done.";

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
                TryDeleteFile(filePath);
                _isBatchProcessing = false;
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(IsSending));
                });
            }
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
                    await _streamingService.SendAudioAsync(e.Buffer, e.BytesRecorded);
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
                    if (!string.IsNullOrEmpty(e.Text))
                    {
                        TranscriptText = e.Text;
                    }
                    // Always call inject (even for empty text) so committed state is updated correctly
                    await _inputInjector.InjectStreamingTextAsync(
                        e.Text ?? string.Empty, e.IsFinal, _targetWindowHandle);
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
            while (_audioBuffer.TryDequeue(out _)) { }
        }
    }
}
