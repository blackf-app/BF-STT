using NAudio.Wave;
using System;
using System.IO;
using System.Threading.Tasks;

namespace BF_STT.Services
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
        private WaveFormat? _waveFormat;
        private long _dataChunkSizePosition;
        private long _riffSizePosition;
        private bool _isRecording;
        
        // Audio processing state
        private float _volumeMultiplier = 6f;
        private float _prevSample = 0;
        private float _prevHpfOutput = 0;
        private const float HpfAlpha = 0.97f;
        private const float SoftClipThreshold = 0.95f;
        
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

        public event EventHandler<StoppedEventArgs>? RecordingStopped;
        public event EventHandler<float>? AudioLevelUpdated;
        public event EventHandler<bool>? IsSpeakingChanged;
        /// <summary>
        /// Fired with processed PCM audio data ready to send to streaming API.
        /// </summary>
        public event EventHandler<AudioDataEventArgs>? AudioDataAvailable;

        public bool IsRecording => _isRecording;
        public bool IsSpeaking => _isSpeaking;

        public float VolumeMultiplier
        {
            get => _volumeMultiplier;
            set => _volumeMultiplier = value;
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

            _waveIn = new WaveInEvent
            {
                DeviceNumber = DeviceNumber,
                WaveFormat = new WaveFormat(16000, 16, 1), // 16kHz, 16-bit, Mono
                BufferMilliseconds = 50,
                NumberOfBuffers = 3
            };

            _waveFormat = _waveIn.WaveFormat;

            // Initialize in-memory WAV buffer
            _audioMemory = new MemoryStream();
            _memWriter = new BinaryWriter(_audioMemory);
            WriteWavHeader(_memWriter, _waveFormat);

            _waveIn.DataAvailable += OnWaveInDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;

            _waveIn.StartRecording();
            _isRecording = true;
        }

        /// <summary>
        /// Writes a WAV file header to the stream. Reserves space for sizes to be patched later.
        /// </summary>
        private void WriteWavHeader(BinaryWriter writer, WaveFormat format)
        {
            // RIFF header
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            _riffSizePosition = writer.BaseStream.Position;
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
            _dataChunkSizePosition = writer.BaseStream.Position;
            writer.Write(0); // placeholder for data chunk size
        }

        /// <summary>
        /// Patches the WAV header with the actual data and RIFF sizes.
        /// </summary>
        private void FinalizeWavHeader(BinaryWriter writer)
        {
            long totalDataBytes = writer.BaseStream.Length - _dataChunkSizePosition - 4;

            // Patch data chunk size
            writer.BaseStream.Position = _dataChunkSizePosition;
            writer.Write((int)totalDataBytes);

            // Patch RIFF chunk size (total file size - 8)
            writer.BaseStream.Position = _riffSizePosition;
            writer.Write((int)(writer.BaseStream.Length - 8));

            // Seek back to end
            writer.BaseStream.Position = writer.BaseStream.Length;
        }

        private void OnWaveInDataAvailable(object? sender, WaveInEventArgs e)

        {
            float maxSample = 0;
            
            // Process samples in-place
            for (int i = 0; i < e.BytesRecorded; i += 2)
            {
                // 1. Convert to float
                short rawSample = BitConverter.ToInt16(e.Buffer, i);
                float sample = rawSample;

                // 2. High-Pass Filter (DC Offset Removal & Rumble Filter)
                float hpfOut = HpfAlpha * (_prevHpfOutput + sample - _prevSample);
                
                // Update filter state
                _prevSample = sample;
                _prevHpfOutput = hpfOut;

                // 3. Apply Gain
                float amplified = hpfOut * _volumeMultiplier;

                // 4. Soft Clipping (Tanh Limiter)
                float normalized = amplified / short.MaxValue;
                float softClipped = (float)Math.Tanh(normalized); 
                float finalFloat = softClipped * short.MaxValue;

                // Clamp strictly to short range
                if (finalFloat > short.MaxValue) finalFloat = short.MaxValue;
                if (finalFloat < short.MinValue) finalFloat = short.MinValue;

                short finalSample = (short)finalFloat;

                // Track peak for UI visualization
                float absSample = Math.Abs(finalFloat / short.MaxValue);
                if (absSample > maxSample) maxSample = absSample;

                // Write back to buffer
                byte[] bytes = BitConverter.GetBytes(finalSample);
                e.Buffer[i] = bytes[0];
                e.Buffer[i + 1] = bytes[1];
            }

            // Track peak for silent detection
            if (maxSample > _maxPeakLevel) _maxPeakLevel = maxSample;
            if (_maxPeakLevel > SilenceThreshold) _hasAudio = true;

        // 5. VAD Logic
        if (maxSample > VadSpeechThreshold)
        {
            _speechFrameCount++;
            _silenceFrameCount = 0;
            
            if (!_isSpeaking && _speechFrameCount >= VadSpeechFramesToStart)
            {
                _isSpeaking = true;
                _hasSpeechContent = true;
                IsSpeakingChanged?.Invoke(this, true);
                System.Diagnostics.Debug.WriteLine("[AudioRecording] VAD: Speech Started");
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

            // 1. Write to in-memory WAV buffer (for batch mode)
            if (_memWriter != null)
            {
                try
                {
                    _memWriter.Write(e.Buffer, 0, e.BytesRecorded);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error writing to memory: {ex.Message}");
                }
            }

            // 2. Fire event for streaming
            var copy = new byte[e.BytesRecorded];
            Array.Copy(e.Buffer, copy, e.BytesRecorded);
            AudioDataAvailable?.Invoke(this, new AudioDataEventArgs(copy, e.BytesRecorded));
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
                    FinalizeWavHeader(_memWriter);
                    _memWriter.Flush();
                    audioData = _audioMemory.ToArray();
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
            try { _waveIn?.Dispose(); } catch { }
            _waveIn = null;
        }
    }
}
