using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace BF_STT.Services.Platform
{
    /// <summary>
    /// macOS implementation of <see cref="IInputSimulator"/> using Quartz Event Services.
    /// Requires Accessibility permission for the app in System Settings &gt; Privacy &amp; Security.
    /// </summary>
    [SupportedOSPlatform("macos")]
    internal class MacInputSimulator : IInputSimulator
    {
        private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

        [DllImport(CoreGraphics)]
        private static extern IntPtr CGEventCreateKeyboardEvent(IntPtr source, ushort virtualKey, bool keyDown);

        [DllImport(CoreGraphics)]
        private static extern void CGEventPost(uint tap, IntPtr eventRef);

        [DllImport(CoreGraphics)]
        private static extern void CGEventSetFlags(IntPtr eventRef, ulong flags);

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern void CFRelease(IntPtr cf);

        // kCGEventFlagMaskCommand = 1 << 20
        private const ulong kCGEventFlagMaskCommand = 0x100000;
        // kCGHIDEventTap = 0
        private const uint kCGHIDEventTap = 0;

        // macOS virtual key codes (kVK_*)
        private const ushort kVK_ANSI_V = 0x09;
        private const ushort kVK_Delete = 0x33;   // Backspace
        private const ushort kVK_Return = 0x24;

        public void SimulatePaste()
        {
            // V key-down: Command held while V is pressed.
            var down = CGEventCreateKeyboardEvent(IntPtr.Zero, kVK_ANSI_V, true);
            CGEventSetFlags(down, kCGEventFlagMaskCommand);
            CGEventPost(kCGHIDEventTap, down);
            CFRelease(down);

            // V key-up: Command flag cleared — signals Command is fully released.
            // Leaving kCGEventFlagMaskCommand on the key-up causes the system to
            // report Command as still held after the paste, which makes a subsequent
            // SimulateEnter() look like Cmd+Return to receiving apps.
            var up = CGEventCreateKeyboardEvent(IntPtr.Zero, kVK_ANSI_V, false);
            CGEventSetFlags(up, 0);
            CGEventPost(kCGHIDEventTap, up);
            CFRelease(up);
        }

        public void SimulateBackspace(int count)
        {
            if (count <= 0) return;
            for (int i = 0; i < count; i++)
            {
                var down = CGEventCreateKeyboardEvent(IntPtr.Zero, kVK_Delete, true);
                CGEventPost(kCGHIDEventTap, down);
                CFRelease(down);

                var up = CGEventCreateKeyboardEvent(IntPtr.Zero, kVK_Delete, false);
                CGEventPost(kCGHIDEventTap, up);
                CFRelease(up);
            }
        }

        public void SimulateEnter()
        {
            var down = CGEventCreateKeyboardEvent(IntPtr.Zero, kVK_Return, true);
            CGEventPost(kCGHIDEventTap, down);
            CFRelease(down);

            var up = CGEventCreateKeyboardEvent(IntPtr.Zero, kVK_Return, false);
            CGEventPost(kCGHIDEventTap, up);
            CFRelease(up);
        }

        // macOS doesn't expose a direct "foreground window HWND". We treat the
        // currently-active application as the focus target. Cross-process focus
        // injection on macOS works at the application level — keystrokes flow to
        // whichever app is frontmost, which is exactly what we want when the user
        // presses the global hotkey while another app is focused.
        //
        // We return a sentinel non-zero value so the existing InputInjector logic
        // (which checks for IntPtr.Zero) still works.
        private static readonly IntPtr FocusSentinel = new IntPtr(1);

        public IntPtr GetForegroundWindow() => FocusSentinel;

        public void SetForegroundWindow(IntPtr handle)
        {
            // No-op: macOS keeps the previously-focused app active when our
            // overlay window does not steal focus. CGEventPost will deliver
            // keystrokes to the frontmost app automatically.
        }

        public uint GetWindowProcessId(IntPtr handle) => 0;

        public void EnsureWindowFocused(IntPtr handle)
        {
            // No-op on macOS. See comment on GetForegroundWindow.
        }
    }
}
