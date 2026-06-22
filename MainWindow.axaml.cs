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
            if (Screens.Primary == null) return;
            var screen = Screens.Primary;
            var bounds = screen.WorkingArea;

            // WorkingArea and Position are in physical pixels, but SizeChanged sizes are
            // in logical DIPs. On displays with scaling != 100% (common on Windows) the two
            // must be reconciled via RenderScaling, otherwise the window drifts on resize.
            double scale = RenderScaling;
            double newWidthPx = e.NewSize.Width * scale;
            double newHeightPx = e.NewSize.Height * scale;
            double prevWidthPx = e.PreviousSize.Width * scale;
            double prevHeightPx = e.PreviousSize.Height * scale;

            double left;
            double top;

            if (e.PreviousSize.Width == 0 || e.PreviousSize.Height == 0)
            {
                // First layout: pin to bottom-center of the working area.
                left = bounds.X + (bounds.Width - newWidthPx) / 2;
                top = bounds.Y + bounds.Height - newHeightPx;
            }
            else
            {
                // The window auto-sizes (SizeToContent) — width grows when the recording
                // visualizer/status appears, height grows when history opens. Keep the
                // window's horizontal CENTER and BOTTOM edge fixed so it expands/contracts
                // symmetrically instead of jumping sideways, while preserving any position
                // the user dragged it to.
                left = Position.X - (newWidthPx - prevWidthPx) / 2;
                top = Position.Y - (newHeightPx - prevHeightPx);
            }

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
                var bounds = Screens.Primary.WorkingArea;
                double scale = RenderScaling;
                double left = bounds.X + (bounds.Width - Bounds.Width * scale) / 2;
                double top = bounds.Y + bounds.Height - Bounds.Height * scale;
                Position = new PixelPoint((int)left, (int)top);
            }
        }

        private void ForceTopmost()
        {
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
