using BF_STT.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace BF_STT.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly AudioRecordingService _audioService;
        private readonly DeepgramService _deepgramService;
        private readonly InputInjector _inputInjector;

        private string _transcriptText = string.Empty;
        private string _statusText = "Ready";
        private bool _isRecording;
        private bool _isSending;
        private string? _lastRecordedFilePath;

        public MainViewModel(AudioRecordingService audioService, DeepgramService deepgramService, InputInjector inputInjector)
        {
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _deepgramService = deepgramService ?? throw new ArgumentNullException(nameof(deepgramService));
            _inputInjector = inputInjector ?? throw new ArgumentNullException(nameof(inputInjector));

            // Allow Start command to execute even if recording (to serve as Cancel)
            StartRecordingCommand = new RelayCommand(StartRecording, _ => !IsSending);
            StopRecordingCommand = new RelayCommand(StopRecording, _ => IsRecording);
            SendToDeepgramCommand = new RelayCommand(async _ => await SendToDeepgramAsync(), _ => !IsRecording && !IsSending && !string.IsNullOrEmpty(_lastRecordedFilePath));
            CloseCommand = new RelayCommand(_ => System.Windows.Application.Current.Shutdown());

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

        private async void StartRecording(object? parameter)
        {
            try
            {
                if (IsRecording)
                {
                    // Cancel logic: Stop and discard
                    await _audioService.StopRecordingAsync(discard: true);
                    IsRecording = false;
                    StatusText = "Recording cancelled.";
                    _lastRecordedFilePath = null;
                }
                else
                {
                    // Start logic
                    _audioService.StartRecording();
                    IsRecording = true;
                    StatusText = "Recording... (Click Start to Cancel)";
                    TranscriptText = string.Empty; // Clear previous
                    _lastRecordedFilePath = null;
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
                _lastRecordedFilePath = await _audioService.StopRecordingAsync(discard: false);
                IsRecording = false;
                StatusText = "Recording stopped. Sending to Deepgram...";
                
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
                
                // Inject text into previous window
                _inputInjector.InjectText(transcript);
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
    }
}
