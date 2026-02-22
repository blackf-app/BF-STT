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
            this.DataContextChanged += (s, e) =>
            {
                if (e.NewValue is ViewModels.MainViewModel vm)
                {
                    vm.PropertyChanged += Vm_PropertyChanged;
                }
            };
            this.SizeChanged += MainWindow_SizeChanged;
        }

        private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.MainViewModel.IsHistoryVisible))
            {
                var vm = DataContext as ViewModels.MainViewModel;
                if (vm == null) return;

                if (vm.IsHistoryVisible)
                {
                    // Since the window is now anchored to the bottom of the screen,
                    // any expansion (like history) should always go UPWARDS.
                    vm.IsHistoryAtTop = true;
                }
            }
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.HeightChanged)
            {
                // Always keep the bottom of the window anchored to the bottom of the screen
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;
                double left = (screenWidth - e.NewSize.Width) / 2;
                double top = screenHeight - e.NewSize.Height;

                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    SetWindowPos(hwnd, HWND_TOPMOST, (int)left, (int)top, (int)e.NewSize.Width, (int)e.NewSize.Height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ForceTopmost();
            
            // Periodically enforce Topmost to prevent Taskbar from covering it
            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(1000);
            timer.Tick += (s, args) => ForceTopmost();
            timer.Start();

            // Initial Position calculation
            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            double left = (screenWidth - this.ActualWidth) / 2;
            double top = screenHeight - this.ActualHeight;

            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                // Set position using Win32 for precise placement overlapping taskbar
                SetWindowPos(hwnd, HWND_TOPMOST, (int)left, (int)top, (int)this.ActualWidth, (int)this.ActualHeight, SWP_SHOWWINDOW | SWP_NOACTIVATE);
            }
            
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