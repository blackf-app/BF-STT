using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using BF_STT.ViewModels;
using System;

namespace BF_STT
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.SizeChanged += MainWindow_SizeChanged;
            this.Opened += MainWindow_Opened;
            this.PropertyChanged += MainWindow_PropertyChanged;
        }

        private void MainWindow_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == DataContextProperty && e.NewValue is MainViewModel vm)
            {
                vm.PropertyChanged += Vm_PropertyChanged;
            }
        }

        private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsHistoryVisible))
            {
                if (DataContext is MainViewModel vm && vm.IsHistoryVisible)
                {
                    vm.IsHistoryAtTop = true;
                }
            }
        }

        private void MainWindow_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            var screen = Screens.Primary;
            if (screen == null) return;

            // Use the FULL screen bounds (which INCLUDE the taskbar area), not WorkingArea,
            // so the window overlays the taskbar at the very bottom instead of resting above it.
            var full = screen.Bounds;

            // Bounds/Position are physical pixels, but SizeChanged sizes are logical DIPs.
            // Reconcile via RenderScaling so positioning is correct at any display scaling.
            double scale = RenderScaling;
            double newWidthPx = e.NewSize.Width * scale;
            double newHeightPx = e.NewSize.Height * scale;
            double prevWidthPx = e.PreviousSize.Width * scale;

            // Horizontal: center on first layout; afterwards keep the window's center fixed so
            // it grows/shrinks symmetrically (no sideways jump) and a horizontal drag survives.
            double left = (e.PreviousSize.Width == 0)
                ? full.X + (full.Width - newWidthPx) / 2
                : Position.X - (newWidthPx - prevWidthPx) / 2;

            // Vertical: ALWAYS pin the bottom edge to the bottom of the screen. The window
            // auto-sizes (SizeToContent), so it grows upward when the recording visualizer or
            // history panel appears while its bottom stays put, overlapping the taskbar — no
            // jump up above the taskbar, no jump to mid-screen.
            double top = full.Y + full.Height - newHeightPx;

            Position = new PixelPoint((int)left, (int)top);
        }

        private void MainWindow_Opened(object? sender, EventArgs e)
        {
            ForceTopmost();

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
            timer.Tick += (s, args) => ForceTopmost();
            timer.Start();

            if (Screens.Primary != null)
            {
                var full = Screens.Primary.Bounds;
                double scale = RenderScaling;
                double left = full.X + (full.Width - Bounds.Width * scale) / 2;
                double top = full.Y + full.Height - Bounds.Height * scale;
                Position = new PixelPoint((int)left, (int)top);
            }
        }

        private void ForceTopmost()
        {
            this.Topmost = false;
            this.Topmost = true;
        }

        private void OnGridPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        }
    }
}
