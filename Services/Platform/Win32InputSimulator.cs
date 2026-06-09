using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace BF_STT.Services.Platform
{
    /// <summary>
    /// Windows implementation of <see cref="IInputSimulator"/>.
    /// Wraps P/Invoke calls for SendInput, focus management, and thread attachment.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal class Win32InputSimulator : IInputSimulator
    {
        #region P/Invoke Declarations

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindowNative();

        [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
        private static extern IntPtr User32_GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindowNative(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "SetForegroundWindow")]
        private static extern bool User32_SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

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
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

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

        public void SimulatePaste()
        {
            var inputs = new INPUT[4];

            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = VK_CONTROL;

            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = VK_V;

            inputs[2].type = INPUT_KEYBOARD;
            inputs[2].U.ki.wVk = VK_V;
            inputs[2].U.ki.dwFlags = KEYEVENTF_KEYUP;

            inputs[3].type = INPUT_KEYBOARD;
            inputs[3].U.ki.wVk = VK_CONTROL;
            inputs[3].U.ki.dwFlags = KEYEVENTF_KEYUP;

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }

        public void SimulateBackspace(int count)
        {
            if (count <= 0) return;

            var inputs = new INPUT[count * 2];
            for (int i = 0; i < count; i++)
            {
                inputs[i * 2].type = INPUT_KEYBOARD;
                inputs[i * 2].U.ki.wVk = VK_BACK;

                inputs[i * 2 + 1].type = INPUT_KEYBOARD;
                inputs[i * 2 + 1].U.ki.wVk = VK_BACK;
                inputs[i * 2 + 1].U.ki.dwFlags = KEYEVENTF_KEYUP;
            }

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }

        public void SimulateEnter()
        {
            var inputs = new INPUT[2];

            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = VK_RETURN;

            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = VK_RETURN;
            inputs[1].U.ki.dwFlags = KEYEVENTF_KEYUP;

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }

        public IntPtr GetForegroundWindow() => User32_GetForegroundWindow();

        public void SetForegroundWindow(IntPtr handle) => User32_SetForegroundWindow(handle);

        public uint GetWindowProcessId(IntPtr handle)
        {
            GetWindowThreadProcessId(handle, out uint pid);
            return pid;
        }

        public void EnsureWindowFocused(IntPtr handle)
        {
            var foreground = User32_GetForegroundWindow();
            if (foreground != handle)
            {
                uint currentThreadId = GetCurrentThreadId();
                uint targetThreadId = GetWindowThreadProcessId(handle, out _);

                bool attached = false;
                if (currentThreadId != targetThreadId)
                {
                    attached = AttachThreadInput(currentThreadId, targetThreadId, true);
                }

                User32_SetForegroundWindow(handle);

                if (attached)
                {
                    AttachThreadInput(currentThreadId, targetThreadId, false);
                }
            }
        }

        internal void EnsureWindowFocusedWithAttach(IntPtr handle, out uint currentThreadId, out uint targetThreadId, out bool attached)
        {
            currentThreadId = GetCurrentThreadId();
            targetThreadId = GetWindowThreadProcessId(handle, out _);
            attached = false;
            if (currentThreadId != targetThreadId)
            {
                attached = AttachThreadInput(currentThreadId, targetThreadId, true);
            }
            User32_SetForegroundWindow(handle);
        }

        internal void DetachThread(uint currentThreadId, uint targetThreadId)
        {
            AttachThreadInput(currentThreadId, targetThreadId, false);
        }
    }
}
