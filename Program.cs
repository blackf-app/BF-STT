using Avalonia;
using System;
using System.Threading;
using OpenTK.Audio.OpenAL;
using System.Linq;

namespace BF_STT
{
    internal static class Program
    {
        private static Mutex? _mutex;

        [STAThread]
        public static int Main(string[] args)
        {
            Console.WriteLine("--- OpenAL Diagnostic Start ---");
            try
            {
                Console.WriteLine("Querying capture devices...");
                var devices = ALC.GetString(ALDevice.Null, AlcGetStringList.CaptureDeviceSpecifier).ToList();
                Console.WriteLine($"Found {devices.Count} capture devices:");
                foreach (var d in devices)
                {
                    Console.WriteLine($"  - {d}");
                }

                Console.WriteLine($"Opening capture device '{devices[0]}'...");
                var device = ALC.CaptureOpenDevice(devices[0], 16000, ALFormat.Mono16, 8000);
                Console.WriteLine($"Device handle: {device.Handle}");
                if (device.Handle == IntPtr.Zero)
                {
                    Console.WriteLine("Failed to open device.");
                }
                else
                {
                    Console.WriteLine("Starting capture...");
                    ALC.CaptureStart(device);
                    Console.WriteLine("Capture started.");

                    Console.WriteLine("Getting integer for CaptureSamples...");
                    int[] buffer = new int[1];
                    ALC.GetInteger(device, AlcGetInteger.CaptureSamples, 1, buffer);
                    Console.WriteLine($"Available samples: {buffer[0]}");

                    Console.WriteLine("Stopping capture...");
                    ALC.CaptureStop(device);
                    Console.WriteLine("Closing device...");
                    ALC.CaptureCloseDevice(device);
                    Console.WriteLine("OpenAL capture test succeeded.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OpenAL Error: {ex}");
            }
            Console.WriteLine("--- OpenAL Diagnostic End ---");

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
