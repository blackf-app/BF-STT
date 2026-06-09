using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using BF_STT.Services.Infrastructure;
using BF_STT.Services.Platform;
using BF_STT.Services.STT;
using BF_STT.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Net.Http;

namespace BF_STT
{
    public partial class App : Application
    {
        private IServiceProvider? _serviceProvider;
        private HotkeyService? _hotkeyService;
        public static IServiceProvider? Services { get; private set; }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                TempFileCleanupService.CleanupOldTempFiles();
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                _serviceProvider = ServiceRegistration.Configure();
                Services = _serviceProvider;

                var settingsService = _serviceProvider.GetRequiredService<SettingsService>();
                var settings = settingsService.CurrentSettings;
                var registry = _serviceProvider.GetRequiredService<SttProviderRegistry>();

                var allProviders = registry.GetAllProviders();

                if (registry.ValidateApiKey(settings.BatchModeApi, settings) != null)
                {
                    var firstValid = allProviders.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.GetApiKey(settings)));
                    if (firstValid != null) settings.BatchModeApi = firstValid.Name;
                }

                if (registry.ValidateApiKey(settings.StreamingModeApi, settings) != null
                    || allProviders.FirstOrDefault(p => p.Name.Equals(settings.StreamingModeApi, StringComparison.OrdinalIgnoreCase))?.SupportsStreaming != true)
                {
                    var firstValidStreaming = allProviders.FirstOrDefault(p => p.SupportsStreaming && !string.IsNullOrWhiteSpace(p.GetApiKey(settings)));
                    if (firstValidStreaming != null) settings.StreamingModeApi = firstValidStreaming.Name;
                }

                settingsService.SaveSettings(settings);

                var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
                var mainWindow = new MainWindow { DataContext = mainViewModel };
                desktop.MainWindow = mainWindow;

                bool hasAnyKey = allProviders.Any(p => !string.IsNullOrWhiteSpace(p.GetApiKey(settings)));
                if (!hasAnyKey)
                {
                    var updateService = _serviceProvider.GetRequiredService<UpdateService>();
                    var settingsWindow = new SettingsWindow(settingsService, updateService);
                    // Show settings window modally before the main window.
                    settingsWindow.Show();
                }

                _hotkeyService = new HotkeyService(
                    settingsService,
                    onKeyDown: () => mainViewModel.OnF3KeyDown(),
                    onKeyUp: () => mainViewModel.OnF3KeyUp(),
                    onStopAndSendKeyDown: () => mainViewModel.OnStopAndSendKeyDown(),
                    onStopAndSendKeyUp: () => mainViewModel.OnStopAndSendKeyUp()
                );

                mainWindow.Show();

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
                                Log.Information("A new version is available: {Version}", release.Version);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Update check background error");
                        }
                    });
                }

                desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
                desktop.Exit += (_, _) =>
                {
                    _hotkeyService?.Dispose();
                    if (_serviceProvider is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                    Log.CloseAndFlush();
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
