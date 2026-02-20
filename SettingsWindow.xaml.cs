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
                SelectedApi = _settingsService.CurrentSettings.SelectedApi
            };

            ApiKeyTextBox.Text = _tempSettings.ApiKey;
            SpeechmaticsApiKeyTextBox.Text = _tempSettings.SpeechmaticsApiKey;
            StartWithWindowsCheckBox.IsChecked = _tempSettings.StartWithWindows;
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
