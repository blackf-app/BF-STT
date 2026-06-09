using NAudio.Wave;

namespace BF_STT.Services.Audio
{
    /// <summary>
    /// Platform-neutral audio capture interface.
    /// Windows: implemented via NAudio's WaveInEvent.
    /// macOS: implemented via OpenAL's capture API (OpenTK).
    /// </summary>
    public interface IAudioCapture : IDisposable
    {
        int DeviceNumber { get; set; }
        WaveFormat WaveFormat { get; set; }
        int BufferMilliseconds { get; set; }

        event EventHandler<AudioFrameEventArgs>? DataAvailable;
        event EventHandler<Exception?>? CaptureStopped;

        void StartCapture();
        void StopCapture();
    }

    public class AudioFrameEventArgs : EventArgs
    {
        public byte[] Buffer { get; }
        public int BytesRecorded { get; }

        public AudioFrameEventArgs(byte[] buffer, int bytesRecorded)
        {
            Buffer = buffer;
            BytesRecorded = bytesRecorded;
        }
    }

    /// <summary>
    /// Describes a discovered audio capture device.
    /// </summary>
    public class AudioDeviceInfo
    {
        public int Index { get; init; }
        public string Name { get; init; } = string.Empty;
    }
}
