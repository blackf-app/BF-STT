using NAudio.Wave;
using OpenTK.Audio.OpenAL;

namespace BF_STT.Services.Audio
{
    /// <summary>
    /// Cross-platform audio capture using OpenAL's ALC_EXT_CAPTURE extension via OpenTK.
    /// Used on macOS (and any non-Windows OS). OpenAL ships with macOS.
    /// </summary>
    internal class OpenAlCapture : IAudioCapture
    {
        private ALCaptureDevice _device;
        private Thread? _pollThread;
        private volatile bool _running;
        private readonly object _gate = new();

        public int DeviceNumber { get; set; } = 0;
        public WaveFormat WaveFormat { get; set; } = new WaveFormat(16000, 16, 1);
        public int BufferMilliseconds { get; set; } = 50;

        public event EventHandler<AudioFrameEventArgs>? DataAvailable;
        public event EventHandler<Exception?>? CaptureStopped;

        public void StartCapture()
        {
            lock (_gate)
            {
                if (_running) return;

                if (WaveFormat.BitsPerSample != 16)
                    throw new NotSupportedException("OpenAlCapture only supports 16-bit PCM.");

                var format = WaveFormat.Channels == 1
                    ? ALFormat.Mono16
                    : ALFormat.Stereo16;

                string? deviceName = ResolveDeviceName(DeviceNumber);

                int samplesPerBuffer = WaveFormat.SampleRate * BufferMilliseconds / 1000;
                int captureBufferSize = samplesPerBuffer * 8;

                _device = ALC.CaptureOpenDevice(deviceName, WaveFormat.SampleRate, format, captureBufferSize);
                if (_device.Handle == IntPtr.Zero)
                {
                    throw new InvalidOperationException(
                        $"Failed to open OpenAL capture device '{deviceName ?? "(default)"}'.");
                }

                ALC.CaptureStart(_device);
                _running = true;

                _pollThread = new Thread(PollLoop)
                {
                    IsBackground = true,
                    Name = "BF-STT OpenAL capture"
                };
                _pollThread.Start();
            }
        }

        public void StopCapture()
        {
            lock (_gate)
            {
                _running = false;
            }
        }

        private void PollLoop()
        {
            Exception? terminatingException = null;
            try
            {
                int samplesPerFrame = WaveFormat.SampleRate * BufferMilliseconds / 1000;
                int bytesPerFrame = samplesPerFrame * WaveFormat.Channels * (WaveFormat.BitsPerSample / 8);
                var buffer = new byte[bytesPerFrame];
                var sampleCountBuffer = new int[1];
                while (_running)
                {
                    ALC.GetInteger(_device, AlcGetInteger.CaptureSamples, 1, sampleCountBuffer);
                    int available = sampleCountBuffer[0];

                    if (available >= samplesPerFrame)
                    {
                        ALC.CaptureSamples(_device, buffer, samplesPerFrame);
                        DataAvailable?.Invoke(this, new AudioFrameEventArgs(buffer, bytesPerFrame));
                    }
                    else
                    {
                        Thread.Sleep(Math.Max(1, BufferMilliseconds / 2));
                    }
                }
            }
            catch (Exception ex)
            {
                terminatingException = ex;
                Serilog.Log.Warning(ex, "OpenAL capture loop terminated unexpectedly");
            }
            finally
            {
                try
                {
                    if (_device.Handle != IntPtr.Zero)
                    {
                        ALC.CaptureStop(_device);
                        ALC.CaptureCloseDevice(_device);
                        _device = default;
                    }
                }
                catch { }
                CaptureStopped?.Invoke(this, terminatingException);
            }
        }

        private static string? ResolveDeviceName(int index)
        {
            try
            {
                var devices = ALC.GetString(ALDevice.Null, AlcGetStringList.CaptureDeviceSpecifier).ToList();
                if (devices.Count == 0) return null;
                if (index < 0 || index >= devices.Count) return devices[0];
                return devices[index];
            }
            catch
            {
                return null;
            }
        }

        public void Dispose()
        {
            StopCapture();
            _pollThread?.Join(500);
            _pollThread = null;
        }
    }
}
