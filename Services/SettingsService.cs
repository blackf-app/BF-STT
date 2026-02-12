using Microsoft.Win32;
using System.IO;
using System.Text.Json;

namespace BF_STT.Services
{
    public class AppSettings
    {
        public string ApiKey { get; set; } = "";
        public bool StartWithWindows { get; set; }
        public string StreamingUrl { get; set; } = "wss://api.deepgram.com/v1/listen";
        public string BaseUrl { get; set; } = "https://api.deepgram.com/v1/listen";
        public string Model { get; set; } = "nova-3";
    }

    public class SettingsService
    {
        private readonly string _settingsFilePath;
        private const string AppName = "BF-STT";

        public AppSettings CurrentSettings { get; private set; } = new AppSettings();

        public SettingsService()
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);
            Directory.CreateDirectory(appDataPath);
            _settingsFilePath = Path.Combine(appDataPath, "settings.json");
            LoadSettings();
        }

        public void LoadSettings()
        {
            bool needsFix = false;
            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    CurrentSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                catch
                {
                    CurrentSettings = new AppSettings();
                }
            }
            else
            {
                // Fallback: Check appsettings.json in application directory
                CurrentSettings = new AppSettings();
                try
                {
                    var appSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                    if (File.Exists(appSettingsPath))
                    {
                        var json = File.ReadAllText(appSettingsPath);
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("Deepgram", out var deepgram) && 
                            deepgram.TryGetProperty("ApiKey", out var apiKey))
                        {
                            var key = apiKey.GetString();
                            if (!string.IsNullOrWhiteSpace(key))
                            {
                                CurrentSettings.ApiKey = key;
                                needsFix = true;
                            }
                        }
                    }
                }
                catch { /* Ignore errors in fallback */ }
            }

            // Sanitize StreamingUrl (remove query params)
            if (CurrentSettings.StreamingUrl.Contains("?"))
            {
                CurrentSettings.StreamingUrl = CurrentSettings.StreamingUrl.Split('?')[0];
                needsFix = true;
            }

            // Migrate Model to nova-3 if using older defaults
            if (CurrentSettings.Model == "nova-2-general" || CurrentSettings.Model == "nova-2")
            {
                CurrentSettings.Model = "nova-3";
                needsFix = true;
            }

            if (needsFix)
            {
                SaveSettings(CurrentSettings);
            }
        }

        public void SaveSettings(AppSettings newSettings)
        {
            CurrentSettings = newSettings;
            var json = JsonSerializer.Serialize(CurrentSettings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
            SetStartWithWindows(CurrentSettings.StartWithWindows);
        }

        private void SetStartWithWindows(bool enable)
        {
            const string runKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(runKey, true))
            {
                if (key != null)
                {
                    if (enable)
                    {
                        string? exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                        if (exePath != null)
                        {
                            key.SetValue(AppName, exePath);
                        }
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                    }
                }
            }
        }
    }
}
