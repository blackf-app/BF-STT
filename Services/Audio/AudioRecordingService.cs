using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.IO;
using System.Threading.Tasks;

namespace BF_STT.Services.Audio
{
    public class AudioDataEventArgs : EventArgs
    {
        public byte[] Buffer { get; }
        public int BytesRecorded { get; }

        public AudioDataEventArgs(byte[] buffer, int bytesRecorded)
        {
            Buffer = buffer;
            BytesRecorded = bytesRecorded;
        }
    }

    public class AudioRecordingService : IDisposable
    {
        private WaveInEvent? _waveIn;
        private MemoryStream? _audioMemory;
        private BinaryWriter? _memWriter;
        private FileStream? _audioFile;
        private BinaryWriter? _fileWriter;
        private string? _tempFilePath;
        private WaveFormat? _waveFormat;
        private long _memDataPos;
        private long _memRiffPos;
        private long _fileDataPos;
        private long _fileRiffPos;
        private bool _isRecording;
        
        public bool SaveToFile { get; set; }
        
        // Audio processing state
        private float _prevSample = 0;
        private float _prevHpfOutput = 0;
        private const float HpfAlpha = 0.97f;
        private const float SoftClipThreshold = 0.95f;
        private AutoGainControl _agc = new AutoGainControl();
        
        // Noise Suppression
        private NoiseSuppressionService? _noiseService;
        public bool EnableNoiseSuppression { get; set; }
        
        // High-quality resampler (48kHz → 16kHz)
        private BufferedWaveProvider? _resamplerInput;
        private WdlResamplingSampleProvider? _resampler;
        private float[]? _resamplerReadBuffer;
        
        // Silent detection
        private bool _hasAudio;
        private bool _hasSpeechContent;
        private float _maxPeakLevel;
        private const float SilenceThreshold = 0.01f;
        
        // VAD (Voice Activity Detection) state
        private bool _isSpeaking;
        private int _silenceFrameCount;
        private int _speechFrameCount;
        
        // VAD Constants
        private const float VadSpeechThreshold = 0.02f;
        private const int VadSilenceFramesToPause = 10;
        private const int VadSpeechFramesToStart = 2;
        
        // Batch silence trimming
        public bool EnableSilenceTrimming { get; set; }
        private bool _batchWritePaused;
        private int _batchSilenceFrameCount;
        private const int BatchSilenceFrameLimit = 30; // 1.5s / 50ms = 30 frames
        
        // Ring buffer for pre-speech capture (~200ms = 4 frames)
        private const int RingBufferCapacity = 4;
        private byte[]?[] _ringBuffer = new byte[RingBufferCapacity][];
        private int[] _ringBufferLengths = new int[RingBufferCapacity];
        private int _ringBufferIndex;
        private int _ringBufferCount;
        
        // Crossfade to prevent click/pop at splice points
        private const int CrossfadeSamples = 80; // ~5ms at 16kHz

        public event EventHandler<StoppedEventArgs>? RecordingStopped;
        public event EventHandler<float>? AudioLevelUpdated;
        public event EventHandler<bool>? IsSpeakingChanged;
        /// <summary>
        /// Fired with processed PCM audio data ready to send to streaming API.
        /// </summary>
        public event EventHandler<AudioDataEventArgs>? AudioDataAvailable;

        public bool IsRecording => _isRecording;
        public bool IsSpeaking => _isSpeaking;

        public float VolumeMultiplier // Khuyến khích giữ lại property này nhưng redirect vào TargetLevel của AGC nếu cần, hiện tại set cho AGC không ý nghĩa lắm nếu AGC tự lo 
        {
            get => _agc.TargetLevel;
            set => _agc.TargetLevel = value;
        }

        public int DeviceNumber { get; set; } = 0;

