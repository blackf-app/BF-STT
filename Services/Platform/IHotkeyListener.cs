namespace BF_STT.Services.Platform
{
    /// <summary>
    /// Platform abstraction for global system-wide hotkey capture.
    /// </summary>
    public interface IHotkeyListener : IDisposable
    {
    }

    /// <summary>
    /// Platform-neutral key codes used by HotkeyService to identify which key was pressed.
    /// On Windows these map to Virtual Key codes; on macOS they map to internal kVK_* values.
    /// </summary>
    public static class HotkeyCodes
    {
        // Common F-keys
        public const int F1 = 0x70;
        public const int F2 = 0x71;
        public const int F3 = 0x72;
        public const int F4 = 0x73;
        public const int F5 = 0x74;
        public const int F6 = 0x75;
        public const int F7 = 0x76;
        public const int F8 = 0x77;
        public const int F9 = 0x78;
        public const int F10 = 0x79;
        public const int F11 = 0x7A;
        public const int F12 = 0x7B;
        public const int Backtick = 0xC0;
    }
}
