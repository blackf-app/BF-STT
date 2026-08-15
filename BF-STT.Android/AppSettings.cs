using Android.Content;

namespace BFSTT.Droid
{
    /// <summary>
    /// Lightweight SharedPreferences-backed settings store.
    /// API keys are stored per-provider so switching providers keeps each key.
    /// </summary>
    public static class AppSettings
    {
        private static ISharedPreferences? _prefs;

        public static readonly string[] Providers = { "Deepgram", "OpenAI", "ElevenLabs" };

        public static void Init(Context ctx)
        {
            _prefs ??= ctx.ApplicationContext!.GetSharedPreferences("bfstt_prefs", FileCreationMode.Private);
        }

        public static string Provider
        {
            get => Get("provider", "Deepgram");
            set => Set("provider", value);
        }

        /// <summary>Speech-recognition language code passed to the STT provider
        /// ("vi", "en", …), or "auto" for automatic detection.</summary>
        public static string Language
        {
            get => Get("language", "vi");
            set => Set("language", value);
        }

        /// <summary>App interface (display) language code — one of L.Langs.</summary>
        public static string UiLang
        {
            get => Get("ui_lang", "vi");
            set => Set("ui_lang", value);
        }

        public static bool AutoPaste
        {
            get => GetBool("auto_paste", true);
            set => SetBool("auto_paste", value);
        }

        /// <summary>
        /// Whether the user has turned the floating bubble on. Persisted so the service
        /// can be auto-restarted on boot / after being killed without re-opening the app.
        /// </summary>
        public static bool BubbleEnabled
        {
            get => GetBool("bubble_enabled", false);
            set => SetBool("bubble_enabled", value);
        }

        public static string ApiKeyFor(string provider) => Get($"key_{provider}", "");

        public static void SetApiKey(string provider, string key) => Set($"key_{provider}", key ?? "");

        public static string CurrentApiKey => ApiKeyFor(Provider);

        private static string Get(string key, string def)
        {
            return _prefs?.GetString(key, def) ?? def;
        }

        private static void Set(string key, string value)
        {
            var editor = _prefs?.Edit();
            editor?.PutString(key, value);
            editor?.Apply();
        }

        private static bool GetBool(string key, bool def) => _prefs?.GetBoolean(key, def) ?? def;

        private static void SetBool(string key, bool value)
        {
            var editor = _prefs?.Edit();
            editor?.PutBoolean(key, value);
            editor?.Apply();
        }
    }
}
