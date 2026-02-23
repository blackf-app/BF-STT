using BF_STT.Services.Audio;
using BF_STT.Services.STT;
using BF_STT.Services.Workflow;
using BF_STT.Services.Platform;
using BF_STT.Services.Infrastructure;
using BF_STT.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;

namespace BF_STT
{
    public partial class App : System.Windows.Application
    {
        private IServiceProvider? _serviceProvider;
        private HotkeyService? _hotkeyService;
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

            // Clean up old temp WAV files from previous sessions
            TempFileCleanupService.CleanupOldTempFiles();

            // Prevent implicit shutdown when SettingsWindow closes
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Build all services via DI container
            _serviceProvider = ServiceRegistration.Configure();

            var settingsService = _serviceProvider.GetRequiredService<SettingsService>();
            var settings = settingsService.CurrentSettings;
            var registry = _serviceProvider.GetRequiredService<SttProviderRegistry>();

            // Check API Key on startup using registry
            bool missingKey = registry.ValidateApiKey(settings.BatchModeApi, settings) != null
                           || registry.ValidateApiKey(settings.StreamingModeApi, settings) != null;

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

            var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();

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

            // Check for updates in background
            var httpClient = _serviceProvider.GetRequiredService<HttpClient>();
            if (settings.AutoCheckUpdate)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var updateService = new UpdateService(httpClient);
                        var release = await updateService.CheckForUpdateAsync();
                        if (release != null)
                        {
                            await Dispatcher.InvokeAsync(() =>
                            {
                                var result = System.Windows.MessageBox.Show(
                                    $"Đã có phiên bản mới ({release.Version}). Bạn có muốn cập nhật ngay không?\n\n" +
                                    "Ứng dụng sẽ tự động tải về, cài đặt và khởi động lại.",
                                    "Cập Nhật Có Sẵn",
                                    System.Windows.MessageBoxButton.YesNo,
                                    System.Windows.MessageBoxImage.Information);

                                if (result == System.Windows.MessageBoxResult.Yes)
                                {
                                    // Start update process
                                    _ = updateService.DownloadAndInstallUpdateAsync(release.DownloadUrl);
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Update check background error: {ex.Message}");
                    }
                });
            }
            
            // Restore normal shutdown behavior
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Dispose hotkey service
            _hotkeyService?.Dispose();

            // Dispose all services via the DI container
            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _mutex?.Dispose();
            
            base.OnExit(e);
        }
    }
}
