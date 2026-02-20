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
                SelectedApi = _settingsService.CurrentSettings.SelectedApi,
                TestMode = _settingsService.CurrentSettings.TestMode
            };

            ApiKeyTextBox.Text = _tempSettings.ApiKey;
            SpeechmaticsApiKeyTextBox.Text = _tempSettings.SpeechmaticsApiKey;
            SonioxApiKeyTextBox.Text = _tempSettings.SonioxApiKey;
            OpenAIApiKeyTextBox.Text = _tempSettings.OpenAIApiKey;
            StartWithWindowsCheckBox.IsChecked = _tempSettings.StartWithWindows;
            TestModeCheckBox.IsChecked = _tempSettings.TestMode;

            // Set ComboBox selection based on current SelectedApi
            if (_tempSettings.SelectedApi == "Speechmatics")
            {
                SelectedApiComboBox.SelectedIndex = 1;
            }
            else if (_tempSettings.SelectedApi == "Soniox")
            {
                SelectedApiComboBox.SelectedIndex = 2;
            }
            else if (_tempSettings.SelectedApi == "OpenAI")
            {
                SelectedApiComboBox.SelectedIndex = 3;
            }
            else
            {
                SelectedApiComboBox.SelectedIndex = 0;
            }
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
            _tempSettings.TestMode = TestModeCheckBox.IsChecked ?? false;
            
            // Get Selected API from ComboBox
            if (SelectedApiComboBox.SelectedIndex == 3) _tempSettings.SelectedApi = "OpenAI";
            else if (SelectedApiComboBox.SelectedIndex == 2) _tempSettings.SelectedApi = "Soniox";
            else if (SelectedApiComboBox.SelectedIndex == 1) _tempSettings.SelectedApi = "Speechmatics";
            else _tempSettings.SelectedApi = "Deepgram";

            // If the selected API changed here, MainViewModel's UI binding might not automatically pick it up
            // unless MainViewModel listens to an event. However, it will apply on next restart or reload.

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
