using NAudio.Wave;
using System;
using System.IO;
using System.Threading.Tasks;

namespace BF_STT.Services
{
    public class AudioRecordingService : IDisposable
    {
        private WaveInEvent? _waveIn;
        private WaveFileWriter? _writer;
        private string? _currentFilePath;
        private bool _isRecording;
        
        // Audio processing state
        private float _volumeMultiplier = 1.0f; // Default 1.0 to let Deepgram AGC handle levels
        private float _prevSample = 0;
        private float _prevHpfOutput = 0;
        private const float HpfAlpha = 0.97f; // High-pass filter alpha for ~80Hz cutoff at 16kHz
        private const float SoftClipThreshold = 0.95f; 

        public event EventHandler<StoppedEventArgs>? RecordingStopped;
        public event EventHandler<float>? AudioLevelUpdated;

        public bool IsRecording => _isRecording;

        public float VolumeMultiplier
        {
            get => _volumeMultiplier;
            set => _volumeMultiplier = value;
        }

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

            _waveIn = new WaveInEvent
            {
                DeviceNumber = 0,
                WaveFormat = new WaveFormat(16000, 16, 1), // 16kHz, 16-bit, Mono
                BufferMilliseconds = 50, // balanced latency/stability
                NumberOfBuffers = 3
            };

            _writer = new WaveFileWriter(_currentFilePath, _waveIn.WaveFormat);

            _waveIn.DataAvailable += (s, e) =>
            {
                if (_writer != null)
                {
                    float maxSample = 0;
                    
                    // Process samples in-place
                    // Note: We are modifying the buffer content before writing to file
                    for (int i = 0; i < e.BytesRecorded; i += 2)
                    {
                        // 1. Convert to float
                        short rawSample = BitConverter.ToInt16(e.Buffer, i);
                        float sample = rawSample;

                        // 2. High-Pass Filter (DC Offset Removal & Rumble Filter)
                        // y[i] = α * (y[i-1] + x[i] - x[i-1])
                        float hpfOut = HpfAlpha * (_prevHpfOutput + sample - _prevSample);
                        
                        // Update filter state
                        _prevSample = sample;
                        _prevHpfOutput = hpfOut;

                        // 3. Apply Gain
                        float amplified = hpfOut * _volumeMultiplier;

                        // 4. Soft Clipping (Tanh Limiter)
                        // Prevents hard digital distortion if gain is too high
                        float normalized = amplified / short.MaxValue;
                        
                        // Apply soft clip: y = tanh(x)
                        // We use a simple approximation if performance is an issue, but Math.Tanh is fine for 16kHz mono
                        float softClipped = (float)Math.Tanh(normalized); 
                        
                        // Scale back to short range
                        // Multiplying by slightly less than MaxValue prevents boundary issues
                        float finalFloat = softClipped * short.MaxValue;

                        // Clamp strictly to short range just in case
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

                    AudioLevelUpdated?.Invoke(this, maxSample);

                    _writer.Write(e.Buffer, 0, e.BytesRecorded);
                }
            };

            _waveIn.RecordingStopped += OnRecordingStopped;

            _waveIn.StartRecording();
            _isRecording = true;
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
                _writer?.Flush();
                _writer?.Dispose();
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
