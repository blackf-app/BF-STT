using Microsoft.Win32;
using System.IO;
using System.Text.Json;

namespace BF_STT.Services.Infrastructure
{
    public class AppSettings
    {
        // General
        public bool StartWithWindows { get; set; }
        public bool AutoCheckUpdate { get; set; } = true;
        public bool TestMode { get; set; } = false;
        public string BatchModeApi { get; set; } = "Deepgram";
        public string StreamingModeApi { get; set; } = "Deepgram";
        public string DefaultLanguage { get; set; } = "vi";
        public int HotkeyVirtualKeyCode { get; set; } = 0x72; // VK_F3
        public int StopAndSendHotkeyVirtualKeyCode { get; set; } = 0x73; // VK_F4
        public int MicrophoneDeviceNumber { get; set; } = 0;
        public int MaxHistoryItems { get; set; } = 100;

        // Legacy for migration
        public string? SelectedApi { get; set; } 

        // Deepgram
        public string ApiKey { get; set; } = "";
        public string StreamingUrl { get; set; } = "wss://api.deepgram.com/v1/listen";
        public string BaseUrl { get; set; } = "https://api.deepgram.com/v1/listen";
        public string Model { get; set; } = "nova-3";

        // Speechmatics
        public string SpeechmaticsApiKey { get; set; } = "";
        public string SpeechmaticsStreamingUrl { get; set; } = "wss://eu2.rt.speechmatics.com/v2";
        public string SpeechmaticsBaseUrl { get; set; } = "https://asr.api.speechmatics.com/v2";
        public string SpeechmaticsModel { get; set; } = ""; // E.g., not strictly necessary but keeps consistency

        // Soniox
        public string SonioxApiKey { get; set; } = "";
        public string SonioxStreamingUrl { get; set; } = "wss://stt-rt.soniox.com/transcribe-websocket";
        public string SonioxBaseUrl { get; set; } = "https://api.soniox.com/v1";
        public string SonioxModel { get; set; } = "";

        // OpenAI Whisper
        public string OpenAIApiKey { get; set; } = "";
        public string OpenAIBaseUrl { get; set; } = "https://api.openai.com/v1/audio/transcriptions";
        public string OpenAIModel { get; set; } = "whisper-1";

        // ElevenLabs
        public string ElevenLabsApiKey { get; set; } = "";
        public string ElevenLabsBaseUrl { get; set; } = "https://api.elevenlabs.io/v1/speech-to-text";
        public string ElevenLabsStreamingUrl { get; set; } = "wss://api.elevenlabs.io/v1/speech-to-text/streaming";
        public string ElevenLabsModel { get; set; } = "scribe_v2";

        // Noise Suppression
        public bool EnableNoiseSuppression { get; set; } = false;
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

            // Migrate SelectedApi to BatchModeApi and StreamingModeApi
            if (!string.IsNullOrEmpty(CurrentSettings.SelectedApi))
            {
                CurrentSettings.BatchModeApi = CurrentSettings.SelectedApi;
                CurrentSettings.StreamingModeApi = CurrentSettings.SelectedApi;
                CurrentSettings.SelectedApi = null;
                needsFix = true;
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
