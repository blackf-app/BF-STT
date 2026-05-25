using Microsoft.Extensions.Logging;
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
        public string SelectedTtsProvider { get; set; } = "OpenAI";
        public int HotkeyVirtualKeyCode { get; set; } = 0x72; // VK_F3
        public int TtsHotkeyVirtualKeyCode { get; set; } = 0x71; // VK_F2
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

        // Google
        public string GoogleApiKey { get; set; } = "";
        public string GoogleBaseUrl { get; set; } = "https://speech.googleapis.com/v1/speech:recognize";
        public string GoogleModel { get; set; } = "default";

        // AssemblyAI
        public string AssemblyAIApiKey { get; set; } = "";
        public string AssemblyAIBaseUrl { get; set; } = "https://api.assemblyai.com";
        public string AssemblyAIStreamingUrl { get; set; } = "wss://streaming.assemblyai.com/v3/ws";
        public string AssemblyAIModel { get; set; } = "best";

        // Azure
        public string AzureApiKey { get; set; } = "";
        public string AzureBaseUrl { get; set; } = "eastus";
        public string AzureModel { get; set; } = "";

        // TTS
        public string DeepgramTtsApiKey { get; set; } = "";
        public string DeepgramTtsBaseUrl { get; set; } = "https://api.deepgram.com/v1/speak";
        public string DeepgramTtsModel { get; set; } = "aura-2-thalia-en";

        public string SpeechmaticsTtsApiKey { get; set; } = "";
        public string SpeechmaticsTtsBaseUrl { get; set; } = "https://preview.tts.speechmatics.com/generate";
        public string SpeechmaticsTtsVoice { get; set; } = "sarah";

        public string SonioxTtsApiKey { get; set; } = "";
        public string SonioxTtsBaseUrl { get; set; } = "https://tts-rt.soniox.com/tts";
        public string SonioxTtsModel { get; set; } = "tts-rt-v1";
        public string SonioxTtsVoice { get; set; } = "Adrian";

        public string OpenAITtsApiKey { get; set; } = "";
        public string OpenAITtsBaseUrl { get; set; } = "https://api.openai.com/v1/audio/speech";
        public string OpenAITtsModel { get; set; } = "gpt-4o-mini-tts";
        public string OpenAITtsVoice { get; set; } = "alloy";

        public string ElevenLabsTtsApiKey { get; set; } = "";
        public string ElevenLabsTtsBaseUrl { get; set; } = "https://api.elevenlabs.io/v1/text-to-speech";
        public string ElevenLabsTtsModel { get; set; } = "eleven_flash_v2_5";
        public string ElevenLabsTtsVoice { get; set; } = "21m00Tcm4TlvDq8ikWAM";

        public string GoogleTtsApiKey { get; set; } = "";
        public string GoogleTtsBaseUrl { get; set; } = "https://texttospeech.googleapis.com/v1/text:synthesize";
        public string GoogleTtsVoice { get; set; } = "vi-VN-Standard-A";

        public string AzureTtsApiKey { get; set; } = "";
        public string AzureTtsRegion { get; set; } = "eastus";
        public string AzureTtsVoice { get; set; } = "vi-VN-HoaiMyNeural";

        // Noise Suppression
        public bool EnableNoiseSuppression { get; set; } = false;
    }

    public class SettingsService
    {
        private readonly string _settingsFilePath;
        private const string AppName = "BF-STT";
        private readonly ILogger<SettingsService> _logger;

        public AppSettings CurrentSettings { get; private set; } = new AppSettings();

        public SettingsService(ILogger<SettingsService>? logger = null)
        {
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsService>.Instance;
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
                    DecryptApiKeys(CurrentSettings);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Settings file is corrupted or unreadable, resetting to defaults");
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
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to read fallback appsettings.json"); }
            }

            // Guard against null/empty API selections (can happen from JSON deserialization)
            if (string.IsNullOrEmpty(CurrentSettings.BatchModeApi))
            {
                CurrentSettings.BatchModeApi = "Deepgram";
                needsFix = true;
            }
            if (string.IsNullOrEmpty(CurrentSettings.StreamingModeApi))
            {
                CurrentSettings.StreamingModeApi = "Deepgram";
                needsFix = true;
            }
            if (string.IsNullOrEmpty(CurrentSettings.SelectedTtsProvider))
            {
                CurrentSettings.SelectedTtsProvider = "OpenAI";
                needsFix = true;
            }
            if (CurrentSettings.TtsHotkeyVirtualKeyCode == 0)
            {
                CurrentSettings.TtsHotkeyVirtualKeyCode = 0x71;
                needsFix = true;
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

            // Migrate ElevenLabs TTS defaults to a Vietnamese-capable model and a softer female voice.
            // Only update when the user is still on the old built-in defaults.
            if (CurrentSettings.ElevenLabsTtsModel == "eleven_multilingual_v2"
                && CurrentSettings.ElevenLabsTtsVoice == "JBFqnCBsd6RMkjVDRZzb")
            {
                CurrentSettings.ElevenLabsTtsModel = "eleven_flash_v2_5";
                CurrentSettings.ElevenLabsTtsVoice = "21m00Tcm4TlvDq8ikWAM";
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
            var copy = JsonSerializer.Deserialize<AppSettings>(
                JsonSerializer.Serialize(CurrentSettings))!;
            EncryptApiKeys(copy);
            var json = JsonSerializer.Serialize(copy, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
            SetStartWithWindows(CurrentSettings.StartWithWindows);
        }

        private static void EncryptApiKeys(AppSettings s)
        {
            s.ApiKey = SecureSettingsSerializer.Encrypt(s.ApiKey);
            s.SpeechmaticsApiKey = SecureSettingsSerializer.Encrypt(s.SpeechmaticsApiKey);
            s.SonioxApiKey = SecureSettingsSerializer.Encrypt(s.SonioxApiKey);
            s.OpenAIApiKey = SecureSettingsSerializer.Encrypt(s.OpenAIApiKey);
            s.ElevenLabsApiKey = SecureSettingsSerializer.Encrypt(s.ElevenLabsApiKey);
            s.GoogleApiKey = SecureSettingsSerializer.Encrypt(s.GoogleApiKey);
            s.AssemblyAIApiKey = SecureSettingsSerializer.Encrypt(s.AssemblyAIApiKey);
            s.AzureApiKey = SecureSettingsSerializer.Encrypt(s.AzureApiKey);
            s.DeepgramTtsApiKey = SecureSettingsSerializer.Encrypt(s.DeepgramTtsApiKey);
            s.SpeechmaticsTtsApiKey = SecureSettingsSerializer.Encrypt(s.SpeechmaticsTtsApiKey);
            s.SonioxTtsApiKey = SecureSettingsSerializer.Encrypt(s.SonioxTtsApiKey);
            s.OpenAITtsApiKey = SecureSettingsSerializer.Encrypt(s.OpenAITtsApiKey);
            s.ElevenLabsTtsApiKey = SecureSettingsSerializer.Encrypt(s.ElevenLabsTtsApiKey);
            s.GoogleTtsApiKey = SecureSettingsSerializer.Encrypt(s.GoogleTtsApiKey);
            s.AzureTtsApiKey = SecureSettingsSerializer.Encrypt(s.AzureTtsApiKey);
        }

        private static void DecryptApiKeys(AppSettings s)
        {
            s.ApiKey = SecureSettingsSerializer.Decrypt(s.ApiKey);
            s.SpeechmaticsApiKey = SecureSettingsSerializer.Decrypt(s.SpeechmaticsApiKey);
            s.SonioxApiKey = SecureSettingsSerializer.Decrypt(s.SonioxApiKey);
            s.OpenAIApiKey = SecureSettingsSerializer.Decrypt(s.OpenAIApiKey);
            s.ElevenLabsApiKey = SecureSettingsSerializer.Decrypt(s.ElevenLabsApiKey);
            s.GoogleApiKey = SecureSettingsSerializer.Decrypt(s.GoogleApiKey);
            s.AssemblyAIApiKey = SecureSettingsSerializer.Decrypt(s.AssemblyAIApiKey);
            s.AzureApiKey = SecureSettingsSerializer.Decrypt(s.AzureApiKey);
            s.DeepgramTtsApiKey = SecureSettingsSerializer.Decrypt(s.DeepgramTtsApiKey);
            s.SpeechmaticsTtsApiKey = SecureSettingsSerializer.Decrypt(s.SpeechmaticsTtsApiKey);
            s.SonioxTtsApiKey = SecureSettingsSerializer.Decrypt(s.SonioxTtsApiKey);
            s.OpenAITtsApiKey = SecureSettingsSerializer.Decrypt(s.OpenAITtsApiKey);
            s.ElevenLabsTtsApiKey = SecureSettingsSerializer.Decrypt(s.ElevenLabsTtsApiKey);
            s.GoogleTtsApiKey = SecureSettingsSerializer.Decrypt(s.GoogleTtsApiKey);
            s.AzureTtsApiKey = SecureSettingsSerializer.Decrypt(s.AzureTtsApiKey);
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