        /// <summary>
        /// Starts recording. Writes audio to an in-memory WAV buffer
        /// AND fires AudioDataAvailable events (for streaming/buffering).
        /// </summary>
        public void StartRecording()
        {
            if (_isRecording) return;

            if (WaveIn.DeviceCount == 0)
            {
                throw new InvalidOperationException("No microphone detected.");
            }

            // Clean up previous recording
            Dispose();

            // Reset filter state
            _prevSample = 0;
            _prevHpfOutput = 0;
            
            // Reset silent detection
            _hasAudio = false;
            _maxPeakLevel = 0;
            _hasSpeechContent = false;
            
            // Reset VAD
            _isSpeaking = false;
            _silenceFrameCount = 0;
            _speechFrameCount = 0;
            
            // Reset batch silence trimming
            _batchWritePaused = false;
            _batchSilenceFrameCount = 0;
            _ringBuffer = new byte[RingBufferCapacity][];
            _ringBufferLengths = new int[RingBufferCapacity];
            _ringBufferIndex = 0;
            _ringBufferCount = 0;

            int sampleRate = EnableNoiseSuppression ? 48000 : 16000;
            if (EnableNoiseSuppression && _noiseService == null)
            {
                _noiseService = new NoiseSuppressionService();
            }

            // Initialize high-quality resampler for 48kHz → 16kHz
            if (EnableNoiseSuppression)
            {
                _resamplerInput = new BufferedWaveProvider(new WaveFormat(48000, 16, 1))
                {
                    ReadFully = false,
                    DiscardOnBufferOverflow = true
                };
                _resampler = new WdlResamplingSampleProvider(_resamplerInput.ToSampleProvider(), 16000);
                _resamplerReadBuffer = new float[4800]; // enough for 50ms at 48kHz / 3 = ~800 float samples, allocate extra
            }
            else
            {
                _resamplerInput = null;
                _resampler = null;
                _resamplerReadBuffer = null;
            }

            _waveIn = new WaveInEvent
            {
                DeviceNumber = DeviceNumber,
                WaveFormat = new WaveFormat(sampleRate, 16, 1), // 48kHz if NS enabled, else 16kHz
                BufferMilliseconds = 50,
                NumberOfBuffers = 3
            };

            _waveFormat = new WaveFormat(16000, 16, 1); // Always 16kHz for the output buffer/file

            // Initialize in-memory WAV buffer
            _audioMemory = new MemoryStream();
            _memWriter = new BinaryWriter(_audioMemory);
            var (mRiff, mData) = WriteWavHeader(_memWriter, _waveFormat);
            _memRiffPos = mRiff;
            _memDataPos = mData;

            // Initialize optional file buffer for Test Mode
            if (SaveToFile)
            {
                try
                {
                    string tempPath = Path.GetTempPath();
                    string fileName = $"bf_stt_{DateTime.Now:yyyyMMdd_HHmmss_fff}.wav";
                    _tempFilePath = Path.Combine(tempPath, fileName);
                    _audioFile = new FileStream(_tempFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                    _fileWriter = new BinaryWriter(_audioFile);
                    var (fRiff, fData) = WriteWavHeader(_fileWriter, _waveFormat);
                    _fileRiffPos = fRiff;
                    _fileDataPos = fData;
                    System.Diagnostics.Debug.WriteLine($"[AudioRecording] Test Mode: Saving to {_tempFilePath}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AudioRecording] Error creating temp file: {ex.Message}");
                }
            }

            _waveIn.DataAvailable += OnWaveInDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;

            _waveIn.StartRecording();
            _isRecording = true;
        }

        /// <summary>
        /// Writes a WAV file header to the stream. Reserves space for sizes to be patched later.
        /// </summary>
        private (long riffPos, long dataPos) WriteWavHeader(BinaryWriter writer, WaveFormat format)
        {
            // RIFF header
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            long riffPos = writer.BaseStream.Position;
            writer.Write(0); // placeholder for RIFF chunk size
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            // fmt sub-chunk
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16); // Sub-chunk size (PCM = 16)
            writer.Write((short)1); // AudioFormat (PCM = 1)
            writer.Write((short)format.Channels);
            writer.Write(format.SampleRate);
            writer.Write(format.AverageBytesPerSecond);
            writer.Write((short)format.BlockAlign);
            writer.Write((short)format.BitsPerSample);

            // data sub-chunk header
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            long dataPos = writer.BaseStream.Position;
            writer.Write(0); // placeholder for data chunk size
            return (riffPos, dataPos);
        }

        /// <summary>
        /// Patches the WAV header with the actual data and RIFF sizes.
        /// </summary>
        private void FinalizeWavHeader(BinaryWriter writer, long riffPos, long dataPos)
        {
            long totalDataBytes = writer.BaseStream.Length - dataPos - 4;

            // Patch data chunk size
            writer.BaseStream.Position = dataPos;
            writer.Write((int)totalDataBytes);

            // Patch RIFF chunk size (total file size - 8)
            writer.BaseStream.Position = riffPos;
            writer.Write((int)(writer.BaseStream.Length - 8));

            // Seek back to end
            writer.BaseStream.Position = writer.BaseStream.Length;
        }

