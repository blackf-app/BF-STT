using BF_STT.Services.Infrastructure;

namespace BF_STT.Services.Platform
{
    /// <summary>
    /// Cross-platform global hotkey service. Dispatches to the appropriate
    /// per-OS implementation under the hood.
    /// </summary>
    public class HotkeyService : IDisposable
    {
        private readonly IHotkeyListener _listener;

        public HotkeyService(SettingsService settingsService,
            Action onKeyDown, Action onKeyUp,
            Action onStopAndSendKeyDown, Action onStopAndSendKeyUp)
        {
            if (OperatingSystem.IsWindows())
            {
                _listener = new WindowsHotkeyListener(settingsService,
                    onKeyDown, onKeyUp, onStopAndSendKeyDown, onStopAndSendKeyUp);
            }
            else if (OperatingSystem.IsMacOS())
            {
                _listener = new MacHotkeyListener(settingsService,
                    onKeyDown, onKeyUp, onStopAndSendKeyDown, onStopAndSendKeyUp);
            }
            else
            {
                throw new PlatformNotSupportedException(
                    "BF-STT only supports Windows and macOS.");
            }
        }

        public void Dispose() => _listener.Dispose();
    }
}
