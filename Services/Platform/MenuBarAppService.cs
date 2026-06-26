using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using BF_STT.Services.Infrastructure;
using BF_STT.ViewModels;
using Serilog;
using System.ComponentModel;

namespace BF_STT.Services.Platform
{
    /// <summary>
    /// Owns the macOS menu bar shell while keeping the STT workflow in the existing
    /// Avalonia windows and view models.
    /// </summary>
    public sealed class MenuBarAppService : IDisposable
    {
        private const string AppName = "BF-STT";
        private const string IconFileName = "MenuBarIconTemplate.png";
        private const string IconResourceUri = "avares://BF-STT/Assets/MenuBarIconTemplate.png";

        private readonly IClassicDesktopStyleApplicationLifetime _desktop;
        private readonly MainViewModel _mainViewModel;
        private readonly SettingsService _settingsService;
        private readonly UpdateService _updateService;

        private MainWindow? _mainWindow;
        private SettingsWindow? _settingsWindow;
        private TrayIcon? _trayIcon;
        private TrayIcons? _trayIcons;
        private NativeMenuItem? _showHideItem;
        private NativeMenuItem? _recordItem;
        private NativeMenuItem? _resendItem;
        private NativeMenuItem? _statusItem;
        private bool _isQuitting;
        private bool _disposed;

        public MenuBarAppService(
            IClassicDesktopStyleApplicationLifetime desktop,
            MainViewModel mainViewModel,
            SettingsService settingsService,
            UpdateService updateService)
        {
            _desktop = desktop;
            _mainViewModel = mainViewModel;
            _settingsService = settingsService;
            _updateService = updateService;
        }

