using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BF_STT.Services.Platform
{
    /// <summary>
    /// Provides low-level Win32 input simulation and window management.
    /// Wraps P/Invoke calls for SendInput, focus management, and thread attachment.
    /// </summary>
    internal static class Win32InputSimulator
    {
        #region P/Invoke Declarations

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        #endregion

        #region Structs & Constants

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

        #region Public Methods

        /// <summary>
        /// Simulates Ctrl+V keystroke using SendInput API (much faster than SendKeys.SendWait).
        /// </summary>
        public static void SimulateCtrlV()
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
        public static void SimulateBackspace(int count)
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
        public static void SimulateEnter()
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

        /// <summary>
        /// Ensures the target window is in the foreground without blocking.
        /// Uses AttachThreadInput for reliable focus switching.
        /// </summary>
        public static void EnsureWindowFocused(IntPtr handle)
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

        #endregion
    }
}
