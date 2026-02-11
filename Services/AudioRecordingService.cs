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

        public event EventHandler<StoppedEventArgs>? RecordingStopped;

        public bool IsRecording => _isRecording;

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