        private void OnWaveInDataAvailable(object? sender, WaveInEventArgs e)

        {
            // 1. Convert to float to apply filters
            int sampleCount = e.BytesRecorded / 2;
            short[] shortBuffer = new short[sampleCount];
            float[] floatBuffer = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                shortBuffer[i] = BitConverter.ToInt16(e.Buffer, i * 2);
                floatBuffer[i] = shortBuffer[i] / (float)short.MaxValue;
            }

            // Apply High-Pass Filter (HPF)
            for (int i = 0; i < sampleCount; i++)
            {
                float sample = floatBuffer[i];
                float hpfOut = HpfAlpha * (_prevHpfOutput + sample - _prevSample);
                _prevSample = sample;
                _prevHpfOutput = hpfOut;
                floatBuffer[i] = hpfOut;
            }

            // Apply AGC (Auto Gain Control) to the float buffer
            _agc.Process(floatBuffer);

            // Apply Soft Clipping and convert back to short
            for (int i = 0; i < sampleCount; i++)
            {
                float normalized = floatBuffer[i];
                float softClipped = (float)Math.Tanh(normalized); 
                float finalFloat = softClipped * short.MaxValue;

                if (finalFloat > short.MaxValue) finalFloat = short.MaxValue;
                if (finalFloat < short.MinValue) finalFloat = short.MinValue;

                shortBuffer[i] = (short)finalFloat;
            }

            // Apply Noise Suppression and Downsample if needed
            byte[] finalBuffer;
            int finalBytesRecorded;

            if (EnableNoiseSuppression && _noiseService != null && _resamplerInput != null && _resampler != null && _resamplerReadBuffer != null)
            {
                // Denoise at 48kHz
                _noiseService.Process(shortBuffer, sampleCount);

                // High-quality resample 48kHz → 16kHz using WDL sinc interpolation
                byte[] inputBytes = new byte[sampleCount * 2];
                Buffer.BlockCopy(shortBuffer, 0, inputBytes, 0, sampleCount * 2);
                _resamplerInput.AddSamples(inputBytes, 0, sampleCount * 2);

                int expectedSamples = sampleCount / 3 + 16; // add margin for resampler latency
                if (_resamplerReadBuffer.Length < expectedSamples)
                    _resamplerReadBuffer = new float[expectedSamples];

                int samplesRead = _resampler.Read(_resamplerReadBuffer, 0, expectedSamples);

                short[] downsampledBuffer = new short[samplesRead];
                for (int i = 0; i < samplesRead; i++)
                {
                    float val = _resamplerReadBuffer[i] * short.MaxValue;
                    if (val > short.MaxValue) val = short.MaxValue;
                    if (val < short.MinValue) val = short.MinValue;
                    downsampledBuffer[i] = (short)val;
                }

                finalBytesRecorded = samplesRead * 2;
                finalBuffer = new byte[finalBytesRecorded];
                Buffer.BlockCopy(downsampledBuffer, 0, finalBuffer, 0, finalBytesRecorded);
            }
            else
            {
                finalBytesRecorded = e.BytesRecorded;
                finalBuffer = new byte[finalBytesRecorded];
                Buffer.BlockCopy(shortBuffer, 0, finalBuffer, 0, finalBytesRecorded);
            }

            // Process final 16kHz data for VAD and Level
            float maxSample = 0;
            double sumSquares = 0;
            int finalSampleCount = finalBytesRecorded / 2;
            for (int i = 0; i < finalSampleCount; i++)
            {
                short sample = BitConverter.ToInt16(finalBuffer, i * 2);
                float absSample = Math.Abs((float)sample / short.MaxValue);
                if (absSample > maxSample) maxSample = absSample;
                sumSquares += (absSample * absSample);
            }
            float currentFrameRms = (float)Math.Sqrt(sumSquares / finalSampleCount);

            // Track peak for silent detection
            if (maxSample > _maxPeakLevel) _maxPeakLevel = maxSample;
            if (_maxPeakLevel > SilenceThreshold) _hasAudio = true;

