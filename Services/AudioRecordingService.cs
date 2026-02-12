using NAudio.Wave;
using System.IO;

namespace BF_STT.Services
{
    public class AudioRecordingService : IDisposable
    {
        private WaveInEvent? _waveIn;
        private WaveFileWriter? _writer;
        private string? _currentFilePath;
        private bool _isRecording;
        private float _volumeMultiplier = 5.0f; // Default gain of 3x

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

            _waveIn = new WaveInEvent
            {
                DeviceNumber = 0,
                WaveFormat = new WaveFormat(16000, 16, 1) // 16kHz, 16-bit, Mono
            };

            _writer = new WaveFileWriter(_currentFilePath, _waveIn.WaveFormat);

            _waveIn.DataAvailable += (s, e) =>
            {
                if (_writer != null)
                {
                    float maxSample = 0;
                    // Apply Digital Gain
                    if (_volumeMultiplier != 1.0f)
                    {
                        for (int i = 0; i < e.BytesRecorded; i += 2)
                        {
                            short sample = BitConverter.ToInt16(e.Buffer, i);
                            float amplified = sample * _volumeMultiplier;

                            if (amplified > short.MaxValue) amplified = short.MaxValue;
                            else if (amplified < short.MinValue) amplified = short.MinValue;

                            short finalSample = (short)amplified;
                            
                            // For visualization, use the amplified value but keep it positive
                            float absSample = Math.Abs((float)finalSample / short.MaxValue);
                            if (absSample > maxSample) maxSample = absSample;

                            byte[] bytes = BitConverter.GetBytes(finalSample);
                            e.Buffer[i] = bytes[0];
                            e.Buffer[i + 1] = bytes[1];
                        }
                    }
                    else
                    {
                        for (int i = 0; i < e.BytesRecorded; i += 2)
                        {
                            short sample = BitConverter.ToInt16(e.Buffer, i);
                            float absSample = Math.Abs((float)sample / short.MaxValue);
                            if (absSample > maxSample) maxSample = absSample;
                        }
                    }

                    AudioLevelUpdated?.Invoke(this, maxSample);

                    _writer.Write(e.Buffer, 0, e.BytesRecorded);
                    if (_writer.Position > _writer.Length)
                        _writer.Flush();
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
            _waveIn?.StopRecording();
            
            // _isRecording will be set to false in OnRecordingStopped
            // Return the task that will be completed when recording actually stops
            return _stopRecordingTcs.Task;
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            _writer?.Dispose();
            _writer = null;
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
                _waveIn?.StopRecording();
            }
            // We can't await here, so we rely on the event handler to clean up or do it manually if needed.
            // However, _writer needs to be disposed.
            // If we are disposing, we should probably force clean up.

            _writer?.Dispose(); 
            _writer = null;
            _waveIn?.Dispose();
            _waveIn = null;
        }
    }
}
