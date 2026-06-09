namespace BF_STT.Services.Platform
{
    /// <summary>
    /// Platform abstraction for simulating keyboard input and managing window focus.
    /// </summary>
    public interface IInputSimulator
    {
        /// <summary>
        /// Simulates the OS-native paste shortcut (Ctrl+V on Windows, Cmd+V on macOS).
        /// </summary>
        void SimulatePaste();

        /// <summary>
        /// Simulates pressing Backspace N times.
        /// </summary>
        void SimulateBackspace(int count);

        /// <summary>
        /// Simulates pressing the Enter / Return key.
        /// </summary>
        void SimulateEnter();

        /// <summary>
        /// Returns an opaque handle (or identifier) for the currently focused window.
        /// </summary>
        IntPtr GetForegroundWindow();

        /// <summary>
        /// Brings the given window handle to the foreground if possible.
        /// </summary>
        void SetForegroundWindow(IntPtr handle);

        /// <summary>
        /// Returns the process id that owns the given window handle, or 0 when unknown.
        /// </summary>
        uint GetWindowProcessId(IntPtr handle);

        /// <summary>
        /// Ensures the target window is focused. May be a no-op on platforms that
        /// don't support cross-process focus changes.
        /// </summary>
        void EnsureWindowFocused(IntPtr handle);
    }
}
