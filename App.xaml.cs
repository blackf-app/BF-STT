using BF_STT.Services;
using BF_STT.ViewModels;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Windows;

namespace BF_STT
{
    public partial class App : System.Windows.Application
    {
        // Track all disposable services for proper cleanup
        private HotkeyService? _hotkeyService;
        private HttpClient? _httpClient;
        private AudioRecordingService? _audioService;
        private SttProviderRegistry? _registry;
        private InputInjector? _inputInjector;
        private static Mutex? _mutex;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Ensure single instance
            const string appName = "BF-STT-Unique-Mutex-Name";
            _mutex = new Mutex(true, appName, out bool createdNew);

            if (!createdNew)
            {
                // App is already running
                System.Windows.MessageBox.Show("Ứng dụng hiện đang chạy.", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // Prevent implicit shutdown when SettingsWindow closes
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Initialize Settings Service
            var settingsService = new SettingsService();
            var settings = settingsService.CurrentSettings;

            // Build services
            _httpClient = new HttpClient();
            _audioService = new AudioRecordingService();
            _inputInjector = new InputInjector();
            var soundService = new SoundService();
            var historyService = new HistoryService(settings.MaxHistoryItems);

            // Build provider registry
            _registry = new SttProviderRegistry();

            var deepgramBatch = new DeepgramService(_httpClient, settings.ApiKey, settings.BaseUrl, settings.Model);
            var deepgramStreaming = new DeepgramStreamingService(settings.ApiKey, settings.StreamingUrl, settings.Model);
            _registry.Register("Deepgram", deepgramBatch, deepgramStreaming,
                s => s.ApiKey, s => s.Model);

            var speechmaticsBatch = new SpeechmaticsBatchService(_httpClient, settings.SpeechmaticsApiKey, settings.SpeechmaticsBaseUrl);
            var speechmaticsStreaming = new SpeechmaticsStreamingService(settings.SpeechmaticsApiKey, settings.SpeechmaticsStreamingUrl);
            _registry.Register("Speechmatics", speechmaticsBatch, speechmaticsStreaming,
                s => s.SpeechmaticsApiKey, s => s.SpeechmaticsModel);

            var sonioxBatch = new SonioxBatchService(_httpClient, settings.SonioxApiKey, settings.SonioxBaseUrl);
            var sonioxStreaming = new SonioxStreamingService(settings.SonioxApiKey, settings.SonioxStreamingUrl);
            _registry.Register("Soniox", sonioxBatch, sonioxStreaming,
                s => s.SonioxApiKey, s => s.SonioxModel);

            var openaiBatch = new OpenAIBatchService(_httpClient, settings.OpenAIApiKey, settings.OpenAIBaseUrl);
            var openaiStreaming = new OpenAIStreamingService(settings.OpenAIApiKey);
            _registry.Register("OpenAI", openaiBatch, openaiStreaming,
                s => s.OpenAIApiKey, s => s.OpenAIModel);

            // Check API Key on startup using registry
            bool missingKey = _registry.ValidateApiKey(settings.BatchModeApi, settings) != null
                           || _registry.ValidateApiKey(settings.StreamingModeApi, settings) != null;

            if (missingKey)
            {
                var settingsWindow = new SettingsWindow(settingsService);
                if (settingsWindow.ShowDialog() != true)
                {
                    Shutdown(); // Exit if user cancels without saving
                    return;
                }
                // Reload settings after save
                settings = settingsService.CurrentSettings;
            }

            var mainViewModel = new MainViewModel(
                _audioService,
                _registry,
                _inputInjector,
                soundService,
                settingsService,
                historyService
            );

            // Set up Global Hotkey
            _hotkeyService = new HotkeyService(
                settingsService,
                onKeyDown: () => mainViewModel.OnF3KeyDown(),
                onKeyUp: () => mainViewModel.OnF3KeyUp(),
                onStopAndSendKeyDown: () => mainViewModel.OnStopAndSendKeyDown(),
                onStopAndSendKeyUp: () => mainViewModel.OnStopAndSendKeyUp()
            );

            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            mainWindow.Show();
            
            // Restore normal shutdown behavior
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Dispose all services in reverse order of creation
            _hotkeyService?.Dispose();
            _inputInjector?.Dispose();

            // Dispose all streaming services registered in the registry
            if (_registry != null)
            {
                foreach (var provider in _registry.GetAllProviders())
                {
                    if (provider.StreamingService is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }

            _audioService?.Dispose();
            _httpClient?.Dispose();
            _mutex?.Dispose();
            
            base.OnExit(e);
        }
    }
}
