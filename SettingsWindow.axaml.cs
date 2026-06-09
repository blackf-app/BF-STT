using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using BF_STT.Services.Audio;
using BF_STT.Services.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace BF_STT
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService? _settingsService;
        private readonly UpdateService? _updateService;
        private AppSettings? _tempSettings;

        public bool? Result { get; private set; }

        public SettingsWindow() : this(null!, null!) { }

        public SettingsWindow(SettingsService settingsService, UpdateService updateService)
        {
            InitializeComponent();
            _settingsService = settingsService;
            _updateService = updateService;

            if (settingsService == null) return;

            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"Version: {version?.Major}.{version?.Minor}.{version?.Build}";

            _tempSettings = CopySettings(_settingsService!.CurrentSettings);

            ApiKeyTextBox.Text = _tempSettings.ApiKey;
            SpeechmaticsApiKeyTextBox.Text = _tempSettings.SpeechmaticsApiKey;
            SonioxApiKeyTextBox.Text = _tempSettings.SonioxApiKey;
            OpenAIApiKeyTextBox.Text = _tempSettings.OpenAIApiKey;
            ElevenLabsApiKeyTextBox.Text = _tempSettings.ElevenLabsApiKey;
            GoogleApiKeyTextBox.Text = _tempSettings.GoogleApiKey;
            AssemblyAIApiKeyTextBox.Text = _tempSettings.AssemblyAIApiKey;
            AzureApiKeyTextBox.Text = _tempSettings.AzureApiKey;
            AzureRegionTextBox.Text = _tempSettings.AzureBaseUrl;

            StartWithWindowsCheckBox.IsChecked = _tempSettings.StartWithWindows;
            AutoCheckUpdateCheckBox.IsChecked = _tempSettings.AutoCheckUpdate;
            TestModeCheckBox.IsChecked = _tempSettings.TestMode;
            NoiseSuppressionCheckBox.IsChecked = _tempSettings.EnableNoiseSuppression;
            MaxHistoryLimitTextBox.Text = _tempSettings.MaxHistoryItems.ToString();

            // Language selection
            for (int i = 0; i < LanguageComboBox.Items.Count; i++)
            {
                if (LanguageComboBox.Items[i] is ComboBoxItem item
                    && item.Tag?.ToString() == _tempSettings.DefaultLanguage)
                {
                    LanguageComboBox.SelectedIndex = i;
                    break;
                }
            }
            if (LanguageComboBox.SelectedIndex < 0) LanguageComboBox.SelectedIndex = 0;

            try
            {
                foreach (var dev in AudioDeviceEnumerator.EnumerateInputDevices())
                {
                    MicrophoneComboBox.Items.Add(new ComboBoxItem { Content = dev.Name, Tag = dev.Index });
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Failed to enumerate microphones");
            }

            for (int i = 0; i < MicrophoneComboBox.Items.Count; i++)
            {
                if (MicrophoneComboBox.Items[i] is ComboBoxItem item && item.Tag is int deviceId && deviceId == _tempSettings.MicrophoneDeviceNumber)
                {
                    MicrophoneComboBox.SelectedIndex = i;
                    break;
                }
            }
            if (MicrophoneComboBox.SelectedIndex < 0 && MicrophoneComboBox.Items.Count > 0)
                MicrophoneComboBox.SelectedIndex = 0;

            var hotkeys = new Dictionary<string, int>
            {
                { "F1", 0x70 }, { "F2", 0x71 }, { "F3", 0x72 }, { "F4", 0x73 },
                { "F5", 0x74 }, { "F6", 0x75 }, { "F7", 0x76 }, { "F8", 0x77 },
                { "F9", 0x78 }, { "F10", 0x79 }, { "F11", 0x7A }, { "F12", 0x7B },
                { "` (Tilde)", 0xC0 }
            };
            foreach (var hk in hotkeys)
            {
                HotkeyComboBox.Items.Add(new ComboBoxItem { Content = hk.Key, Tag = hk.Value });
                StopAndSendHotkeyComboBox.Items.Add(new ComboBoxItem { Content = hk.Key, Tag = hk.Value });
            }

            for (int i = 0; i < HotkeyComboBox.Items.Count; i++)
            {
                if (HotkeyComboBox.Items[i] is ComboBoxItem item && item.Tag is int vk && vk == _tempSettings.HotkeyVirtualKeyCode)
                {
                    HotkeyComboBox.SelectedIndex = i;
                    break;
                }
            }
            if (HotkeyComboBox.SelectedIndex < 0) HotkeyComboBox.SelectedIndex = 2;

            for (int i = 0; i < StopAndSendHotkeyComboBox.Items.Count; i++)
            {
                if (StopAndSendHotkeyComboBox.Items[i] is ComboBoxItem item && item.Tag is int vk && vk == _tempSettings.StopAndSendHotkeyVirtualKeyCode)
                {
                    StopAndSendHotkeyComboBox.SelectedIndex = i;
                    break;
                }
            }
            if (StopAndSendHotkeyComboBox.SelectedIndex < 0) StopAndSendHotkeyComboBox.SelectedIndex = 3;
        }

        private static AppSettings CopySettings(AppSettings src) => new AppSettings
        {
            ApiKey = src.ApiKey,
            SpeechmaticsApiKey = src.SpeechmaticsApiKey,
            SonioxApiKey = src.SonioxApiKey,
            StartWithWindows = src.StartWithWindows,
            AutoCheckUpdate = src.AutoCheckUpdate,
            StreamingUrl = src.StreamingUrl,
            Model = src.Model,
            SpeechmaticsBaseUrl = src.SpeechmaticsBaseUrl,
            SpeechmaticsModel = src.SpeechmaticsModel,
            SonioxBaseUrl = src.SonioxBaseUrl,
            SonioxStreamingUrl = src.SonioxStreamingUrl,
            SonioxModel = src.SonioxModel,
            OpenAIApiKey = src.OpenAIApiKey,
            OpenAIBaseUrl = src.OpenAIBaseUrl,
            OpenAIModel = src.OpenAIModel,
            ElevenLabsApiKey = src.ElevenLabsApiKey,
            ElevenLabsBaseUrl = src.ElevenLabsBaseUrl,
            ElevenLabsStreamingUrl = src.ElevenLabsStreamingUrl,
            ElevenLabsModel = src.ElevenLabsModel,
            GoogleApiKey = src.GoogleApiKey,
            GoogleBaseUrl = src.GoogleBaseUrl,
            GoogleModel = src.GoogleModel,
            AssemblyAIApiKey = src.AssemblyAIApiKey,
            AssemblyAIBaseUrl = src.AssemblyAIBaseUrl,
            AssemblyAIStreamingUrl = src.AssemblyAIStreamingUrl,
            AssemblyAIModel = src.AssemblyAIModel,
            AzureApiKey = src.AzureApiKey,
            AzureBaseUrl = src.AzureBaseUrl,
            AzureModel = src.AzureModel,
            BatchModeApi = src.BatchModeApi,
            StreamingModeApi = src.StreamingModeApi,
            TestMode = src.TestMode,
            DefaultLanguage = src.DefaultLanguage,
            HotkeyVirtualKeyCode = src.HotkeyVirtualKeyCode,
            StopAndSendHotkeyVirtualKeyCode = src.StopAndSendHotkeyVirtualKeyCode,
            MicrophoneDeviceNumber = src.MicrophoneDeviceNumber,
            MaxHistoryItems = src.MaxHistoryItems,
            EnableNoiseSuppression = src.EnableNoiseSuppression
        };

        private void Window_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        }

        private async void SaveButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_tempSettings == null || _settingsService == null) return;

            _tempSettings.ApiKey = ApiKeyTextBox.Text ?? "";
            _tempSettings.SpeechmaticsApiKey = SpeechmaticsApiKeyTextBox.Text ?? "";
            _tempSettings.SonioxApiKey = SonioxApiKeyTextBox.Text ?? "";
            _tempSettings.OpenAIApiKey = OpenAIApiKeyTextBox.Text ?? "";
            _tempSettings.ElevenLabsApiKey = ElevenLabsApiKeyTextBox.Text ?? "";
            _tempSettings.GoogleApiKey = GoogleApiKeyTextBox.Text ?? "";
            _tempSettings.AssemblyAIApiKey = AssemblyAIApiKeyTextBox.Text ?? "";
            _tempSettings.AzureApiKey = AzureApiKeyTextBox.Text ?? "";
            _tempSettings.AzureBaseUrl = AzureRegionTextBox.Text ?? "";

            _tempSettings.StartWithWindows = StartWithWindowsCheckBox.IsChecked ?? false;
            _tempSettings.AutoCheckUpdate = AutoCheckUpdateCheckBox.IsChecked ?? false;
            _tempSettings.TestMode = TestModeCheckBox.IsChecked ?? false;
            _tempSettings.EnableNoiseSuppression = NoiseSuppressionCheckBox.IsChecked ?? false;

            if (LanguageComboBox.SelectedItem is ComboBoxItem langItem)
            {
                _tempSettings.DefaultLanguage = langItem.Tag?.ToString() ?? "vi";
            }

            if (MicrophoneComboBox.SelectedItem is ComboBoxItem micItem && micItem.Tag is int deviceId)
            {
                _tempSettings.MicrophoneDeviceNumber = deviceId;
            }

            if (HotkeyComboBox.SelectedItem is ComboBoxItem hotkeyItem && hotkeyItem.Tag is int hvk)
            {
                _tempSettings.HotkeyVirtualKeyCode = hvk;
            }

            if (StopAndSendHotkeyComboBox.SelectedItem is ComboBoxItem stopSendHotkeyItem && stopSendHotkeyItem.Tag is int sshvk)
            {
                _tempSettings.StopAndSendHotkeyVirtualKeyCode = sshvk;
            }

            if (_tempSettings.HotkeyVirtualKeyCode == _tempSettings.StopAndSendHotkeyVirtualKeyCode)
            {
                await ShowMessageAsync("Cảnh báo", "Phím tắt mặc định và phím tắt 'Dừng & Gửi' không được trùng nhau.");
                return;
            }

            if (int.TryParse(MaxHistoryLimitTextBox.Text, out int maxHistory))
            {
                _tempSettings.MaxHistoryItems = Math.Clamp(maxHistory, 1, 1000);
            }

            _settingsService.SaveSettings(_tempSettings);
            Result = true;
            Close();
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            Result = false;
            Close();
        }

        private void OpenConfigFolderButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                string appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BF-STT");
                if (Directory.Exists(appDataPath))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = OperatingSystem.IsMacOS() ? "open" : appDataPath,
                        Arguments = OperatingSystem.IsMacOS() ? $"\"{appDataPath}\"" : "",
                        UseShellExecute = !OperatingSystem.IsMacOS()
                    };
                    Process.Start(psi);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Failed to open config folder");
            }
        }

        private async void CheckUpdateButtonClick(object? sender, RoutedEventArgs e)
        {
            if (_updateService == null) return;
            try
            {
                var button = sender as Button;
                if (button != null) button.IsEnabled = false;

                var release = await _updateService.CheckForUpdateAsync();

                if (release != null)
                {
                    await ShowMessageAsync("Cập Nhật Có Sẵn",
                        $"Đã có phiên bản mới ({release.Version}). Tải về để cập nhật thủ công.");
                    _ = _updateService.DownloadAndInstallUpdateAsync(release.DownloadUrl);
                }
                else
                {
                    await ShowMessageAsync("Thông báo", "Bạn đang sử dụng phiên bản mới nhất.");
                }

                if (button != null) button.IsEnabled = true;
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("Lỗi", $"Lỗi khi kiểm tra cập nhật: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task ShowMessageAsync(string title, string message)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 360,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SystemDecorations = SystemDecorations.BorderOnly,
                Background = Avalonia.Media.Brushes.DimGray,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(15),
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = message,
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            Foreground = Avalonia.Media.Brushes.White
                        }
                    }
                }
            };
            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
            };
            okButton.Click += (_, _) => dialog.Close();
            ((StackPanel)dialog.Content!).Children.Add(okButton);
            await dialog.ShowDialog(this);
        }
    }
}
