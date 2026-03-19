using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace BF_STT.Services.Audio
{
    /// <summary>
    /// Encapsulates the audio processing chain: HPF → AGC → soft clip → optional noise suppression + resampling.
    /// Input: raw 16-bit PCM at either 16kHz (no NS) or 48kHz (NS enabled).
    /// Output: processed 16-bit PCM at 16kHz.
    /// </summary>
    internal class AudioPipeline : IDisposable
    {
        private float _prevSample = 0;
        private float _prevHpfOutput = 0;
        private const float HpfAlpha = 0.97f;

        private readonly AutoGainControl _agc = new AutoGainControl();
        private NoiseSuppressionService? _noiseService;
        private BufferedWaveProvider? _resamplerInput;
        private WdlResamplingSampleProvider? _resampler;
        private float[]? _resamplerReadBuffer;

        public bool EnableNoiseSuppression { get; private set; }

        public float TargetLevel
        {
            get => _agc.TargetLevel;
            set => _agc.TargetLevel = value;
        }

        /// <summary>
        /// (Re)initialises the pipeline. Must be called before the first <see cref="Process"/> call.
        /// </summary>
        public void Initialize(bool enableNoiseSuppression)
        {
            EnableNoiseSuppression = enableNoiseSuppression;
            _prevSample = 0;
            _prevHpfOutput = 0;

            if (enableNoiseSuppression)
            {
                _noiseService ??= new NoiseSuppressionService();
                _resamplerInput = new BufferedWaveProvider(new WaveFormat(48000, 16, 1))
                {
                    ReadFully = false,
                    DiscardOnBufferOverflow = true
                };
                _resampler = new WdlResamplingSampleProvider(_resamplerInput.ToSampleProvider(), 16000);
                _resamplerReadBuffer = new float[4800];
            }
            else
            {
                _resamplerInput = null;
                _resampler = null;
                _resamplerReadBuffer = null;
            }
        }

        /// <summary>
        /// Processes a raw PCM buffer and returns processed 16kHz PCM.
        /// </summary>
        public (byte[] buffer, int bytes) Process(byte[] rawBuffer, int rawBytes)
        {
            int sampleCount = rawBytes / 2;
            var shortBuffer = new short[sampleCount];
            var floatBuffer = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                shortBuffer[i] = BitConverter.ToInt16(rawBuffer, i * 2);
                floatBuffer[i] = shortBuffer[i] / (float)short.MaxValue;
            }

            // High-Pass Filter (removes DC offset / rumble)
            for (int i = 0; i < sampleCount; i++)
            {
                float sample = floatBuffer[i];
                float hpfOut = HpfAlpha * (_prevHpfOutput + sample - _prevSample);
                _prevSample = sample;
                _prevHpfOutput = hpfOut;
                floatBuffer[i] = hpfOut;
            }

            // Auto Gain Control
            _agc.Process(floatBuffer);

            // Soft clip → back to short[]
            for (int i = 0; i < sampleCount; i++)
            {
                float softClipped = (float)Math.Tanh(floatBuffer[i]);
                float finalFloat = softClipped * short.MaxValue;
                if (finalFloat > short.MaxValue) finalFloat = short.MaxValue;
                if (finalFloat < short.MinValue) finalFloat = short.MinValue;
                shortBuffer[i] = (short)finalFloat;
            }

            if (EnableNoiseSuppression
                && _noiseService != null
                && _resamplerInput != null
                && _resampler != null
                && _resamplerReadBuffer != null)
            {
                // Denoise at 48kHz
                _noiseService.Process(shortBuffer, sampleCount);

                // Resample 48kHz → 16kHz using WDL sinc interpolation
                byte[] inputBytes = new byte[sampleCount * 2];
                Buffer.BlockCopy(shortBuffer, 0, inputBytes, 0, sampleCount * 2);
                _resamplerInput.AddSamples(inputBytes, 0, sampleCount * 2);

                int expectedSamples = sampleCount / 3 + 16;
                if (_resamplerReadBuffer.Length < expectedSamples)
                    _resamplerReadBuffer = new float[expectedSamples];

                int samplesRead = _resampler.Read(_resamplerReadBuffer, 0, expectedSamples);
                var downsampled = new short[samplesRead];
                for (int i = 0; i < samplesRead; i++)
                {
                    float val = _resamplerReadBuffer[i] * short.MaxValue;
                    if (val > short.MaxValue) val = short.MaxValue;
                    if (val < short.MinValue) val = short.MinValue;
                    downsampled[i] = (short)val;
                }

                int finalBytes = samplesRead * 2;
                var finalBuffer = new byte[finalBytes];
                Buffer.BlockCopy(downsampled, 0, finalBuffer, 0, finalBytes);
                return (finalBuffer, finalBytes);
            }
            else
            {
                var finalBuffer = new byte[rawBytes];
                Buffer.BlockCopy(shortBuffer, 0, finalBuffer, 0, rawBytes);
                return (finalBuffer, rawBytes);
            }
        }

        public void Dispose()
        {
            _noiseService?.Dispose();
            _noiseService = null;
            _resamplerInput = null;
            _resampler = null;
            _resamplerReadBuffer = null;
        }
    }
}
