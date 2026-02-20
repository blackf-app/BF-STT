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
                StartWithWindows = _settingsService.CurrentSettings.StartWithWindows,
                StreamingUrl = _settingsService.CurrentSettings.StreamingUrl,
                Model = _settingsService.CurrentSettings.Model,
                SpeechmaticsBaseUrl = _settingsService.CurrentSettings.SpeechmaticsBaseUrl,
                SpeechmaticsModel = _settingsService.CurrentSettings.SpeechmaticsModel,
                SelectedApi = _settingsService.CurrentSettings.SelectedApi,
                TestMode = _settingsService.CurrentSettings.TestMode
            };

            ApiKeyTextBox.Text = _tempSettings.ApiKey;
            SpeechmaticsApiKeyTextBox.Text = _tempSettings.SpeechmaticsApiKey;
            StartWithWindowsCheckBox.IsChecked = _tempSettings.StartWithWindows;
            TestModeCheckBox.IsChecked = _tempSettings.TestMode;

            // Set ComboBox selection based on current SelectedApi
            if (_tempSettings.SelectedApi == "Speechmatics")
            {
                SelectedApiComboBox.SelectedIndex = 1;
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
            _tempSettings.StartWithWindows = StartWithWindowsCheckBox.IsChecked ?? false;
            _tempSettings.TestMode = TestModeCheckBox.IsChecked ?? false;
            
            // Get Selected API from ComboBox
            _tempSettings.SelectedApi = SelectedApiComboBox.SelectedIndex == 1 ? "Speechmatics" : "Deepgram";

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
