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
        private WaveFileWriter? _writer;
        private string? _currentFilePath;
        private bool _isRecording;
        
        // Audio processing state
        private float _volumeMultiplier = 3f; // Default 1.0 to let Deepgram AGC handle levels
        private float _prevSample = 0;
        private float _prevHpfOutput = 0;
        private const float HpfAlpha = 0.97f; // High-pass filter alpha for ~80Hz cutoff at 16kHz
        private const float SoftClipThreshold = 0.95f;
        
        // Silent detection
        private bool _hasAudio;
        private float _maxPeakLevel;
        private const float SilenceThreshold = 0.01f; // ~-40dB peak threshold
        
        // VAD (Voice Activity Detection) state
        private bool _isSpeaking;
        private int _silenceFrameCount;
        private int _speechFrameCount;
        
        // VAD Constants
        private const float VadSpeechThreshold = 0.02f; // ~-34dB energy threshold
        private const int VadSilenceFramesToPause = 10; // 10 frames * 50ms = 500ms
        private const int VadSpeechFramesToStart = 2;   // 2 frames * 50ms = 100ms

        public event EventHandler<StoppedEventArgs>? RecordingStopped;
        public event EventHandler<float>? AudioLevelUpdated;
        public event EventHandler<bool>? IsSpeakingChanged;
        /// <summary>
        /// Fired with processed PCM audio data ready to send to Deepgram.
        /// </summary>
        public event EventHandler<AudioDataEventArgs>? AudioDataAvailable;

        public bool IsRecording => _isRecording;
        public bool IsSpeaking => _isSpeaking;

        public float VolumeMultiplier
        {
            get => _volumeMultiplier;
            set => _volumeMultiplier = value;
        }

        /// <summary>
        /// Starts recording. Always writes to a temporary WAV file (for batch mode) 
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

            _currentFilePath = Path.Combine(Path.GetTempPath(), $"bf_stt_{Guid.NewGuid()}.wav");
            
            // Reset filter state
            _prevSample = 0;
            _prevHpfOutput = 0;
            
            // Reset silent detection
            _hasAudio = false;
            _maxPeakLevel = 0;
            
            // Reset VAD
            _isSpeaking = false;
            _silenceFrameCount = 0;
            _speechFrameCount = 0;

            _waveIn = new WaveInEvent
            {
                DeviceNumber = 0,
                WaveFormat = new WaveFormat(16000, 16, 1), // 16kHz, 16-bit, Mono
                BufferMilliseconds = 50, // balanced latency/stability
                NumberOfBuffers = 3
            };

            _writer = new WaveFileWriter(_currentFilePath, _waveIn.WaveFormat);

            _waveIn.DataAvailable += OnWaveInDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;

            _waveIn.StartRecording();
            _isRecording = true;
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

            // 1. Write to WAV file (always, in case we decide to use Batch mode)
            if (_writer != null)
            {
                try
                {
                     _writer.Write(e.Buffer, 0, e.BytesRecorded);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error writing to wave file: {ex.Message}");
                }
            }

            // 2. Fire event (always, effectively buffering if no subscribers yet, or streaming if subscribed)
            // Make a copy of the buffer to avoid race conditions
            var copy = new byte[e.BytesRecorded];
            Array.Copy(e.Buffer, copy, e.BytesRecorded);
            AudioDataAvailable?.Invoke(this, new AudioDataEventArgs(copy, e.BytesRecorded));
        }

        private bool _discardRecording;
        private TaskCompletionSource<string>? _stopRecordingTcs;

        public Task<string> StopRecordingAsync(bool discard = false)
        {
            if (!_isRecording) return Task.FromResult(string.Empty);

            _discardRecording = discard;
            _stopRecordingTcs = new TaskCompletionSource<string>();
            
            // StopRecording() is non-blocking but triggers the RecordingStopped event
            try 
            {
                _waveIn?.StopRecording();
            }
            catch (Exception)
            {
                // In case device was disconnected or other error
                _stopRecordingTcs.TrySetResult(string.Empty);
                return _stopRecordingTcs.Task;
            }
            
            return _stopRecordingTcs.Task;
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            // Flush and dispose writer ensures all data is written
            try 
            {
                if (_writer != null)
                {
                    _writer.Flush();
                    _writer.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error closing wave writer: {ex.Message}");
            }
            finally
            {
                _writer = null;
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
                string resultPath = _currentFilePath ?? string.Empty;
                
                if (_discardRecording)
                {
                    try
                    {
                        if (File.Exists(resultPath))
                        {
                            File.Delete(resultPath);
                        }
                    }
                    catch { /* Ignore delete errors */ }
                    resultPath = string.Empty;
                }

                _stopRecordingTcs?.TrySetResult(resultPath);
            }

            RecordingStopped?.Invoke(this, e);
        }

        /// <summary>
        /// Call this when switching to Streaming mode to discard the WAV file being written,
        /// but keep the recording session active (so we don't lose audio context).
        /// Actually, since we can just discard the file at the end using StopRecordingAsync(discard: true),
        /// we don't strictly need a special method here.
        /// But if we want to stop writing to disk to save I/O during streaming, we could close the writer early.
        /// For simplicity and robustness, we'll keep writing to file until Stop.
        /// </summary>
        public void DiscardCurrentRecordingFile()
        {
             _discardRecording = true;
        }

        /// <summary>
        /// Returns true if the last recording contained meaningful audio above the silence threshold.
        /// Must be called after StopRecordingAsync completes.
        /// </summary>
        public bool HasMeaningfulAudio()
        {
            return _hasAudio;
        }

        public void Dispose()
        {
            if (_isRecording)
            {
                try { _waveIn?.StopRecording(); } catch { }
            }
            
            try { _writer?.Dispose(); } catch { }
            _writer = null;
            try { _waveIn?.Dispose(); } catch { }
            _waveIn = null;
        }
    }
}