        public void Initialize(bool showMainWindowOnLaunch, bool showSettingsOnLaunch)
        {
            _desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _mainWindow = new MainWindow { DataContext = _mainViewModel };
            _mainWindow.Closing += MainWindowClosing;

            CreateTrayIcon();

            _mainViewModel.PropertyChanged += MainViewModelPropertyChanged;

            if (showMainWindowOnLaunch)
            {
                _desktop.MainWindow = _mainWindow;
                ShowMainWindow();
            }
            else
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_desktop.MainWindow == null)
                    {
                        _desktop.MainWindow = _mainWindow;
                    }
                });
            }

            if (showSettingsOnLaunch)
            {
                _ = ShowSettingsAsync();
            }

            UpdateMenuState();
        }

        public void ShowMainWindow()
        {
            if (_mainWindow == null)
            {
                return;
            }

            if (!_mainWindow.IsVisible)
            {
                _mainWindow.Show();
            }

            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }

            _mainWindow.Activate();
            UpdateMenuState();
        }

        public void HideMainWindow()
        {
            _mainWindow?.Hide();
            UpdateMenuState();
        }

        public async Task ShowSettingsAsync()
        {
            if (_settingsWindow != null)
            {
                _settingsWindow.Activate();
                return;
            }

            var settingsWindow = new SettingsWindow(_settingsService, _updateService);
            _settingsWindow = settingsWindow;

            try
            {
                if (_mainWindow?.IsVisible == true)
                {
                    await settingsWindow.ShowDialog(_mainWindow);
                }
                else
                {
                    var closed = new TaskCompletionSource<object?>();
                    settingsWindow.Closed += (_, _) => closed.TrySetResult(null);
                    settingsWindow.Show();
                    settingsWindow.Activate();
                    await closed.Task;
                }

                if (settingsWindow.Result == true)
                {
                    ApplySettingsChanged();
                }
            }
            finally
            {
                _settingsWindow = null;
                UpdateMenuState();
            }
        }

        public void Quit()
        {
            if (_isQuitting)
            {
                return;
            }

            _isQuitting = true;
            _trayIcon?.Dispose();
            _desktop.Shutdown();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _mainViewModel.PropertyChanged -= MainViewModelPropertyChanged;

            if (_mainWindow != null)
            {
                _mainWindow.Closing -= MainWindowClosing;
            }

            _trayIcon?.Dispose();
        }

        private void CreateTrayIcon()
        {
            var menu = new NativeMenu();

            _statusItem = DisabledItem(_mainViewModel.StatusText);
            menu.Items.Add(_statusItem);
            menu.Items.Add(new NativeMenuItemSeparator());

            _showHideItem = new NativeMenuItem { Header = "Show BF-STT" };
            _showHideItem.Click += (_, _) => ToggleMainWindow();
            menu.Items.Add(_showHideItem);

            var settingsItem = new NativeMenuItem { Header = "Settings..." };
            settingsItem.Click += (_, _) => _ = ShowSettingsAsync();
            menu.Items.Add(settingsItem);

            menu.Items.Add(new NativeMenuItemSeparator());

            _recordItem = new NativeMenuItem { Header = "Start Recording" };
            _recordItem.Click += (_, _) => ToggleRecording();
            menu.Items.Add(_recordItem);

            _resendItem = new NativeMenuItem { Header = "Resend Last Audio" };
            _resendItem.Click += (_, _) => ExecuteIfAvailable(_mainViewModel.ResendAudioCommand);
            menu.Items.Add(_resendItem);

            menu.Items.Add(new NativeMenuItemSeparator());

            var quitItem = new NativeMenuItem { Header = "Quit BF-STT" };
            quitItem.Click += (_, _) => Quit();
            menu.Items.Add(quitItem);

            _trayIcon = new TrayIcon
            {
                Icon = LoadTrayIcon(),
                IsVisible = true,
                ToolTipText = AppName,
                Menu = menu
            };
            MacOSProperties.SetIsTemplateIcon(_trayIcon, true);

            _trayIcons = new TrayIcons { _trayIcon };

            if (Application.Current != null)
            {
                TrayIcon.SetIcons(Application.Current, _trayIcons);
            }
        }

        private static WindowIcon? LoadTrayIcon()
        {
            try
            {
                using var stream = AssetLoader.Open(new Uri(IconResourceUri));
                return new WindowIcon(stream);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to load menu bar icon from Avalonia resources");
            }

            foreach (var path in GetIconFallbackPaths())
            {
                try
                {
                    if (!File.Exists(path))
                    {
                        continue;
                    }

                    using var stream = File.OpenRead(path);
                    return new WindowIcon(stream);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to load menu bar icon from {Path}", path);
                }
            }

            return null;
        }

        private static IEnumerable<string> GetIconFallbackPaths()
        {
            yield return Path.Combine(AppContext.BaseDirectory, IconFileName);
            yield return Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "Resources",
                IconFileName));
            yield return Path.Combine(Environment.CurrentDirectory, "Assets", IconFileName);
        }

        private void ToggleMainWindow()
        {
            if (_mainWindow?.IsVisible == true)
            {
                HideMainWindow();
            }
            else
            {
                ShowMainWindow();
            }
        }

        private void ToggleRecording()
        {
            var command = _mainViewModel.IsRecording
                ? _mainViewModel.StopRecordingCommand
                : _mainViewModel.StartRecordingCommand;

            ExecuteIfAvailable(command);
        }

        private static void ExecuteIfAvailable(System.Windows.Input.ICommand command)
        {
            if (command.CanExecute(null))
            {
                command.Execute(null);
            }
        }

        private void ApplySettingsChanged()
        {
            _mainViewModel.ApplySettingsChanges();
        }

        private void MainWindowClosing(object? sender, WindowClosingEventArgs e)
        {
            if (_isQuitting)
            {
                return;
            }

            e.Cancel = true;
            HideMainWindow();
        }

        private void MainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsRecording)
                || e.PropertyName == nameof(MainViewModel.StatusText)
                || e.PropertyName == nameof(MainViewModel.IsSending))
            {
                UpdateMenuState();
            }
        }

        private void UpdateMenuState()
        {
            if (_showHideItem != null)
            {
                _showHideItem.Header = _mainWindow?.IsVisible == true
                    ? "Hide BF-STT"
                    : "Show BF-STT";
            }

            if (_recordItem != null)
            {
                _recordItem.Header = _mainViewModel.IsRecording
                    ? "Stop & Send"
                    : "Start Recording";
                _recordItem.IsEnabled = _mainViewModel.IsRecording
                    ? _mainViewModel.StopRecordingCommand.CanExecute(null)
                    : _mainViewModel.StartRecordingCommand.CanExecute(null);
            }

            if (_resendItem != null)
            {
                _resendItem.IsEnabled = _mainViewModel.ResendAudioCommand.CanExecute(null);
            }

            if (_statusItem != null)
            {
                _statusItem.Header = $"Status: {_mainViewModel.StatusText}";
            }

            if (_trayIcon != null)
            {
                _trayIcon.ToolTipText = $"{AppName} - {_mainViewModel.StatusText}";
            }
        }

        private static NativeMenuItem DisabledItem(string header)
        {
            return new NativeMenuItem
            {
                Header = header,
                IsEnabled = false
            };
        }
    }
}
