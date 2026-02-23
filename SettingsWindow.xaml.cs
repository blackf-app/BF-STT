using BF_STT.Services;
using System.Windows;
using System.Windows.Input;

namespace BF_STT
{
    public partial class SettingsWindow : Window
    {
        private readonly SettingsService _settingsService;
        private AppSettings _tempSettings;

        public SettingsWindow(SettingsService settingsService)
        {
            InitializeComponent();
            _settingsService = settingsService;
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
                BatchModeApi = _settingsService.CurrentSettings.BatchModeApi,
                StreamingModeApi = _settingsService.CurrentSettings.StreamingModeApi,
                TestMode = _settingsService.CurrentSettings.TestMode,
                DefaultLanguage = _settingsService.CurrentSettings.DefaultLanguage,
                HotkeyVirtualKeyCode = _settingsService.CurrentSettings.HotkeyVirtualKeyCode,
                StopAndSendHotkeyVirtualKeyCode = _settingsService.CurrentSettings.StopAndSendHotkeyVirtualKeyCode,
                MicrophoneDeviceNumber = _settingsService.CurrentSettings.MicrophoneDeviceNumber,
                MaxHistoryItems = _settingsService.CurrentSettings.MaxHistoryItems
            };

            ApiKeyTextBox.Text = _tempSettings.ApiKey;
            SpeechmaticsApiKeyTextBox.Text = _tempSettings.SpeechmaticsApiKey;
            SonioxApiKeyTextBox.Text = _tempSettings.SonioxApiKey;
            OpenAIApiKeyTextBox.Text = _tempSettings.OpenAIApiKey;
            StartWithWindowsCheckBox.IsChecked = _tempSettings.StartWithWindows;
            AutoCheckUpdateCheckBox.IsChecked = _tempSettings.AutoCheckUpdate;
            TestModeCheckBox.IsChecked = _tempSettings.TestMode;
            MaxHistoryLimitTextBox.Text = _tempSettings.MaxHistoryItems.ToString();

            // Set ComboBox selection based on current BatchModeApi
            if (_tempSettings.BatchModeApi == "Speechmatics") BatchModeApiComboBox.SelectedIndex = 1;
            else if (_tempSettings.BatchModeApi == "Soniox") BatchModeApiComboBox.SelectedIndex = 2;
            else if (_tempSettings.BatchModeApi == "OpenAI") BatchModeApiComboBox.SelectedIndex = 3;
            else BatchModeApiComboBox.SelectedIndex = 0;

            // Set ComboBox selection based on current StreamingModeApi
            if (_tempSettings.StreamingModeApi == "Speechmatics") StreamingModeApiComboBox.SelectedIndex = 1;
            else if (_tempSettings.StreamingModeApi == "Soniox") StreamingModeApiComboBox.SelectedIndex = 2;
            else if (_tempSettings.StreamingModeApi == "OpenAI") StreamingModeApiComboBox.SelectedIndex = 3;
            else StreamingModeApiComboBox.SelectedIndex = 0;

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
                StopAndSendHotkeyComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = hk.Key, Tag = hk.Value });
            }
            
            for (int i = 0; i < HotkeyComboBox.Items.Count; i++)
            {
                if (HotkeyComboBox.Items[i] is System.Windows.Controls.ComboBoxItem item && (int)item.Tag == _tempSettings.HotkeyVirtualKeyCode)
                {
                    HotkeyComboBox.SelectedIndex = i;
                    break;
                }
            }
            if (HotkeyComboBox.SelectedIndex < 0) HotkeyComboBox.SelectedIndex = 2; // Default F3

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
            _tempSettings.ApiKey = ApiKeyTextBox.Text;
            _tempSettings.SpeechmaticsApiKey = SpeechmaticsApiKeyTextBox.Text;
            _tempSettings.SonioxApiKey = SonioxApiKeyTextBox.Text;
            _tempSettings.OpenAIApiKey = OpenAIApiKeyTextBox.Text;
            _tempSettings.StartWithWindows = StartWithWindowsCheckBox.IsChecked ?? false;
            _tempSettings.AutoCheckUpdate = AutoCheckUpdateCheckBox.IsChecked ?? false;
            _tempSettings.TestMode = TestModeCheckBox.IsChecked ?? false;
            
            // Get Batch API from ComboBox
            if (BatchModeApiComboBox.SelectedIndex == 3) _tempSettings.BatchModeApi = "OpenAI";
            else if (BatchModeApiComboBox.SelectedIndex == 2) _tempSettings.BatchModeApi = "Soniox";
            else if (BatchModeApiComboBox.SelectedIndex == 1) _tempSettings.BatchModeApi = "Speechmatics";
            else _tempSettings.BatchModeApi = "Deepgram";

            // Get Streaming API from ComboBox
            if (StreamingModeApiComboBox.SelectedIndex == 3) _tempSettings.StreamingModeApi = "OpenAI";
            else if (StreamingModeApiComboBox.SelectedIndex == 2) _tempSettings.StreamingModeApi = "Soniox";
            else if (StreamingModeApiComboBox.SelectedIndex == 1) _tempSettings.StreamingModeApi = "Speechmatics";
            else _tempSettings.StreamingModeApi = "Deepgram";

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

            // Get Stop & Send Hotkey
            if (StopAndSendHotkeyComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem stopSendHotkeyItem && stopSendHotkeyItem.Tag is int sshvk)
            {
                _tempSettings.StopAndSendHotkeyVirtualKeyCode = sshvk;
            }

            // Validation: Hotkeys must be different
            if (_tempSettings.HotkeyVirtualKeyCode == _tempSettings.StopAndSendHotkeyVirtualKeyCode)
            {
                System.Windows.MessageBox.Show("Phím tắt mặc định và phím tắt 'Dừng & Gửi' không được trùng nhau.", "Cảnh báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
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

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
