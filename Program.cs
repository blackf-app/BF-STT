using Avalonia;
using System;
using System.Threading;

namespace BF_STT
{
    internal static class Program
    {
        private static Mutex? _mutex;

        [STAThread]
        public static int Main(string[] args)
        {
            // Single-instance check across both Windows and macOS.
            const string mutexName = "BF-STT-Unique-Mutex-Name";
            _mutex = new Mutex(true, mutexName, out bool createdNew);
            if (!createdNew)
            {
                Console.Error.WriteLine("BF-STT is already running.");
                return 0;
            }

            try
            {
                return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            finally
            {
                _mutex?.Dispose();
            }
        }

        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
