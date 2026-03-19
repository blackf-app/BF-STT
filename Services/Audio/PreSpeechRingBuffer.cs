using System.IO;

namespace BF_STT.Services.Audio
{
    /// <summary>
    /// Fixed-capacity ring buffer for pre-speech audio frames.
    /// Stores the last N frames so they can be flushed (with a fade-in crossfade)
    /// when speech resumes after a silence pause.
    /// </summary>
    internal class PreSpeechRingBuffer
    {
        private readonly int _capacity;
        private readonly int _crossfadeSamples;
        private readonly byte[]?[] _buffer;
        private readonly int[] _lengths;
        private int _index;
        private int _count;

        public PreSpeechRingBuffer(int capacity, int crossfadeSamples)
        {
            _capacity = capacity;
            _crossfadeSamples = crossfadeSamples;
            _buffer = new byte[capacity][];
            _lengths = new int[capacity];
        }

        /// <summary>Pushes a frame into the ring, overwriting the oldest entry if full.</summary>
        public void Push(byte[] data, int length)
        {
            var frame = new byte[length];
            Buffer.BlockCopy(data, 0, frame, 0, length);
            _buffer[_index] = frame;
            _lengths[_index] = length;
            _index = (_index + 1) % _capacity;
            if (_count < _capacity) _count++;
        }

        /// <summary>
        /// Flushes all buffered frames to the provided writers in chronological order,
        /// applying a fade-in to the first frame to prevent click/pop artefacts.
        /// Resets the buffer afterwards.
        /// </summary>
        public void Flush(BinaryWriter? memWriter, BinaryWriter? fileWriter)
        {
            if ((memWriter == null && fileWriter == null) || _count == 0) return;

            int startIdx = _count < _capacity ? 0 : _index; // oldest entry

            for (int i = 0; i < _count; i++)
            {
                int idx = (startIdx + i) % _capacity;
                var buf = _buffer[idx];
                var len = _lengths[idx];
                if (buf == null) continue;

                if (i == 0)
                    ApplyCrossfade(buf, len, fadeIn: true);

                memWriter?.Write(buf, 0, len);
                fileWriter?.Write(buf, 0, len);
            }

            Reset();
        }

        /// <summary>Clears the ring buffer without writing.</summary>
        public void Reset()
        {
            _count = 0;
            _index = 0;
        }

        private void ApplyCrossfade(byte[] buffer, int length, bool fadeIn)
        {
            int sampleCount = length / 2;
            int fadeSamples = Math.Min(_crossfadeSamples, sampleCount);

            for (int i = 0; i < fadeSamples; i++)
            {
                float factor = fadeIn
                    ? (float)i / fadeSamples
                    : (float)(fadeSamples - i) / fadeSamples;

                int offset = fadeIn ? i * 2 : (sampleCount - fadeSamples + i) * 2;
                short sample = BitConverter.ToInt16(buffer, offset);
                sample = (short)(sample * factor);
                byte[] bytes = BitConverter.GetBytes(sample);
                buffer[offset] = bytes[0];
                buffer[offset + 1] = bytes[1];
            }
        }
    }
}
