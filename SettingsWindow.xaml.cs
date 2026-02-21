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
                BatchModeApi = _settingsService.CurrentSettings.BatchModeApi,
                StreamingModeApi = _settingsService.CurrentSettings.StreamingModeApi,
                TestMode = _settingsService.CurrentSettings.TestMode
            };

            ApiKeyTextBox.Text = _tempSettings.ApiKey;
            SpeechmaticsApiKeyTextBox.Text = _tempSettings.SpeechmaticsApiKey;
            SonioxApiKeyTextBox.Text = _tempSettings.SonioxApiKey;
            OpenAIApiKeyTextBox.Text = _tempSettings.OpenAIApiKey;
            StartWithWindowsCheckBox.IsChecked = _tempSettings.StartWithWindows;
            TestModeCheckBox.IsChecked = _tempSettings.TestMode;

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
