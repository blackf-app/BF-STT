using BF_STT.Models;
using BF_STT.Services.Workflow;
using BF_STT.Services.Infrastructure;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace BF_STT.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        #region Fields

        private readonly RecordingCoordinator _coordinator;
        private readonly SettingsService _settingsService;
        private readonly HistoryService _historyService;
        private bool _isHistoryVisible;
        private bool _isHistoryAtTop;

        private string _transcriptText = string.Empty;
        private string _statusText = "Ready";
        private bool _isRecording;
        private float _audioLevel;

        #endregion

        #region Constructor

        public MainViewModel(
            RecordingCoordinator coordinator,
            SettingsService settingsService,
            HistoryService historyService)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));

            // Subscribe to coordinator events
            _coordinator.StatusChanged += status => StatusText = status;
            _coordinator.TranscriptChanged += text => TranscriptText = text;
            _coordinator.ProviderTranscriptChanged += (provider, text) =>
                OnPropertyChanged($"{provider}Transcript");
            _coordinator.RecordingStateChanged += recording => IsRecording = recording;
            _coordinator.SendingStateChanged += () => OnPropertyChanged(nameof(IsSending));
            _coordinator.AudioLevelChanged += level => AudioLevel = level;
            _coordinator.CommandsInvalidated += () => CommandManager.InvalidateRequerySuggested();

            // Commands
            StartRecordingCommand = new RelayCommand(
                _ => _coordinator.StartRecording(), _ => true);
            StopRecordingCommand = new RelayCommand(
                _ => _coordinator.StopRecording(), _ => IsRecording);
            ResendAudioCommand = new RelayCommand(
                _ => _coordinator.ResendAudio(), _ => _coordinator.CanResend);
            CloseCommand = new RelayCommand(_ =>
            {
                _coordinator.CleanupLastFile();
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
            SendHistoryItemCommand = new RelayCommand(async item =>
            {
                if (item is HistoryItem historyItem)
                {
                    await _coordinator.SendHistoryItemAsync(historyItem);
                }
            });
        }

        #endregion

        #region Properties

        public ObservableCollection<string> AvailableApis { get; } = new ObservableCollection<string> { "Deepgram", "Speechmatics", "Soniox", "OpenAI", "ElevenLabs" };

        public string BatchModeApi
        {
            get => _coordinator.BatchModeApi;
            set
            {
                if (_coordinator.BatchModeApi != value)
                {
                    _coordinator.BatchModeApi = value;
                    OnPropertyChanged();
                    _coordinator.CheckApiConfiguration();
                }
            }
        }

        public string StreamingModeApi
        {
            get => _coordinator.StreamingModeApi;
            set
            {
                if (_coordinator.StreamingModeApi != value)
                {
                    _coordinator.StreamingModeApi = value;
                    OnPropertyChanged();
                    _coordinator.CheckApiConfiguration();
                }
            }
        }

        public string TranscriptText
        {
            get => _transcriptText;
            set => SetProperty(ref _transcriptText, value);
        }

        public bool IsTestMode => _coordinator.IsTestMode;

        // Per-provider transcripts for Test Mode UI panels
        public string DeepgramTranscript
        {
            get => _coordinator.GetProviderTranscript("Deepgram");
            set => OnPropertyChanged();
        }

        public string SpeechmaticsTranscript
        {
            get => _coordinator.GetProviderTranscript("Speechmatics");
            set => OnPropertyChanged();
        }

        public string SonioxTranscript
        {
            get => _coordinator.GetProviderTranscript("Soniox");
            set => OnPropertyChanged();
        }

        public string OpenAITranscript
        {
            get => _coordinator.GetProviderTranscript("OpenAI");
            set => OnPropertyChanged();
        }

        public string ElevenLabsTranscript
        {
            get => _coordinator.GetProviderTranscript("ElevenLabs");
            set => OnPropertyChanged();
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

        public bool IsSending => _coordinator.IsSending;

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
        public ICommand SendHistoryItemCommand { get; }

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

        #region Hotkey Handlers

        public void OnF3KeyDown() => _coordinator.HandleHotkeyDown(false);
        public void OnStopAndSendKeyDown() => _coordinator.HandleHotkeyDown(true);
        public void OnF3KeyUp() => _coordinator.HandleHotkeyUp();
        public void OnStopAndSendKeyUp() => _coordinator.HandleHotkeyUp();

        #endregion

        #region Settings

        private void OpenSettings()
        {
            System.Windows.Application.Current.MainWindow.Hide();
            var settingsWindow = new SettingsWindow(_settingsService);
            if (settingsWindow.ShowDialog() == true)
            {
                OnPropertyChanged(nameof(BatchModeApi));
                OnPropertyChanged(nameof(StreamingModeApi));
                OnPropertyChanged(nameof(IsTestMode));

                _coordinator.UpdateSettingsFromRegistry();

                StatusText = "Settings updated.";
            }
            System.Windows.Application.Current.MainWindow.Show();
        }

        #endregion
    }
}
