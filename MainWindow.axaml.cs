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

            double left;
            double top;

            if (e.PreviousSize.Height == 0)
            {
                left = bounds.X + (bounds.Width - e.NewSize.Width) / 2;
                top = bounds.Y + bounds.Height - e.NewSize.Height;
            }
            else
            {
                left = Position.X;
                top = Position.Y - (e.NewSize.Height - e.PreviousSize.Height);
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
                double left = bounds.X + (bounds.Width - Bounds.Width) / 2;
                double top = bounds.Y + bounds.Height - Bounds.Height;
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
