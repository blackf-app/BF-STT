using BF_STT.Services.Infrastructure;
using System.Windows;
using System.Windows.Input;

namespace BF_STT
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settingsService;
        private readonly UpdateService _updateService;
        private AppSettings _tempSettings;

        public SettingsWindow(SettingsService settingsService, UpdateService updateService)
        {
            InitializeComponent();
            _settingsService = settingsService;
            _updateService = updateService;

            // Set Version Text
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"Version: {version?.Major}.{version?.Minor}.{version?.Build}";

            _tempSettings = new AppSettings
            {
                ApiKey = _settingsService.CurrentSettings.ApiKey,
                SpeechmaticsApiKey = _settingsService.CurrentSettings.SpeechmaticsApiKey,
                SonioxApiKey = _settingsService.CurrentSettings.SonioxApiKey,
                StartWithWindows = _settingsService.CurrentSettings.StartWithWindows,
                AutoCheckUpdate = _settingsService.CurrentSettings.AutoCheckUpdate,
                StreamingUrl = _settingsService.CurrentSettings.StreamingUrl,
                Model = _settingsService.CurrentSettings.Model,
                SpeechmaticsBaseUrl = _settingsService.CurrentSettings.SpeechmaticsBaseUrl,
                SpeechmaticsModel = _settingsService.CurrentSettings.SpeechmaticsModel,
                SonioxBaseUrl = _settingsService.CurrentSettings.SonioxBaseUrl,
                SonioxStreamingUrl = _settingsService.CurrentSettings.SonioxStreamingUrl,
                SonioxModel = _settingsService.CurrentSettings.SonioxModel,
                OpenAIApiKey = _settingsService.CurrentSettings.OpenAIApiKey,
                OpenAIBaseUrl = _settingsService.CurrentSettings.OpenAIBaseUrl,
                OpenAIModel = _settingsService.CurrentSettings.OpenAIModel,
                ElevenLabsApiKey = _settingsService.CurrentSettings.ElevenLabsApiKey,
                ElevenLabsBaseUrl = _settingsService.CurrentSettings.ElevenLabsBaseUrl,
                ElevenLabsStreamingUrl = _settingsService.CurrentSettings.ElevenLabsStreamingUrl,
                ElevenLabsModel = _settingsService.CurrentSettings.ElevenLabsModel,
                GoogleApiKey = _settingsService.CurrentSettings.GoogleApiKey,
                GoogleBaseUrl = _settingsService.CurrentSettings.GoogleBaseUrl,
                GoogleModel = _settingsService.CurrentSettings.GoogleModel,
                AssemblyAIApiKey = _settingsService.CurrentSettings.AssemblyAIApiKey,
                AssemblyAIBaseUrl = _settingsService.CurrentSettings.AssemblyAIBaseUrl,
                AssemblyAIStreamingUrl = _settingsService.CurrentSettings.AssemblyAIStreamingUrl,
                AssemblyAIModel = _settingsService.CurrentSettings.AssemblyAIModel,
                AzureApiKey = _settingsService.CurrentSettings.AzureApiKey,
                AzureBaseUrl = _settingsService.CurrentSettings.AzureBaseUrl,
                AzureModel = _settingsService.CurrentSettings.AzureModel,
                BatchModeApi = _settingsService.CurrentSettings.BatchModeApi,
                StreamingModeApi = _settingsService.CurrentSettings.StreamingModeApi,
                TestMode = _settingsService.CurrentSettings.TestMode,
                DefaultLanguage = _settingsService.CurrentSettings.DefaultLanguage,
                SelectedTtsProvider = _settingsService.CurrentSettings.SelectedTtsProvider,
                HotkeyVirtualKeyCode = _settingsService.CurrentSettings.HotkeyVirtualKeyCode,
                TtsHotkeyVirtualKeyCode = _settingsService.CurrentSettings.TtsHotkeyVirtualKeyCode,
                StopAndSendHotkeyVirtualKeyCode = _settingsService.CurrentSettings.StopAndSendHotkeyVirtualKeyCode,
                MicrophoneDeviceNumber = _settingsService.CurrentSettings.MicrophoneDeviceNumber,
                MaxHistoryItems = _settingsService.CurrentSettings.MaxHistoryItems,
                EnableNoiseSuppression = _settingsService.CurrentSettings.EnableNoiseSuppression,
                DeepgramTtsApiKey = _settingsService.CurrentSettings.DeepgramTtsApiKey,
                DeepgramTtsBaseUrl = _settingsService.CurrentSettings.DeepgramTtsBaseUrl,
                DeepgramTtsModel = _settingsService.CurrentSettings.DeepgramTtsModel,
                SpeechmaticsTtsApiKey = _settingsService.CurrentSettings.SpeechmaticsTtsApiKey,
                SpeechmaticsTtsBaseUrl = _settingsService.CurrentSettings.SpeechmaticsTtsBaseUrl,
                SpeechmaticsTtsVoice = _settingsService.CurrentSettings.SpeechmaticsTtsVoice,
                SonioxTtsApiKey = _settingsService.CurrentSettings.SonioxTtsApiKey,
                SonioxTtsBaseUrl = _settingsService.CurrentSettings.SonioxTtsBaseUrl,
                SonioxTtsModel = _settingsService.CurrentSettings.SonioxTtsModel,
                SonioxTtsVoice = _settingsService.CurrentSettings.SonioxTtsVoice,
                OpenAITtsApiKey = _settingsService.CurrentSettings.OpenAITtsApiKey,
                OpenAITtsBaseUrl = _settingsService.CurrentSettings.OpenAITtsBaseUrl,
                OpenAITtsModel = _settingsService.CurrentSettings.OpenAITtsModel,
                OpenAITtsVoice = _settingsService.CurrentSettings.OpenAITtsVoice,
                ElevenLabsTtsApiKey = _settingsService.CurrentSettings.ElevenLabsTtsApiKey,
                ElevenLabsTtsBaseUrl = _settingsService.CurrentSettings.ElevenLabsTtsBaseUrl,
                ElevenLabsTtsModel = _settingsService.CurrentSettings.ElevenLabsTtsModel,
                ElevenLabsTtsVoice = _settingsService.CurrentSettings.ElevenLabsTtsVoice,
                GoogleTtsApiKey = _settingsService.CurrentSettings.GoogleTtsApiKey,
                GoogleTtsBaseUrl = _settingsService.CurrentSettings.GoogleTtsBaseUrl,
                GoogleTtsVoice = _settingsService.CurrentSettings.GoogleTtsVoice,
                AzureTtsApiKey = _settingsService.CurrentSettings.AzureTtsApiKey,
                AzureTtsRegion = _settingsService.CurrentSettings.AzureTtsRegion,
                AzureTtsVoice = _settingsService.CurrentSettings.AzureTtsVoice,
                TtsProviderVolumes = new Dictionary<string, float>(_settingsService.CurrentSettings.TtsProviderVolumes, StringComparer.OrdinalIgnoreCase),
                TtsProviderRates = new Dictionary<string, float>(_settingsService.CurrentSettings.TtsProviderRates, StringComparer.OrdinalIgnoreCase)
            };

            // Load API Keys
            ApiKeyTextBox.Text = _tempSettings.ApiKey;
            SpeechmaticsApiKeyTextBox.Text = _tempSettings.SpeechmaticsApiKey;
            SonioxApiKeyTextBox.Text = _tempSettings.SonioxApiKey;
            OpenAIApiKeyTextBox.Text = _tempSettings.OpenAIApiKey;
            ElevenLabsApiKeyTextBox.Text = _tempSettings.ElevenLabsApiKey;
            GoogleApiKeyTextBox.Text = _tempSettings.GoogleApiKey;
            AssemblyAIApiKeyTextBox.Text = _tempSettings.AssemblyAIApiKey;
            AzureApiKeyTextBox.Text = _tempSettings.AzureApiKey;
            AzureRegionTextBox.Text = _tempSettings.AzureBaseUrl;
            OpenAITtsApiKeyTextBox.Text = _tempSettings.OpenAITtsApiKey;
            OpenAITtsVoiceTextBox.Text = _tempSettings.OpenAITtsVoice;
            ElevenLabsTtsApiKeyTextBox.Text = _tempSettings.ElevenLabsTtsApiKey;
            ElevenLabsTtsVoiceTextBox.Text = _tempSettings.ElevenLabsTtsVoice;
            DeepgramTtsApiKeyTextBox.Text = _tempSettings.DeepgramTtsApiKey;
            SonioxTtsApiKeyTextBox.Text = _tempSettings.SonioxTtsApiKey;
            SpeechmaticsTtsApiKeyTextBox.Text = _tempSettings.SpeechmaticsTtsApiKey;
            GoogleTtsApiKeyTextBox.Text = _tempSettings.GoogleTtsApiKey;
            AzureTtsApiKeyTextBox.Text = _tempSettings.AzureTtsApiKey;
            InitializeTtsPlaybackSliders();

            // Load Checkboxes
            StartWithWindowsCheckBox.IsChecked = _tempSettings.StartWithWindows;
            AutoCheckUpdateCheckBox.IsChecked = _tempSettings.AutoCheckUpdate;
            TestModeCheckBox.IsChecked = _tempSettings.TestMode;
            NoiseSuppressionCheckBox.IsChecked = _tempSettings.EnableNoiseSuppression;
            MaxHistoryLimitTextBox.Text = _tempSettings.MaxHistoryItems.ToString();

            // Set Language ComboBox
            for (int i = 0; i < LanguageComboBox.Items.Count; i++)
            {
                if (LanguageComboBox.Items[i] is System.Windows.Controls.ComboBoxItem item
                    && item.Tag?.ToString() == _tempSettings.DefaultLanguage)
                {
                    LanguageComboBox.SelectedIndex = i;
                    break;
                }
            }
            if (LanguageComboBox.SelectedIndex < 0) LanguageComboBox.SelectedIndex = 0;

            // Enumerate Microphones
            for (int i = 0; i < NAudio.Wave.WaveIn.DeviceCount; i++)
            {
                var caps = NAudio.Wave.WaveIn.GetCapabilities(i);
                MicrophoneComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = caps.ProductName, Tag = i });
            }

            // Set current microphone
            for (int i = 0; i < MicrophoneComboBox.Items.Count; i++)
            {
                if (MicrophoneComboBox.Items[i] is System.Windows.Controls.ComboBoxItem item && (int)item.Tag == _tempSettings.MicrophoneDeviceNumber)
                {
                    MicrophoneComboBox.SelectedIndex = i;
                    break;
                }
            }
            if (MicrophoneComboBox.SelectedIndex < 0 && MicrophoneComboBox.Items.Count > 0) MicrophoneComboBox.SelectedIndex = 0;

            var hotkeys = new System.Collections.Generic.Dictionary<string, int>
            {
                { "F1", 0x70 }, { "F2", 0x71 }, { "F3", 0x72 }, { "F4", 0x73 },
                { "F5", 0x74 }, { "F6", 0x75 }, { "F7", 0x76 }, { "F8", 0x77 },
                { "F9", 0x78 }, { "F10", 0x79 }, { "F11", 0x7A }, { "F12", 0x7B },
                { "` (Tilde)", 0xC0 }
            };
            foreach (var hk in hotkeys)
            {
                HotkeyComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = hk.Key, Tag = hk.Value });
                TtsHotkeyComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = hk.Key, Tag = hk.Value });
                StopAndSendHotkeyComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = hk.Key, Tag = hk.Value });
            }

            foreach (var provider in new[] { "Deepgram", "Speechmatics", "Soniox", "OpenAI", "ElevenLabs", "Google", "Azure" })
            {
                TtsProviderComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = provider, Tag = provider });
            }
            TtsProviderComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem
            {
                Content = "AssemblyAI (unavailable)",
                Tag = "AssemblyAI",
                IsEnabled = false,
                ToolTip = "AssemblyAI has voice-agent TTS but no standalone text-to-speech endpoint."
            });
            for (int i = 0; i < TtsProviderComboBox.Items.Count; i++)
            {
                if (TtsProviderComboBox.Items[i] is System.Windows.Controls.ComboBoxItem item
                    && string.Equals(item.Tag?.ToString(), _tempSettings.SelectedTtsProvider, StringComparison.OrdinalIgnoreCase)
                    && item.IsEnabled)
                {
                    TtsProviderComboBox.SelectedIndex = i;
                    break;
                }
            }
            if (TtsProviderComboBox.SelectedIndex < 0) TtsProviderComboBox.SelectedIndex = 3; // OpenAI
            
            for (int i = 0; i < HotkeyComboBox.Items.Count; i++)
            {
                if (HotkeyComboBox.Items[i] is System.Windows.Controls.ComboBoxItem item && (int)item.Tag == _tempSettings.HotkeyVirtualKeyCode)
                {
                    HotkeyComboBox.SelectedIndex = i;
                    break;
                }
            }
            if (HotkeyComboBox.SelectedIndex < 0) HotkeyComboBox.SelectedIndex = 2; // Default F3

            for (int i = 0; i < TtsHotkeyComboBox.Items.Count; i++)
            {
                if (TtsHotkeyComboBox.Items[i] is System.Windows.Controls.ComboBoxItem item && (int)item.Tag == _tempSettings.TtsHotkeyVirtualKeyCode)
                {
                    TtsHotkeyComboBox.SelectedIndex = i;
                    break;
                }
            }
            if (TtsHotkeyComboBox.SelectedIndex < 0) TtsHotkeyComboBox.SelectedIndex = 1; // Default F2

            for (int i = 0; i < StopAndSendHotkeyComboBox.Items.Count; i++)
            {
                if (StopAndSendHotkeyComboBox.Items[i] is System.Windows.Controls.ComboBoxItem item && (int)item.Tag == _tempSettings.StopAndSendHotkeyVirtualKeyCode)
                {
                    StopAndSendHotkeyComboBox.SelectedIndex = i;
                    break;
                }
            }
            if (StopAndSendHotkeyComboBox.SelectedIndex < 0) StopAndSendHotkeyComboBox.SelectedIndex = 3; // Default F4
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Save API Keys
            _tempSettings.ApiKey = ApiKeyTextBox.Text;
            _tempSettings.SpeechmaticsApiKey = SpeechmaticsApiKeyTextBox.Text;
            _tempSettings.SonioxApiKey = SonioxApiKeyTextBox.Text;
            _tempSettings.OpenAIApiKey = OpenAIApiKeyTextBox.Text;
            _tempSettings.ElevenLabsApiKey = ElevenLabsApiKeyTextBox.Text;
            _tempSettings.GoogleApiKey = GoogleApiKeyTextBox.Text;
            _tempSettings.AssemblyAIApiKey = AssemblyAIApiKeyTextBox.Text;
            _tempSettings.AzureApiKey = AzureApiKeyTextBox.Text;
            _tempSettings.AzureBaseUrl = AzureRegionTextBox.Text;
            _tempSettings.OpenAITtsApiKey = OpenAITtsApiKeyTextBox.Text;
            _tempSettings.OpenAITtsVoice = OpenAITtsVoiceTextBox.Text;
            _tempSettings.ElevenLabsTtsApiKey = ElevenLabsTtsApiKeyTextBox.Text;
            _tempSettings.ElevenLabsTtsVoice = ElevenLabsTtsVoiceTextBox.Text;
            _tempSettings.DeepgramTtsApiKey = DeepgramTtsApiKeyTextBox.Text;
            _tempSettings.SonioxTtsApiKey = SonioxTtsApiKeyTextBox.Text;
            _tempSettings.SpeechmaticsTtsApiKey = SpeechmaticsTtsApiKeyTextBox.Text;
            _tempSettings.GoogleTtsApiKey = GoogleTtsApiKeyTextBox.Text;
            _tempSettings.AzureTtsApiKey = AzureTtsApiKeyTextBox.Text;
            SaveTtsPlaybackSettings();

            if (TtsProviderComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem ttsProviderItem)
            {
                _tempSettings.SelectedTtsProvider = ttsProviderItem.Tag?.ToString() ?? "OpenAI";
            }

            // Save Checkboxes
            _tempSettings.StartWithWindows = StartWithWindowsCheckBox.IsChecked ?? false;
            _tempSettings.AutoCheckUpdate = AutoCheckUpdateCheckBox.IsChecked ?? false;
            _tempSettings.TestMode = TestModeCheckBox.IsChecked ?? false;
            _tempSettings.EnableNoiseSuppression = NoiseSuppressionCheckBox.IsChecked ?? false;

            // Get Language from ComboBox
            if (LanguageComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem langItem)
            {
                _tempSettings.DefaultLanguage = langItem.Tag?.ToString() ?? "vi";
            }

            // Get Microphone
            if (MicrophoneComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem micItem && micItem.Tag is int deviceId)
            {
                _tempSettings.MicrophoneDeviceNumber = deviceId;
            }

            // Get Hotkey
            if (HotkeyComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem hotkeyItem && hotkeyItem.Tag is int hvk)
            {
                _tempSettings.HotkeyVirtualKeyCode = hvk;
            }

            if (TtsHotkeyComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem ttsHotkeyItem && ttsHotkeyItem.Tag is int thvk)
            {
                _tempSettings.TtsHotkeyVirtualKeyCode = thvk;
            }

            // Get Stop & Send Hotkey
            if (StopAndSendHotkeyComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem stopSendHotkeyItem && stopSendHotkeyItem.Tag is int sshvk)
            {
                _tempSettings.StopAndSendHotkeyVirtualKeyCode = sshvk;
            }

            // Validation: Hotkeys must be different
            if (_tempSettings.HotkeyVirtualKeyCode == _tempSettings.StopAndSendHotkeyVirtualKeyCode
                || _tempSettings.HotkeyVirtualKeyCode == _tempSettings.TtsHotkeyVirtualKeyCode
                || _tempSettings.TtsHotkeyVirtualKeyCode == _tempSettings.StopAndSendHotkeyVirtualKeyCode)
            {
                System.Windows.MessageBox.Show("Hotkeys for recording, TTS, and Stop & Send must be different.", "Warning", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // Get History Limit
            if (int.TryParse(MaxHistoryLimitTextBox.Text, out int maxHistory))
            {
                _tempSettings.MaxHistoryItems = Math.Clamp(maxHistory, 1, 1000);
            }

            _settingsService.SaveSettings(_tempSettings);
            DialogResult = true;
            Close();
        }

        private void InitializeTtsPlaybackSliders()
        {
            SetSliderPercent(DeepgramVolumeSlider, _tempSettings.GetTtsProviderVolume("Deepgram"));
            SetSliderPercent(DeepgramRateSlider, _tempSettings.GetTtsProviderRate("Deepgram"));
            SetSliderPercent(SpeechmaticsVolumeSlider, _tempSettings.GetTtsProviderVolume("Speechmatics"));
            SetSliderPercent(SpeechmaticsRateSlider, _tempSettings.GetTtsProviderRate("Speechmatics"));
            SetSliderPercent(SonioxVolumeSlider, _tempSettings.GetTtsProviderVolume("Soniox"));
            SetSliderPercent(SonioxRateSlider, _tempSettings.GetTtsProviderRate("Soniox"));
            SetSliderPercent(OpenAIVolumeSlider, _tempSettings.GetTtsProviderVolume("OpenAI"));
            SetSliderPercent(OpenAIRateSlider, _tempSettings.GetTtsProviderRate("OpenAI"));
            SetSliderPercent(ElevenLabsVolumeSlider, _tempSettings.GetTtsProviderVolume("ElevenLabs"));
            SetSliderPercent(ElevenLabsRateSlider, _tempSettings.GetTtsProviderRate("ElevenLabs"));
            SetSliderPercent(GoogleVolumeSlider, _tempSettings.GetTtsProviderVolume("Google"));
            SetSliderPercent(GoogleRateSlider, _tempSettings.GetTtsProviderRate("Google"));
            SetSliderPercent(AzureVolumeSlider, _tempSettings.GetTtsProviderVolume("Azure"));
            SetSliderPercent(AzureRateSlider, _tempSettings.GetTtsProviderRate("Azure"));
        }

        private void SaveTtsPlaybackSettings()
        {
            _tempSettings.SetTtsProviderVolume("Deepgram", SliderPercentToValue(DeepgramVolumeSlider));
            _tempSettings.SetTtsProviderRate("Deepgram", SliderPercentToValue(DeepgramRateSlider));
            _tempSettings.SetTtsProviderVolume("Speechmatics", SliderPercentToValue(SpeechmaticsVolumeSlider));
            _tempSettings.SetTtsProviderRate("Speechmatics", SliderPercentToValue(SpeechmaticsRateSlider));
            _tempSettings.SetTtsProviderVolume("Soniox", SliderPercentToValue(SonioxVolumeSlider));
            _tempSettings.SetTtsProviderRate("Soniox", SliderPercentToValue(SonioxRateSlider));
            _tempSettings.SetTtsProviderVolume("OpenAI", SliderPercentToValue(OpenAIVolumeSlider));
            _tempSettings.SetTtsProviderRate("OpenAI", SliderPercentToValue(OpenAIRateSlider));
            _tempSettings.SetTtsProviderVolume("ElevenLabs", SliderPercentToValue(ElevenLabsVolumeSlider));
            _tempSettings.SetTtsProviderRate("ElevenLabs", SliderPercentToValue(ElevenLabsRateSlider));
            _tempSettings.SetTtsProviderVolume("Google", SliderPercentToValue(GoogleVolumeSlider));
            _tempSettings.SetTtsProviderRate("Google", SliderPercentToValue(GoogleRateSlider));
            _tempSettings.SetTtsProviderVolume("Azure", SliderPercentToValue(AzureVolumeSlider));
            _tempSettings.SetTtsProviderRate("Azure", SliderPercentToValue(AzureRateSlider));
        }

        private static void SetSliderPercent(System.Windows.Controls.Slider slider, float value)
        {
            slider.Value = Math.Round(value * 100f);
        }

        private static float SliderPercentToValue(System.Windows.Controls.Slider slider)
        {
            return (float)(slider.Value / 100d);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void OpenConfigFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string appDataPath = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "BF-STT");
                if (System.IO.Directory.Exists(appDataPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                    {
                        FileName = appDataPath,
                        UseShellExecute = true,
                        Verb = "open"
                    });
                }
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show($"Không thể mở thư mục: {ex.Message}", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private async void CheckUpdateButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as System.Windows.Controls.Button;
                if (button != null) button.IsEnabled = false;

                var release = await _updateService.CheckForUpdateAsync();

                if (release != null)
                {
                    var result = System.Windows.MessageBox.Show(
                        $"Đã có phiên bản mới ({release.Version}). Bạn có muốn cập nhật ngay không?\n\n" +
                        "Ứng dụng sẽ tự động tải về, cài đặt và khởi động lại.",
                        "Cập Nhật Có Sẵn",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Information);

                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        _ = _updateService.DownloadAndInstallUpdateAsync(release.DownloadUrl);
                    }
                }
                else
                {
                    System.Windows.MessageBox.Show("Bạn đang sử dụng phiên bản mới nhất.", "Thông báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }

                if (button != null) button.IsEnabled = true;
            }
            catch (System.Exception ex)
            {
                System.Windows.MessageBox.Show($"Lỗi khi kiểm tra cập nhật: {ex.Message}", "Lỗi", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
