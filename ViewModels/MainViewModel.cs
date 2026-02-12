using BF_STT.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using System.IO;

namespace BF_STT.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly AudioRecordingService _audioService;
        private readonly DeepgramService _deepgramService;
        private readonly InputInjector _inputInjector;
        private readonly SoundService _soundService;
        
        private DispatcherTimer _recordingTimer;
        private TimeSpan _recordingDuration;

        private string _transcriptText = string.Empty;
        private string _statusText = "Ready";
        private bool _isRecording;
        private bool _isSending;
        private string? _lastRecordedFilePath;
        private float _audioLevel;

        public MainViewModel(AudioRecordingService audioService, DeepgramService deepgramService, InputInjector inputInjector, SoundService soundService)
        {
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _deepgramService = deepgramService ?? throw new ArgumentNullException(nameof(deepgramService));
            _inputInjector = inputInjector ?? throw new ArgumentNullException(nameof(inputInjector));
            _soundService = soundService ?? throw new ArgumentNullException(nameof(soundService));

            _audioService.AudioLevelUpdated += (s, level) =>
            {
                // Dispatch to UI thread
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    AudioLevel = level;
                });
            };

            _recordingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _recordingTimer.Tick += RecordingTimer_Tick;

            // Allow Start command to execute even if recording (to serve as Cancel)
            StartRecordingCommand = new RelayCommand(StartRecording, _ => !IsSending);
            StopRecordingCommand = new RelayCommand(StopRecording, _ => IsRecording);
            SendToDeepgramCommand = new RelayCommand(async _ => await SendToDeepgramAsync(), _ => !IsRecording && !IsSending && !string.IsNullOrEmpty(_lastRecordedFilePath));
            CloseCommand = new RelayCommand(_ => {
                CleanupLastRecording();
                System.Windows.Application.Current.Shutdown();
            });

            HotkeyCommand = new RelayCommand(HotkeyAction);
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
            get => _isSending;
            set
            {
                if (SetProperty(ref _isSending, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
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
        public ICommand HotkeyCommand { get; }

        private void HotkeyAction(object? parameter)
        {
            if (IsRecording)
            {
                // Second press: Stop and Send
                if (StopRecordingCommand.CanExecute(null))
                {
                    StopRecordingCommand.Execute(null);
                }
            }
            else
            {
                // First press: Start Recording
                if (StartRecordingCommand.CanExecute(null))
                {
                    StartRecordingCommand.Execute(null);
                }
            }
        }

        private void RecordingTimer_Tick(object? sender, EventArgs e)
        {
            _recordingDuration = _recordingDuration.Add(TimeSpan.FromSeconds(1));
            StatusText = $"Recording... {_recordingDuration:mm\\:ss} (Click Start to Cancel)";
        }

        private async void StartRecording(object? parameter)
        {
            try
            {
                if (IsRecording)
                {
                    // Cancel logic: Stop and discard
                    _recordingTimer.Stop();
                    await _audioService.StopRecordingAsync(discard: true);
                    _soundService.PlayStopSound();
                    
                    IsRecording = false;
                    StatusText = "Recording cancelled.";
                    _lastRecordedFilePath = null;
                    AudioLevel = 0;
                }
                else
                {
                    // Clean up previous file before starting new
                    CleanupLastRecording();

                    // Start logic
                    _audioService.StartRecording();
                    _soundService.PlayStartSound();
                    
                    IsRecording = true;
                    _recordingDuration = TimeSpan.Zero;
                    StatusText = "Recording... 00:00 (Click Start to Cancel)";
                    _recordingTimer.Start();
                    
                    TranscriptText = string.Empty; // Clear previous
                    _lastRecordedFilePath = null;
                    AudioLevel = 0;
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
                IsRecording = false;
            }
        }

        private async void StopRecording(object? parameter)
        {
            try
            {
                _recordingTimer.Stop();
                _lastRecordedFilePath = await _audioService.StopRecordingAsync(discard: false);
                _soundService.PlayStopSound();
                
                IsRecording = false;
                StatusText = "Recording stopped. Sending to Deepgram...";
                AudioLevel = 0;
                
                // Auto-send
                if (!string.IsNullOrEmpty(_lastRecordedFilePath))
                {
                    if (SendToDeepgramCommand.CanExecute(null))
                    {
                        SendToDeepgramCommand.Execute(null);
                    }
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error stopping recording: {ex.Message}";
                IsRecording = false;
            }
        }

        private async Task SendToDeepgramAsync()
        {
            if (string.IsNullOrEmpty(_lastRecordedFilePath)) return;

            try
            {
                IsSending = true;
                StatusText = "Sending to Deepgram...";

                // Hardcoded language 'vi' as requested implicitly by removing selection (or keep default 'vi')
                var transcript = await _deepgramService.TranscribeAsync(_lastRecordedFilePath, "vi");
                
                TranscriptText = transcript;
                StatusText = "Done.";
                
                // Inject text into previous window (with clipboard backup/restore)
                await _inputInjector.InjectTextAsync(transcript);
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
                TranscriptText = "Failed to get transcript.";
            }
            finally
            {
                IsSending = false;
            }
        }

        private void CleanupLastRecording()
        {
            if (!string.IsNullOrEmpty(_lastRecordedFilePath))
            {
                try
                {
                    if (File.Exists(_lastRecordedFilePath))
                    {
                        File.Delete(_lastRecordedFilePath);
                    }
                }
                catch { /* Ignore cleanup errors */ }
                // Do not nullify here if we use this method only for cleanup OLD files.
                // However, if we clean up, we should probably set it to null or keep it as "cleaned".
                // But for "StartRecording", we clean up the OLD one, and immediately set _lastRecordedFilePath = null afterwards in StartRecording logic anyway (line 142 in original).
                // Wait, StartRecording sets _lastRecordedFilePath = null; (line 142).
                // So calling this before StartRecording works perfectly.
            }
        }
    }
}
