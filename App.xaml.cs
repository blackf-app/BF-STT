using BF_STT.Services;
using BF_STT.ViewModels;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Windows;

namespace BF_STT
{
    public partial class App : System.Windows.Application
    {
        // Public configuration not strictly needed anymore as we use SettingsService
        
        // Track all disposable services for proper cleanup
        private HotkeyService? _hotkeyService;
        private HttpClient? _httpClient;
        private AudioRecordingService? _audioService;
        private DeepgramStreamingService? _streamingService;
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
                System.Windows.MessageBox.Show("Ứng dụng hiện đang chạy.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            // Prevent implicit shutdown when SettingsWindow closes
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Initialize Settings Service
            var settingsService = new SettingsService();
            var settings = settingsService.CurrentSettings;

            // Check API Key on startup based on the selected API
            bool missingKey = false;
            if (settings.SelectedApi == "Speechmatics" && string.IsNullOrEmpty(settings.SpeechmaticsApiKey))
                missingKey = true;
            else if (settings.SelectedApi == "Deepgram" && string.IsNullOrEmpty(settings.ApiKey))
                missingKey = true;
            
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

            // Dependency Injection
            _httpClient = new HttpClient();
            var deepgramService = new DeepgramService(_httpClient, settings.ApiKey, settings.BaseUrl, settings.Model);
            var speechmaticsService = new SpeechmaticsBatchService(_httpClient, settings.SpeechmaticsApiKey, settings.SpeechmaticsBaseUrl);

            _audioService = new AudioRecordingService();
            _streamingService = new DeepgramStreamingService(settings.ApiKey, settings.StreamingUrl, settings.Model); // Kept for disposing
            var speechmaticsStreaming = new SpeechmaticsStreamingService(settings.SpeechmaticsApiKey, settings.SpeechmaticsStreamingUrl);
            
            _inputInjector = new InputInjector();
            var soundService = new SoundService();

            var mainViewModel = new MainViewModel(_audioService, deepgramService, _streamingService, speechmaticsService, speechmaticsStreaming, _inputInjector, soundService, settingsService);

            // Set up Global Hotkey
            _hotkeyService = new HotkeyService(
                onKeyDown: () => mainViewModel.OnF3KeyDown(),
                onKeyUp: () => mainViewModel.OnF3KeyUp()
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
            _streamingService?.Dispose();
            _audioService?.Dispose();
            _httpClient?.Dispose();
            _mutex?.Dispose();
            
            base.OnExit(e);
        }
    }
}
