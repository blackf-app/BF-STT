using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using WpfClipboard = System.Windows.Clipboard;
using WpfDataObject = System.Windows.DataObject;
using WpfIDataObject = System.Windows.IDataObject;

namespace BF_STT.Services
{
    public class InputInjector : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        private IntPtr _lastExternalWindowHandle;
        private readonly DispatcherTimer _timer;
        private readonly int _myProcessId;

        public InputInjector()
        {
            _myProcessId = Process.GetCurrentProcess().Id;
            
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _timer.Tick += CheckForegroundWindow;
            _timer.Start();
        }

        private void CheckForegroundWindow(object? sender, EventArgs e)
        {
            var foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero) return;

            GetWindowThreadProcessId(foregroundWindow, out uint processId);

            // If the foreground window is NOT our application, store it
            if (processId != _myProcessId)
            {
                _lastExternalWindowHandle = foregroundWindow;
            }
        }

        /// <summary>
        /// Injects text into the last active external window by simulating Ctrl+V.
        /// Backs up and restores the user's clipboard content to avoid data loss.
        /// </summary>
        public IntPtr LastExternalWindowHandle => _lastExternalWindowHandle;

        /// <summary>
        /// Injects text into the specified window (or last active external window) by simulating Ctrl+V.
        /// Backs up and restores the user's clipboard content to avoid data loss.
        /// </summary>
        public async Task InjectTextAsync(string text, IntPtr? targetWindowHandle = null)
        {
            // Use the explicit target if provided and valid (non-zero), otherwise fallback to the last known external window
            var handleToUse = (targetWindowHandle.HasValue && targetWindowHandle.Value != IntPtr.Zero) 
                              ? targetWindowHandle.Value 
                              : _lastExternalWindowHandle;

            if (string.IsNullOrEmpty(text) || handleToUse == IntPtr.Zero) return;

            // Backup current clipboard content
            WpfIDataObject? clipboardBackup = null;
            try
            {
                clipboardBackup = BackupClipboard();
            }
            catch { /* If backup fails, continue without restore */ }

            try
            {
                // Activate the target window
                SetForegroundWindow(handleToUse);

                // Give it a moment to gain focus (non-blocking)
                await Task.Delay(100);

                // Set our text and paste
                WpfClipboard.SetText(text);
                System.Windows.Forms.SendKeys.SendWait("^v");

                // Small delay to let the paste complete before restoring clipboard
                await Task.Delay(50);
            }
            finally
            {
                // Restore original clipboard content
                try
                {
                    RestoreClipboard(clipboardBackup);
                }
                catch { /* Ignore restore errors to avoid crashing */ }
            }
        }

        /// <summary>
        /// Backs up the current clipboard content, preserving all formats.
        /// </summary>
        private WpfIDataObject? BackupClipboard()
        {
            if (!WpfClipboard.ContainsText() &&
                !WpfClipboard.ContainsImage() &&
                !WpfClipboard.ContainsFileDropList() &&
                !WpfClipboard.ContainsAudio())
            {
                return null; // Clipboard is empty or contains unsupported format
            }

            var backup = new WpfDataObject();

            if (WpfClipboard.ContainsText())
            {
                backup.SetText(WpfClipboard.GetText());
            }
            if (WpfClipboard.ContainsImage())
            {
                var image = WpfClipboard.GetImage();
                if (image != null) backup.SetImage(image);
            }
            if (WpfClipboard.ContainsFileDropList())
            {
                var files = WpfClipboard.GetFileDropList();
                if (files != null) backup.SetFileDropList(files);
            }
            if (WpfClipboard.ContainsAudio())
            {
                var audio = WpfClipboard.GetAudioStream();
                if (audio != null) backup.SetAudio(audio);
            }

            return backup;
        }

        /// <summary>
        /// Restores previously backed-up clipboard content.
        /// </summary>
        private void RestoreClipboard(WpfIDataObject? backup)
        {
            if (backup == null)
            {
                // Original clipboard was empty, clear it
                WpfClipboard.Clear();
                return;
            }

            WpfClipboard.SetDataObject(backup, true); // true = persist after app exits
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Tick -= CheckForegroundWindow;
        }
    }
}
