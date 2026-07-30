using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace BF_STT
{
    /// <summary>
    /// Shared "drag the window by its background" logic for the frameless windows.
    /// </summary>
    internal static class WindowDragHelper
    {
        /// <summary>
        /// Starts a window move drag for a left-button press on the window chrome, and stays out of
        /// the way when the press landed on an interactive control.
        ///
        /// The PointerPressed handlers sit on each window's root panel, so they also see presses
        /// that bubbled up from children. Avalonia's ComboBox does NOT mark PointerPressed as
        /// handled while its dropdown is closed - it toggles the dropdown on PointerReleased
        /// instead - so an unconditional BeginMoveDrag() dragged the whole window and swallowed
        /// that release inside the Win32 SC_MOVE loop. The dropdown then never opened, which is
        /// why the microphone picker in Settings (and the API pickers on the main bar) could not
        /// be used at all.
        /// </summary>
        public static void BeginDragIfChrome(Window window, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(window).Properties.IsLeftButtonPressed) return;
            if (IsInteractive(e.Source as Visual)) return;

            window.BeginMoveDrag(e);
        }

        /// <summary>
        /// Walks up the visual tree from the press target looking for a control that owns the
        /// gesture itself. Popup content (dropdown items) lives in its own visual root and never
        /// reaches the window handler, so only the in-window controls need to be listed here.
        /// </summary>
        private static bool IsInteractive(Visual? source)
        {
            for (var visual = source; visual is not null; visual = visual.GetVisualParent())
            {
                if (visual is ComboBox or TextBox or Button or CheckBox or ScrollBar or Slider or ListBox or TabItem)
                    return true;
            }
            return false;
        }
    }
}
