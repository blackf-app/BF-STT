using System.Runtime.InteropServices;

namespace BF_STT.Services.Audio
{
    /// <summary>
    /// Enumerates capture devices in a cross-platform way.
    /// </summary>
    public static class AudioDeviceEnumerator
    {
        public static IReadOnlyList<AudioDeviceInfo> EnumerateInputDevices()
        {
#if WINDOWS
            return EnumerateWindows();
#else
            if (OperatingSystem.IsWindows())
            {
                return EnumerateWindows();
            }
            return EnumerateOpenAl();
#endif
        }

#if WINDOWS
        private static IReadOnlyList<AudioDeviceInfo> EnumerateWindows()
        {
            var list = new List<AudioDeviceInfo>();
            for (int i = 0; i < NAudio.Wave.WaveInEvent.DeviceCount; i++)
            {
                var caps = NAudio.Wave.WaveInEvent.GetCapabilities(i);
                list.Add(new AudioDeviceInfo { Index = i, Name = caps.ProductName });
            }
            return list;
        }
#else
        private static IReadOnlyList<AudioDeviceInfo> EnumerateWindows() => Array.Empty<AudioDeviceInfo>();
#endif

        private static IReadOnlyList<AudioDeviceInfo> EnumerateOpenAl()
        {
            var list = new List<AudioDeviceInfo>();
            try
            {
                var devices = OpenTK.Audio.OpenAL.ALC.GetString(
                    OpenTK.Audio.OpenAL.ALDevice.Null,
                    OpenTK.Audio.OpenAL.AlcGetStringList.CaptureDeviceSpecifier);
                int idx = 0;
                foreach (var dev in devices)
                {
                    list.Add(new AudioDeviceInfo { Index = idx++, Name = dev });
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Failed to enumerate OpenAL devices");
            }
            return list;
        }
    }
}
