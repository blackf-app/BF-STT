using System.IO;
using Android.Media;

namespace BFSTT.Droid.Audio
{
    /// <summary>
    /// Captures mono 16 kHz 16-bit PCM from the microphone using AudioRecord and
    /// returns it as a ready-to-send WAV buffer. 16 kHz matches what the desktop
    /// pipeline downsamples to, so the same STT providers work unchanged.
    /// </summary>
    public sealed class AndroidAudioRecorder
    {
        public const int SampleRate = 16000;

        private AudioRecord? _recorder;
        private Thread? _thread;
        private volatile bool _recording;
        private MemoryStream _buffer = new();

        public bool IsRecording => _recording;

        public void Start()
        {
            int min = AudioRecord.GetMinBufferSize(SampleRate, ChannelIn.Mono, Encoding.Pcm16bit);
            if (min <= 0) min = SampleRate * 2;
            int bufSize = System.Math.Max(min, SampleRate * 2); // ~1s headroom

            _buffer = new MemoryStream();
            _recorder = new AudioRecord(AudioSource.Mic, SampleRate, ChannelIn.Mono, Encoding.Pcm16bit, bufSize);

            if (_recorder.State != State.Initialized)
            {
                _recorder.Release();
                _recorder = null;
                throw new InvalidOperationException("Khong khoi tao duoc microphone (AudioRecord).");
            }

            _recording = true;
            _recorder.StartRecording();

            _thread = new Thread(ReadLoop) { IsBackground = true };
            _thread.Start();
        }

        private void ReadLoop()
        {
            var buf = new byte[4096];
            var rec = _recorder;
            while (_recording && rec != null)
            {
                int n = rec.Read(buf, 0, buf.Length);
                if (n > 0)
                {
                    _buffer.Write(buf, 0, n);
                }
                else if (n < 0)
                {
                    break;
                }
            }
        }

        /// <summary>Stops capture and returns the recorded audio as a WAV byte buffer.</summary>
        public byte[] Stop()
        {
            _recording = false;
            try { _thread?.Join(1500); } catch { /* ignore */ }

            try { _recorder?.Stop(); } catch { /* ignore */ }
            try { _recorder?.Release(); } catch { /* ignore */ }
            _recorder = null;

            byte[] pcm = _buffer.ToArray();
            return WavWriter.Pcm16ToWav(pcm, SampleRate, 1);
        }
    }
}
