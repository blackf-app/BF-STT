using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;

namespace BF_STT
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ForceTopmost();
            
            // Periodically enforce Topmost to prevent Taskbar from covering it
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(1000);
            timer.Tick += (s, args) => ForceTopmost();
            timer.Start();

            // Position at top-center
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            this.Left = (screenWidth - this.ActualWidth) / 2;
            this.Top = 10;
            
            try 
            {
                 // Create a simple icon from the running application executable
                 var icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "");
                 if (icon != null)
                 {
                     MyNotifyIcon.Icon = icon;
                 }
            }
            catch (Exception ex)
            {
                // Fallback or log
                System.Diagnostics.Debug.WriteLine($"Failed to load icon: {ex.Message}");
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            ForceTopmost();
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        private void ForceTopmost()
        {
            this.Topmost = true; // Ensure WPF knows about it too
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
        }
    }
}