using BF_STT.Services;
using BF_STT.ViewModels;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;

namespace BF_STT
{
    public partial class App : System.Windows.Application
    {
        public IConfiguration? Configuration { get; private set; }
        
        // Track all disposable services for proper cleanup
        private HotkeyService? _hotkeyService;
        private HttpClient? _httpClient;
        private AudioRecordingService? _audioService;
        private InputInjector? _inputInjector;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Load configuration
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory());

            // 1. Read from Embedded Resource (Internal default)
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "BF_STT.appsettings.json";
            var resourceStream = assembly.GetManifestResourceStream(resourceName);
            if (resourceStream != null)
            {
                // We must copy to a MemoryStream because AddJsonStream might read it later during Build(),
                // and the original stream would be disposed if we used a 'using' block here.
                var ms = new MemoryStream();
                resourceStream.CopyTo(ms);
                ms.Position = 0;
                builder.AddJsonStream(ms);
            }

            // 2. Read from External File (Optional override)
            builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            Configuration = builder.Build();

            var apiKey = Configuration["Deepgram:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                apiKey = Environment.GetEnvironmentVariable("DEEPGRAM_API_KEY");
            }

            var baseUrl = Configuration["Deepgram:BaseUrl"];
            var model = Configuration["Deepgram:Model"];

            // Dependency Injection (Manual for simplicity)
            // All disposable services are stored as fields for proper cleanup in OnExit
            _httpClient = new HttpClient();
            var deepgramService = new DeepgramService(_httpClient, apiKey ?? "", baseUrl ?? "", model ?? "");
            _audioService = new AudioRecordingService();
            _inputInjector = new InputInjector();
            var soundService = new SoundService();

            var mainViewModel = new MainViewModel(_audioService, deepgramService, _inputInjector, soundService);

            // Set up Global Hotkey
            _hotkeyService = new HotkeyService(() => 
            {
                if (mainViewModel.HotkeyCommand.CanExecute(null))
                {
                    mainViewModel.HotkeyCommand.Execute(null);
                }
            });

            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Dispose all services in reverse order of creation
            _hotkeyService?.Dispose();
            _inputInjector?.Dispose();
            _audioService?.Dispose();
            _httpClient?.Dispose();
            
            base.OnExit(e);
        }
    }
}
