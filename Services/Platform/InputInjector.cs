using System.Diagnostics;
using System.Windows.Threading;
using WpfClipboard = System.Windows.Clipboard;
using WpfIDataObject = System.Windows.IDataObject;

namespace BF_STT.Services.Platform
{
    public class InputInjector : IDisposable
    {
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
            var foregroundWindow = Win32InputSimulator.GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero) return;

            Win32InputSimulator.GetWindowThreadProcessId(foregroundWindow, out uint processId);

            // If the foreground window is NOT our application, store it
            if (processId != _myProcessId)
            {
                _lastExternalWindowHandle = foregroundWindow;
            }
        }

        /// <summary>
        /// The last known window handle that is NOT this application.
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

            var currentForeground = Win32InputSimulator.GetForegroundWindow();
            bool shouldRestoreFocus = currentForeground != IntPtr.Zero && currentForeground != handleToUse;

            // Backup current clipboard content
            WpfIDataObject? clipboardBackup = null;
            try
            {
                clipboardBackup = ClipboardHelper.Backup();
            }
            catch { /* If backup fails, continue without restore */ }

            uint currentThreadId = Win32InputSimulator.GetCurrentThreadId();
            uint targetThreadId = Win32InputSimulator.GetWindowThreadProcessId(handleToUse, out uint _);
            bool attached = false;

            try
            {
                if (currentThreadId != targetThreadId)
                {
                    attached = Win32InputSimulator.AttachThreadInput(currentThreadId, targetThreadId, true);
                }

                // Activate the target window
                // With AttachThreadInput, SetForegroundWindow is much more reliable
                Win32InputSimulator.SetForegroundWindow(handleToUse);

                // Give it a moment to gain focus (non-blocking)
                await Task.Delay(30);

                // Set our text and paste via SendInput (faster than SendKeys)
                WpfClipboard.SetText(text);
                Win32InputSimulator.SimulateCtrlV();

                // Short delay — SendInput is near-instant
                await Task.Delay(30);

                if (autoSend)
                {
                    // Small delay to allow target app (like Zalo/Electron) to process the paste
                    await Task.Delay(50);
                    Win32InputSimulator.SimulateEnter();
                    await Task.Delay(30);
                }
            }
            finally
            {
                if (attached)
                {
                    Win32InputSimulator.AttachThreadInput(currentThreadId, targetThreadId, false);
                }

                if (shouldRestoreFocus)
                {
                    // Ensure the focus is restored back if necessary
                    Win32InputSimulator.EnsureWindowFocused(currentForeground);
                }

                // Restore original clipboard content
                try
                {
                    ClipboardHelper.Restore(clipboardBackup);
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

                var currentForeground = Win32InputSimulator.GetForegroundWindow();
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
                    Win32InputSimulator.EnsureWindowFocused(handleToUse);

                    // Delete characters that need to be replaced via SendInput
                    if (charsToDelete > 0)
                    {
                        Win32InputSimulator.SimulateBackspace(charsToDelete);
                        await Task.Delay(5);
                    }

                    // Insert new characters via clipboard + SendInput (for Unicode/Vietnamese support)
                    if (!string.IsNullOrEmpty(charsToAdd))
                    {
                        WpfClipboard.SetText(charsToAdd);
                        Win32InputSimulator.SimulateCtrlV();
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
                        Win32InputSimulator.EnsureWindowFocused(currentForeground);
                    }
                }
            }
            finally
            {
                _injectSemaphore.Release();
            }
        }

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

            var currentForeground = Win32InputSimulator.GetForegroundWindow();
            bool shouldRestoreFocus = currentForeground != IntPtr.Zero && currentForeground != handleToUse;

            uint currentThreadId = Win32InputSimulator.GetCurrentThreadId();
            uint targetThreadId = Win32InputSimulator.GetWindowThreadProcessId(handleToUse, out uint _);
            bool attached = false;

            try
            {
                if (currentThreadId != targetThreadId)
                {
                    attached = Win32InputSimulator.AttachThreadInput(currentThreadId, targetThreadId, true);
                }

                Win32InputSimulator.SetForegroundWindow(handleToUse);

                // Wait and verify focus actually switched (up to 150ms)
                for (int i = 0; i < 5; i++)
                {
                    await Task.Delay(30);
                    if (Win32InputSimulator.GetForegroundWindow() == handleToUse) break;
                }

                Win32InputSimulator.SimulateEnter();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InputInjector] PressEnter error: {ex.Message}");
            }
            finally
            {
                if (attached)
                {
                    Win32InputSimulator.AttachThreadInput(currentThreadId, targetThreadId, false);
                }

                if (shouldRestoreFocus)
                {
                    Win32InputSimulator.EnsureWindowFocused(currentForeground);
                }
            }
        }
    }
}
