using NAudio.Wave;

namespace BF_STT.Services.TTS
{
    internal sealed class SpeedAdjustedSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly int _channels;
        private float[] _sourceBuffer = Array.Empty<float>();
        private float[] _frameA;
        private float[] _frameB;
        private bool _hasFrameA;
        private bool _hasFrameB;
        private bool _endOfSource;
        private double _position;

        public SpeedAdjustedSampleProvider(ISampleProvider source, float speed)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _channels = Math.Max(1, source.WaveFormat.Channels);
            Speed = Math.Clamp(speed, 0.5f, 2.0f);
            _frameA = new float[_channels];
            _frameB = new float[_channels];
        }

        public WaveFormat WaveFormat => _source.WaveFormat;

        public float Speed { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            int written = 0;
            int framesRequested = count / _channels;
            while (written / _channels < framesRequested)
            {
                if (!EnsureFrameWindow())
                {
                    break;
                }

                double frac = _position;
                for (int channel = 0; channel < _channels; channel++)
                {
                    buffer[offset + written + channel] =
                        _frameA[channel] + (float)((_frameB[channel] - _frameA[channel]) * frac);
                }

                written += _channels;
                _position += Speed;

                while (_position >= 1.0d)
                {
                    ShiftFrameWindow();
                    _position -= 1.0d;

                    if (!_hasFrameB && !TryReadNextFrame(_frameB))
                    {
                        break;
                    }
                }
            }

            return written;
        }

        private bool EnsureFrameWindow()
        {
            if (!_hasFrameA && !TryReadNextFrame(_frameA))
            {
                return false;
            }

            if (!_hasFrameB && !TryReadNextFrame(_frameB))
            {
                return false;
            }

            return _hasFrameA;
        }

        private void ShiftFrameWindow()
        {
            if (!_hasFrameB)
            {
                return;
            }

            Array.Copy(_frameB, _frameA, _channels);
            _hasFrameA = true;
            _hasFrameB = false;
        }

        private bool TryReadNextFrame(float[] targetFrame)
        {
            if (_endOfSource)
            {
                return false;
            }

            EnsureBufferCapacity(_channels);
            int read = _source.Read(_sourceBuffer, 0, _channels);
            if (read < _channels)
            {
                _endOfSource = true;
                return false;
            }

            Array.Copy(_sourceBuffer, targetFrame, _channels);
            if (ReferenceEquals(targetFrame, _frameA))
            {
                _hasFrameA = true;
            }
            else if (ReferenceEquals(targetFrame, _frameB))
            {
                _hasFrameB = true;
            }

            return true;
        }

        private void EnsureBufferCapacity(int sampleCount)
        {
            if (_sourceBuffer.Length < sampleCount)
            {
                _sourceBuffer = new float[sampleCount];
            }
        }
    }
}
