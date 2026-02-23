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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        #region SendInput Structs

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public INPUTUNION U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi; // Must be present — largest union member defines struct size
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        // Required in the union to ensure correct struct size (32 bytes on x64).
        // Without this, Marshal.SizeOf<INPUT>() returns 32 instead of 40,
        // causing SendInput to silently fail.
        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const ushort VK_CONTROL = 0x11;
        private const ushort VK_RETURN = 0x0D;
        private const ushort VK_BACK = 0x08;
        private const ushort VK_V = 0x56;

        #endregion

        private IntPtr _lastExternalWindowHandle;
        private readonly DispatcherTimer _timer;
        private readonly int _myProcessId;

        // Streaming injection state
        private string _lastInjectedText = string.Empty;
        private string _committedText = string.Empty; // Text from all finalized segments
        private readonly SemaphoreSlim _injectSemaphore = new(1, 1);

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
        public async Task InjectTextAsync(string text, IntPtr? targetWindowHandle = null, bool autoSend = false)
        {
            // Use the explicit target if provided and valid (non-zero), otherwise fallback to the last known external window
            var handleToUse = (targetWindowHandle.HasValue && targetWindowHandle.Value != IntPtr.Zero) 
                              ? targetWindowHandle.Value 
                              : _lastExternalWindowHandle;

            if (string.IsNullOrEmpty(text) || handleToUse == IntPtr.Zero) return;

            var currentForeground = GetForegroundWindow();
            bool shouldRestoreFocus = currentForeground != IntPtr.Zero && currentForeground != handleToUse;

            // Backup current clipboard content
            WpfIDataObject? clipboardBackup = null;
            try
            {
                clipboardBackup = BackupClipboard();
            }
            catch { /* If backup fails, continue without restore */ }

            uint currentThreadId = GetCurrentThreadId();
            uint targetThreadId = GetWindowThreadProcessId(handleToUse, out uint _);
            bool attached = false;

            try
            {
                if (currentThreadId != targetThreadId)
                {
                    attached = AttachThreadInput(currentThreadId, targetThreadId, true);
                }

                // Activate the target window
                // With AttachThreadInput, SetForegroundWindow is much more reliable
                SetForegroundWindow(handleToUse);

                // Give it a moment to gain focus (non-blocking)
                await Task.Delay(30);

                // Set our text and paste via SendInput (faster than SendKeys)
                WpfClipboard.SetText(text);
                SimulateCtrlV();

                // Short delay — SendInput is near-instant
                await Task.Delay(30);

                if (autoSend)
                {
                    // Small delay to allow target app (like Zalo/Electron) to process the paste
                    await Task.Delay(50);
                    SimulateEnter();
                    await Task.Delay(30);
                }
            }
            finally
            {
                if (attached)
                {
                    AttachThreadInput(currentThreadId, targetThreadId, false);
                }

                if (shouldRestoreFocus)
                {
                    // Ensure the focus is restored back if necessary
                    EnsureWindowFocused(currentForeground);
                }

                // Restore original clipboard content
                try
                {
                    RestoreClipboard(clipboardBackup);
                }
                catch { /* Ignore restore errors to avoid crashing */ }
            }
        }

        /// <summary>
        /// Resets streaming injection state. Call at the start of a new streaming session.
        /// </summary>
        public void ResetStreamingState()
        {
            _lastInjectedText = string.Empty;
            _committedText = string.Empty;
        }

        /// <summary>
        /// Commits all currently displayed text so it won't be erased.
        /// Call this when streaming session ends.
        /// </summary>
        public void CommitCurrentText()
        {
            _committedText = _lastInjectedText;
        }

        /// <summary>
        /// Injects text incrementally for streaming mode.
        /// Computes delta from previous injection and sends only the difference.
        /// Uses clipboard paste for each delta chunk to support Unicode/Vietnamese.
        /// </summary>
        public async Task InjectStreamingTextAsync(string currentSegmentText, bool isFinal, IntPtr? targetWindowHandle = null)
        {
            await _injectSemaphore.WaitAsync();
            try
            {
                var handleToUse = (targetWindowHandle.HasValue && targetWindowHandle.Value != IntPtr.Zero)
                                  ? targetWindowHandle.Value
                                  : _lastExternalWindowHandle;

                if (handleToUse == IntPtr.Zero) return;

                var currentForeground = GetForegroundWindow();
                bool shouldRestoreFocus = currentForeground != IntPtr.Zero && currentForeground != handleToUse;

                // Build the full text: committed segments + current (interim or final) segment
                var fullText = _committedText + currentSegmentText;

                // Compute what we need to change
                var previousText = _lastInjectedText;

                if (fullText == previousText)
                {
                    // Even if no change in text, if this is final, we must commit the state
                    if (isFinal)
                    {
                        if (!string.IsNullOrEmpty(currentSegmentText))
                            _committedText += currentSegmentText;
                        
                        // If currentSegmentText is empty (silence), we don't add to committedText,
                        // but effectively the "current" committed text is already correct/up-to-date.
                    }
                    return; 
                }

                // Find the common prefix length
                int commonLength = 0;
                int minLength = Math.Min(previousText.Length, fullText.Length);
                for (int i = 0; i < minLength; i++)
                {
                    if (previousText[i] == fullText[i])
                        commonLength++;
                    else
                        break;
                }

                // Number of characters to delete (from previous text that are no longer valid)
                int charsToDelete = previousText.Length - commonLength;
                // New characters to add
                string charsToAdd = fullText.Substring(commonLength);

                try
                {
                    // Ensure target window is focused
                    EnsureWindowFocused(handleToUse);

                    // Delete characters that need to be replaced via SendInput
                    if (charsToDelete > 0)
                    {
                        SimulateBackspace(charsToDelete);
                        await Task.Delay(5);
                    }

                    // Insert new characters via clipboard + SendInput (for Unicode/Vietnamese support)
                    if (!string.IsNullOrEmpty(charsToAdd))
                    {
                        WpfClipboard.SetText(charsToAdd);
                        SimulateCtrlV();
                        await Task.Delay(5);
                    }

                    _lastInjectedText = fullText;

                    // If this segment is final, commit it
                    if (isFinal)
                    {
                        if(!string.IsNullOrEmpty(currentSegmentText))
                        {
                            _committedText += currentSegmentText;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[InputInjector] Streaming inject error: {ex.Message}");
                }
                finally
                {
                    if (shouldRestoreFocus)
                    {
                        EnsureWindowFocused(currentForeground);
                    }
                }
            }
            finally
            {
                _injectSemaphore.Release();
            }
        }

        /// <summary>
        /// Ensures the target window is in the foreground without blocking.
        /// </summary>
        private void EnsureWindowFocused(IntPtr handle)
        {
            var foreground = GetForegroundWindow();
            if (foreground != handle)
            {
                uint currentThreadId = GetCurrentThreadId();
                uint targetThreadId = GetWindowThreadProcessId(handle, out uint _);
                
                bool attached = false;
                if (currentThreadId != targetThreadId)
                {
                    attached = AttachThreadInput(currentThreadId, targetThreadId, true);
                }

                SetForegroundWindow(handle);

                if (attached)
                {
                    AttachThreadInput(currentThreadId, targetThreadId, false);
                }
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

        #region SendInput Helpers

        /// <summary>
        /// Simulates Ctrl+V keystroke using SendInput API (much faster than SendKeys.SendWait).
        /// </summary>
        private void SimulateCtrlV()
        {
            var inputs = new INPUT[4];

            // Ctrl down
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = VK_CONTROL;

            // V down
            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = VK_V;

            // V up
            inputs[2].type = INPUT_KEYBOARD;
            inputs[2].U.ki.wVk = VK_V;
            inputs[2].U.ki.dwFlags = KEYEVENTF_KEYUP;

            // Ctrl up
            inputs[3].type = INPUT_KEYBOARD;
            inputs[3].U.ki.wVk = VK_CONTROL;
            inputs[3].U.ki.dwFlags = KEYEVENTF_KEYUP;

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }

        /// <summary>
        /// Simulates multiple Backspace key presses using SendInput API.
        /// </summary>
        private void SimulateBackspace(int count)
        {
            if (count <= 0) return;

            var inputs = new INPUT[count * 2]; // Each key needs down + up
            for (int i = 0; i < count; i++)
            {
                // Key down
                inputs[i * 2].type = INPUT_KEYBOARD;
                inputs[i * 2].U.ki.wVk = VK_BACK;

                // Key up
                inputs[i * 2 + 1].type = INPUT_KEYBOARD;
                inputs[i * 2 + 1].U.ki.wVk = VK_BACK;
                inputs[i * 2 + 1].U.ki.dwFlags = KEYEVENTF_KEYUP;
            }

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }

        /// <summary>
        /// Simulates an Enter key press using SendInput API.
        /// </summary>
        private void SimulateEnter()
        {
            var inputs = new INPUT[2];

            // Enter down
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = VK_RETURN;

            // Enter up
            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = VK_RETURN;
            inputs[1].U.ki.dwFlags = KEYEVENTF_KEYUP;

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }

        #endregion

        public void Dispose()
        {
            _timer.Stop();
            _timer.Tick -= CheckForegroundWindow;
        }
        /// <summary>
        /// Simulates an Enter key press in the target window.
        /// Uses AttachThreadInput for reliable focus switching.
        /// </summary>
        public async Task PressEnterAsync(IntPtr? targetWindowHandle = null)
        {
            var handleToUse = (targetWindowHandle.HasValue && targetWindowHandle.Value != IntPtr.Zero)
                              ? targetWindowHandle.Value
                              : _lastExternalWindowHandle;

            if (handleToUse == IntPtr.Zero) return;

            var currentForeground = GetForegroundWindow();
            bool shouldRestoreFocus = currentForeground != IntPtr.Zero && currentForeground != handleToUse;

            uint currentThreadId = GetCurrentThreadId();
            uint targetThreadId = GetWindowThreadProcessId(handleToUse, out uint _);
            bool attached = false;

            try
            {
                if (currentThreadId != targetThreadId)
                {
                    attached = AttachThreadInput(currentThreadId, targetThreadId, true);
                }

                SetForegroundWindow(handleToUse);

                // Wait and verify focus actually switched (up to 150ms)
                for (int i = 0; i < 5; i++)
                {
                    await Task.Delay(30);
                    if (GetForegroundWindow() == handleToUse) break;
                }

                SimulateEnter();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InputInjector] PressEnter error: {ex.Message}");
            }
            finally
            {
                if (attached)
                {
                    AttachThreadInput(currentThreadId, targetThreadId, false);
                }

                if (shouldRestoreFocus)
                {
                    EnsureWindowFocused(currentForeground);
                }
            }
        }
    }
}
