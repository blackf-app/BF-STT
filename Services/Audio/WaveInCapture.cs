#if WINDOWS
using NAudio.Wave;
using System.Runtime.Versioning;

namespace BF_STT.Services.Audio
{
    /// <summary>
    /// Windows-only audio capture backed by NAudio's WaveInEvent (Windows MM API).
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal class WaveInCapture : IAudioCapture
    {
        private WaveInEvent? _waveIn;

        public int DeviceNumber { get; set; } = 0;
        public WaveFormat WaveFormat { get; set; } = new WaveFormat(16000, 16, 1);
        public int BufferMilliseconds { get; set; } = 50;

        public event EventHandler<AudioFrameEventArgs>? DataAvailable;
        public event EventHandler<Exception?>? CaptureStopped;

        public void StartCapture()
        {
            if (WaveInEvent.DeviceCount == 0)
                throw new InvalidOperationException("No microphone detected.");

            _waveIn = new WaveInEvent
            {
                DeviceNumber = DeviceNumber,
                WaveFormat = WaveFormat,
                BufferMilliseconds = BufferMilliseconds,
                NumberOfBuffers = 3,
            };
            _waveIn.DataAvailable += OnWaveInDataAvailable;
            _waveIn.RecordingStopped += OnWaveInStopped;
            _waveIn.StartRecording();
        }

        public void StopCapture()
        {
            try { _waveIn?.StopRecording(); }
            catch { /* tolerated */ }
        }

        private void OnWaveInDataAvailable(object? sender, WaveInEventArgs e)
        {
            DataAvailable?.Invoke(this, new AudioFrameEventArgs(e.Buffer, e.BytesRecorded));
        }

        private void OnWaveInStopped(object? sender, StoppedEventArgs e)
        {
            CaptureStopped?.Invoke(this, e.Exception);
        }

        public void Dispose()
        {
            try { _waveIn?.Dispose(); } catch { }
            _waveIn = null;
        }
    }
}
#endif
