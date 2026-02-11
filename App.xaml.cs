using BF_STT.Services;
using BF_STT.ViewModels;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Net.Http;
using System.Windows;

namespace BF_STT
{
    public partial class App : System.Windows.Application
    {
        public IConfiguration Configuration { get; private set; }
        private HotkeyService? _hotkeyService;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Load configuration
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            Configuration = builder.Build();

            var apiKey = Configuration["Deepgram:ApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                apiKey = Environment.GetEnvironmentVariable("DEEPGRAM_API_KEY");
            }

            var baseUrl = Configuration["Deepgram:BaseUrl"];
            var model = Configuration["Deepgram:Model"];

            // Dependency Injection (Manual for simplicity)
            var httpClient = new HttpClient(); // In a real app, use IHttpClientFactory
            var deepgramService = new DeepgramService(httpClient, apiKey ?? "", baseUrl ?? "", model ?? "");
            var audioService = new AudioRecordingService();
            var inputInjector = new InputInjector();

            var mainViewModel = new MainViewModel(audioService, deepgramService, inputInjector);

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
            _hotkeyService?.Dispose();
            base.OnExit(e);
        }
    }
}