            // 5. VAD Logic (Now using RMS instead of Peak)
            if (currentFrameRms > VadSpeechThreshold)
            {
                _speechFrameCount++;
                _silenceFrameCount = 0;
                
                if (!_isSpeaking && _speechFrameCount >= VadSpeechFramesToStart)
                {
                    _isSpeaking = true;
                    _hasSpeechContent = true;
                    IsSpeakingChanged?.Invoke(this, true);
                    System.Diagnostics.Debug.WriteLine($"[AudioRecording] VAD: Speech Started (RMS: {currentFrameRms:F4}, Peak: {maxSample:F4})");
                }
            }
            else
            {
                _silenceFrameCount++;
                _speechFrameCount = 0;

                if (_isSpeaking && _silenceFrameCount >= VadSilenceFramesToPause)
                {
                    _isSpeaking = false;
                    IsSpeakingChanged?.Invoke(this, false);
                    System.Diagnostics.Debug.WriteLine("[AudioRecording] VAD: Silence Detected (Paused)");
                }
            }

            AudioLevelUpdated?.Invoke(this, maxSample);

            // 1. Write to output streams
            void WriteToOutputs(byte[] buffer, int offset, int count)
            {
                _memWriter?.Write(buffer, offset, count);
                _fileWriter?.Write(buffer, offset, count);
            }

            try
            {
                if (EnableSilenceTrimming)
                {
                    if (currentFrameRms > VadSpeechThreshold)
                    {
                        // Speech detected
                        if (_batchWritePaused)
                        {
                            // RESUME: flush ring buffer (pre-speech audio) with fade-in
                            FlushToOutputs();
                            _batchWritePaused = false;
                        }
                        _batchSilenceFrameCount = 0;
                        WriteToOutputs(finalBuffer, 0, finalBytesRecorded);
                    }
                    else
                    {
                        // Silence
                        _batchSilenceFrameCount++;

                        if (_batchSilenceFrameCount <= BatchSilenceFrameLimit)
                        {
                            // Within 1.5s allowance — still write
                            WriteToOutputs(finalBuffer, 0, finalBytesRecorded);
                        }
                        else
                        {
                            // Exceeded 1.5s — pause writing, store in ring buffer
                            if (!_batchWritePaused)
                            {
                                _batchWritePaused = true;
                            }
                            PushToRingBuffer(finalBuffer, finalBytesRecorded);
                        }
                    }
                }
                else
                {
                    // No trimming — write everything (original behavior)
                    WriteToOutputs(finalBuffer, 0, finalBytesRecorded);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error writing to output streams: {ex.Message}");
            }

            // 2. Fire event for streaming
            AudioDataAvailable?.Invoke(this, new AudioDataEventArgs(finalBuffer, finalBytesRecorded));
        }

        private bool _discardRecording;
        private TaskCompletionSource<byte[]?>? _stopRecordingTcs;

