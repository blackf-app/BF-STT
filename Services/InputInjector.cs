using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace BF_STT.Services
{
    public class InputInjector
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
        private int _myProcessId;

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

        public void InjectText(string text)
        {
            if (string.IsNullOrEmpty(text) || _lastExternalWindowHandle == IntPtr.Zero) return;

            // Activate the target window
            SetForegroundWindow(_lastExternalWindowHandle);
            
            // Give it a moment to gain focus
            Thread.Sleep(100);

            // Send keys
            // Using SendKeys from System.Windows.Forms
            System.Windows.Forms.SendKeys.SendWait(text);
        }
    }
}
