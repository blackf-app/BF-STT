using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;

namespace BF_STT.Services.Platform
{
    /// <summary>
    /// Cross-platform clipboard helper using Avalonia's IClipboard.
    /// Backs up the text content of the clipboard before injection and restores
    /// it after. Image/file/audio formats are not preserved on macOS — the
    /// trade-off is acceptable because the injection path only writes text.
    /// </summary>
    internal static class ClipboardHelper
    {
        private static IClipboard? GetClipboard()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow != null)
            {
                return desktop.MainWindow.Clipboard;
            }
            return null;
        }

        /// <summary>
        /// Returns the current clipboard text (or null if none).
        /// </summary>
        public static async Task<string?> BackupAsync()
        {
            var clipboard = GetClipboard();
            if (clipboard == null) return null;
            try
            {
                return await Dispatcher.UIThread.InvokeAsync(async () => await clipboard.GetTextAsync());
            }
            catch { return null; }
        }

        public static async Task SetTextAsync(string text)
        {
            var clipboard = GetClipboard();
            if (clipboard == null) return;
            await Dispatcher.UIThread.InvokeAsync(async () => await clipboard.SetTextAsync(text));
        }

        public static async Task RestoreAsync(string? backup)
        {
            var clipboard = GetClipboard();
            if (clipboard == null) return;
            try
            {
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    if (string.IsNullOrEmpty(backup))
                        await clipboard.ClearAsync();
                    else
                        await clipboard.SetTextAsync(backup);
                });
            }
            catch { /* best-effort restore */ }
        }
    }
}