        /// <summary>
        /// Stops recording and returns the complete WAV audio data as a byte array.
        /// If discard is true, returns null.
        /// </summary>
        public Task<byte[]?> StopRecordingAsync(bool discard = false)
        {
            if (!_isRecording) return Task.FromResult<byte[]?>(null);

            _discardRecording = discard;
            _stopRecordingTcs = new TaskCompletionSource<byte[]?>();
            
            try 
            {
                _waveIn?.StopRecording();
            }
            catch (Exception)
            {
                _stopRecordingTcs.TrySetResult(null);
                return _stopRecordingTcs.Task;
            }
            
            return _stopRecordingTcs.Task;
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            byte[]? audioData = null;

            try 
            {
                if (_memWriter != null && _audioMemory != null && !_discardRecording)
                {
                    FinalizeWavHeader(_memWriter, _memRiffPos, _memDataPos);
                    _memWriter.Flush();
                    audioData = _audioMemory.ToArray();
                }

                if (_fileWriter != null && !_discardRecording)
                {
                    FinalizeWavHeader(_fileWriter, _fileRiffPos, _fileDataPos);
                    _fileWriter.Flush();
                    System.Diagnostics.Debug.WriteLine($"[AudioRecording] Test Mode: Saved recording to {_tempFilePath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error finalizing audio: {ex.Message}");
            }
            finally
            {
                // Always dispose and clear memory
                try { _memWriter?.Dispose(); } catch { }
                _memWriter = null;
                try { _audioMemory?.Dispose(); } catch { }
                _audioMemory = null;
                
                try { _fileWriter?.Dispose(); } catch { }
                _fileWriter = null;
                try { _audioFile?.Dispose(); } catch { }
                _audioFile = null;
            }

            _waveIn?.Dispose();
            _waveIn = null;
            _isRecording = false;

            if (e.Exception != null)
            {
                _stopRecordingTcs?.TrySetException(e.Exception);
            }
            else
            {
                _stopRecordingTcs?.TrySetResult(_discardRecording ? null : audioData);
            }

            RecordingStopped?.Invoke(this, e);
        }

        /// <summary>
        /// Returns true if the last recording contained meaningful audio above the silence threshold.
        /// Must be called after StopRecordingAsync completes.
        /// </summary>
        public bool HasMeaningfulAudio()
        {
            return _hasSpeechContent || (_hasAudio && _speechFrameCount > 0);
        }

        /// <summary>
        /// Pushes a frame into the ring buffer, overwriting the oldest if full.
        /// </summary>
        private void PushToRingBuffer(byte[] data, int length)
        {
            var frame = new byte[length];
            Buffer.BlockCopy(data, 0, frame, 0, length);
            _ringBuffer[_ringBufferIndex] = frame;
            _ringBufferLengths[_ringBufferIndex] = length;
            _ringBufferIndex = (_ringBufferIndex + 1) % RingBufferCapacity;
            if (_ringBufferCount < RingBufferCapacity) _ringBufferCount++;
        }

        /// <summary>
        /// Flushes ring buffer to _memWriter in chronological order.
        /// Applies fade-in to the first frame for smooth transition.
        /// </summary>
        private void FlushToOutputs()
        {
            if ((_memWriter == null && _fileWriter == null) || _ringBufferCount == 0) return;

            int startIdx = _ringBufferCount < RingBufferCapacity
                ? 0
                : _ringBufferIndex; // oldest entry

            for (int i = 0; i < _ringBufferCount; i++)
            {
                int idx = (startIdx + i) % RingBufferCapacity;
                var buf = _ringBuffer[idx];
                var len = _ringBufferLengths[idx];
                if (buf == null) continue;

                if (i == 0)
                {
                    // Apply fade-in to first frame to prevent click/pop
                    ApplyCrossfade(buf, len, fadeIn: true);
                }

                _memWriter?.Write(buf, 0, len);
                _fileWriter?.Write(buf, 0, len);
            }

            // Clear ring buffer
            _ringBufferCount = 0;
            _ringBufferIndex = 0;
        }

        /// <summary>
        /// Applies a short linear fade-in or fade-out to a 16-bit PCM buffer
        /// to prevent click/pop artifacts at splice points.
        /// </summary>
        private void ApplyCrossfade(byte[] buffer, int length, bool fadeIn)
        {
            int sampleCount = length / 2;
            int fadeSamples = Math.Min(CrossfadeSamples, sampleCount);

            for (int i = 0; i < fadeSamples; i++)
            {
                float factor = fadeIn
                    ? (float)i / fadeSamples        // 0 → 1
                    : (float)(fadeSamples - i) / fadeSamples; // 1 → 0

                int offset = fadeIn ? i * 2 : (sampleCount - fadeSamples + i) * 2;
                short sample = BitConverter.ToInt16(buffer, offset);
                sample = (short)(sample * factor);
                byte[] bytes = BitConverter.GetBytes(sample);
                buffer[offset] = bytes[0];
                buffer[offset + 1] = bytes[1];
            }
        }

        public void Dispose()
        {
            if (_isRecording)
            {
                try { _waveIn?.StopRecording(); } catch { }
            }
            
            try { _memWriter?.Dispose(); } catch { }
            _memWriter = null;
            try { _audioMemory?.Dispose(); } catch { }
            _audioMemory = null;

            try { _fileWriter?.Dispose(); } catch { }
            _fileWriter = null;
            try { _audioFile?.Dispose(); } catch { }
            _audioFile = null;

            _waveIn?.Dispose();
            _waveIn = null;
            _noiseService?.Dispose();
            _noiseService = null;
            _resamplerInput = null;
            _resampler = null;
            _resamplerReadBuffer = null;
        }
    }
}
