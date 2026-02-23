using System;
using RNNoise.NET;

namespace BF_STT.Services.Audio
{
    public class NoiseSuppressionService : IDisposable
    {
        private Denoiser? _denoiser;
        private bool _isDisposed;

        public NoiseSuppressionService()
        {
            try
            {
                _denoiser = new Denoiser();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NoiseSuppression] Failed to initialize RNNoise: {ex.Message}");
            }
        }

        /// <summary>
        /// Processes audio data. Expects 16-bit PCM at 48kHz.
        /// Returns processed 16-bit PCM at 48kHz.
        /// </summary>
        public void Process(short[] buffer, int count)
        {
            if (_denoiser == null || _isDisposed) return;

            // Converter to float
            float[] floatBuffer = new float[count];
            for (int i = 0; i < count; i++)
            {
                floatBuffer[i] = buffer[i] / 32768f;
            }

            // Denoise
            // The library says it handles varying sizes, so we pass it directly.
            _denoiser.Denoise(floatBuffer);

            // Convert back to short
            for (int i = 0; i < count; i++)
            {
                float val = floatBuffer[i] * 32768f;
                if (val > 32767) val = 32767;
                if (val < -32768) val = -32768;
                buffer[i] = (short)val;
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _denoiser?.Dispose();
            _denoiser = null;
            _isDisposed = true;
        }
    }
}
