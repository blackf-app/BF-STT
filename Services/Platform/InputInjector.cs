using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace BF_STT.Services.Platform
{
    public class InputInjector : IDisposable
    {
        private readonly IInputSimulator _input;
        private IntPtr _lastExternalWindowHandle;
        private readonly DispatcherTimer _timer;
        private readonly int _myProcessId;
        private readonly ILogger<InputInjector> _logger;

        private string _lastInjectedText = string.Empty;
        private string _committedText = string.Empty;
        private readonly SemaphoreSlim _injectSemaphore = new(1, 1);

        public InputInjector(IInputSimulator input, ILogger<InputInjector>? logger = null)
        {
            _input = input;
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<InputInjector>.Instance;
            _myProcessId = Process.GetCurrentProcess().Id;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _timer.Tick += CheckForegroundWindow;
            _timer.Start();
        }

        private void CheckForegroundWindow(object? sender, EventArgs e)
        {
            var foregroundWindow = _input.GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero) return;

            uint processId = _input.GetWindowProcessId(foregroundWindow);

            // On macOS, GetWindowProcessId returns 0 and the handle is a sentinel —
            // we just always treat it as external (which is the safe default).
            if (processId == 0 || processId != _myProcessId)
            {
                _lastExternalWindowHandle = foregroundWindow;
            }
        }

        public IntPtr LastExternalWindowHandle => _lastExternalWindowHandle;

        public async Task InjectTextAsync(string text, IntPtr? targetWindowHandle = null, bool autoSend = false)
        {
            var handleToUse = (targetWindowHandle.HasValue && targetWindowHandle.Value != IntPtr.Zero)
                              ? targetWindowHandle.Value
                              : _lastExternalWindowHandle;

            if (string.IsNullOrEmpty(text) || handleToUse == IntPtr.Zero) return;

            var currentForeground = _input.GetForegroundWindow();
            bool shouldRestoreFocus = currentForeground != IntPtr.Zero && currentForeground != handleToUse;

            string? clipboardBackup = null;
            try { clipboardBackup = await ClipboardHelper.BackupAsync(); }
            catch (Exception ex) { _logger.LogWarning(ex, "Clipboard backup failed, text injection will proceed without restore"); }

            try
            {
                _input.EnsureWindowFocused(handleToUse);

                await Task.Delay(30);

                await ClipboardHelper.SetTextAsync(text);
                _input.SimulatePaste();

                // 150ms: give the target app enough time to process Cmd+V and read
                // the clipboard before RestoreAsync replaces it. 30ms was too short
                // on busy systems, causing the app to paste the old clipboard content.
                await Task.Delay(150);

                if (autoSend)
                {
                    _input.SimulateEnter();
                    await Task.Delay(30);
                }
            }
            finally
            {
                if (shouldRestoreFocus)
                {
                    _input.EnsureWindowFocused(currentForeground);
                }

                try { await ClipboardHelper.RestoreAsync(clipboardBackup); }
                catch (Exception ex) { _logger.LogWarning(ex, "Clipboard restore failed"); }
            }
        }

        public void ResetStreamingState()
        {
            _lastInjectedText = string.Empty;
            _committedText = string.Empty;
        }

        public void CommitCurrentText()
        {
            _committedText = _lastInjectedText;
        }

        public async Task InjectStreamingTextAsync(string currentSegmentText, bool isFinal, IntPtr? targetWindowHandle = null)
        {
            await _injectSemaphore.WaitAsync();
            try
            {
                var handleToUse = (targetWindowHandle.HasValue && targetWindowHandle.Value != IntPtr.Zero)
                                  ? targetWindowHandle.Value
                                  : _lastExternalWindowHandle;

                if (handleToUse == IntPtr.Zero) return;

                var currentForeground = _input.GetForegroundWindow();
                bool shouldRestoreFocus = currentForeground != IntPtr.Zero && currentForeground != handleToUse;

                var fullText = _committedText + currentSegmentText;
                var previousText = _lastInjectedText;

                if (fullText == previousText)
                {
                    if (isFinal && !string.IsNullOrEmpty(currentSegmentText))
                        _committedText += currentSegmentText;
                    return;
                }

                int commonLength = 0;
                int minLength = Math.Min(previousText.Length, fullText.Length);
                for (int i = 0; i < minLength; i++)
                {
                    if (previousText[i] == fullText[i]) commonLength++;
                    else break;
                }

                int charsToDelete = previousText.Length - commonLength;
                string charsToAdd = fullText.Substring(commonLength);

                try
                {
                    _input.EnsureWindowFocused(handleToUse);

                    if (charsToDelete > 0)
                    {
                        _input.SimulateBackspace(charsToDelete);
                        await Task.Delay(5);
                    }

                    if (!string.IsNullOrEmpty(charsToAdd))
                    {
                        await ClipboardHelper.SetTextAsync(charsToAdd);
                        _input.SimulatePaste();
                        await Task.Delay(5);
                    }

                    _lastInjectedText = fullText;

                    if (isFinal && !string.IsNullOrEmpty(currentSegmentText))
                    {
                        _committedText += currentSegmentText;
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
                        _input.EnsureWindowFocused(currentForeground);
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

        public async Task PressEnterAsync(IntPtr? targetWindowHandle = null)
        {
            var handleToUse = (targetWindowHandle.HasValue && targetWindowHandle.Value != IntPtr.Zero)
                              ? targetWindowHandle.Value
                              : _lastExternalWindowHandle;

            if (handleToUse == IntPtr.Zero) return;

            var currentForeground = _input.GetForegroundWindow();
            bool shouldRestoreFocus = currentForeground != IntPtr.Zero && currentForeground != handleToUse;

            try
            {
                _input.EnsureWindowFocused(handleToUse);

                for (int i = 0; i < 5; i++)
                {
                    await Task.Delay(30);
                    if (_input.GetForegroundWindow() == handleToUse) break;
                }

                _input.SimulateEnter();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InputInjector] PressEnter error: {ex.Message}");
            }
            finally
            {
                if (shouldRestoreFocus)
                {
                    _input.EnsureWindowFocused(currentForeground);
                }
            }
        }
    }
}
