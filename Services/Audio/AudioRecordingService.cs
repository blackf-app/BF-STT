using NAudio.Wave;
using System.IO;

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
        private IAudioCapture? _capture;
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

        private readonly AudioPipeline _pipeline = new AudioPipeline();

        private bool _hasAudio;
        private bool _hasSpeechContent;
        private float _maxPeakLevel;
        private const float SilenceThreshold = 0.01f;

        private bool _isSpeaking;
        private int _silenceFrameCount;
        private int _speechFrameCount;

        private const float VadSpeechThreshold = 0.02f;
        private const int VadSilenceFramesToPause = 10;
        private const int VadSpeechFramesToStart = 2;

        public bool EnableSilenceTrimming { get; set; }
        private bool _batchWritePaused;
        private int _batchSilenceFrameCount;
        private const int BatchSilenceFrameLimit = 30;

        private const int RingBufferCapacity = 4;
        private const int CrossfadeSamples = 80;
        private readonly PreSpeechRingBuffer _preSpeechBuffer =
            new PreSpeechRingBuffer(RingBufferCapacity, CrossfadeSamples);

        public event EventHandler<Exception?>? RecordingStopped;
        public event EventHandler<float>? AudioLevelUpdated;
        public event EventHandler<bool>? IsSpeakingChanged;
        public event EventHandler<AudioDataEventArgs>? AudioDataAvailable;

        public bool IsRecording => _isRecording;
        public bool IsSpeaking => _isSpeaking;

        public bool EnableNoiseSuppression { get; set; }

        public float VolumeMultiplier
        {
            get => _pipeline.TargetLevel;
            set => _pipeline.TargetLevel = value;
        }

        public int DeviceNumber { get; set; } = 0;

        private static IAudioCapture CreateCapture()
        {
#if WINDOWS
            return new WaveInCapture();
#else
            if (OperatingSystem.IsWindows())
            {
                // Forwarded reference still requires platform guard; on cross-target
                // builds the WaveInCapture is omitted from compilation. Fall back to
                // OpenAL even on Windows when WaveInCapture isn't compiled in.
                return new OpenAlCapture();
            }
            return new OpenAlCapture();
#endif
        }

        public void StartRecording()
        {
            if (_isRecording) return;

            Dispose();

            _hasAudio = false;
            _maxPeakLevel = 0;
            _hasSpeechContent = false;

            _isSpeaking = false;
            _silenceFrameCount = 0;
            _speechFrameCount = 0;

            _batchWritePaused = false;
            _batchSilenceFrameCount = 0;
            _preSpeechBuffer.Reset();

            _pipeline.Initialize(EnableNoiseSuppression);

            int sampleRate = EnableNoiseSuppression ? 48000 : 16000;

            _capture = CreateCapture();
            _capture.DeviceNumber = DeviceNumber;
            _capture.WaveFormat = new WaveFormat(sampleRate, 16, 1);
            _capture.BufferMilliseconds = 50;

            _waveFormat = new WaveFormat(16000, 16, 1);

            _audioMemory = new MemoryStream();
            _memWriter = new BinaryWriter(_audioMemory);
            var (mRiff, mData) = WriteWavHeader(_memWriter, _waveFormat);
            _memRiffPos = mRiff;
            _memDataPos = mData;

            if (SaveToFile)
            {
                try
                {
                    string fileName = $"bf_stt_{DateTime.Now:yyyyMMdd_HHmmss_fff}.wav";
                    _tempFilePath = Path.Combine(Path.GetTempPath(), fileName);
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

            _capture.DataAvailable += OnCaptureDataAvailable;
            _capture.CaptureStopped += OnCaptureStopped;
            _capture.StartCapture();
            _isRecording = true;
        }

        private (long riffPos, long dataPos) WriteWavHeader(BinaryWriter writer, WaveFormat format)
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            long riffPos = writer.BaseStream.Position;
            writer.Write(0);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)format.Channels);
            writer.Write(format.SampleRate);
            writer.Write(format.AverageBytesPerSecond);
            writer.Write((short)format.BlockAlign);
            writer.Write((short)format.BitsPerSample);

            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            long dataPos = writer.BaseStream.Position;
            writer.Write(0);
            return (riffPos, dataPos);
        }

        private void FinalizeWavHeader(BinaryWriter writer, long riffPos, long dataPos)
        {
            long totalDataBytes = writer.BaseStream.Length - dataPos - 4;

            writer.BaseStream.Position = dataPos;
            writer.Write((int)totalDataBytes);

            writer.BaseStream.Position = riffPos;
            writer.Write((int)(writer.BaseStream.Length - 8));

            writer.BaseStream.Position = writer.BaseStream.Length;
        }

        private void OnCaptureDataAvailable(object? sender, AudioFrameEventArgs e)
        {
            var (finalBuffer, finalBytesRecorded) = _pipeline.Process(e.Buffer, e.BytesRecorded);

            float maxSample = 0;
            double sumSquares = 0;
            int finalSampleCount = finalBytesRecorded / 2;
            for (int i = 0; i < finalSampleCount; i++)
            {
                short sample = BitConverter.ToInt16(finalBuffer, i * 2);
                float abs = Math.Abs((float)sample / short.MaxValue);
                if (abs > maxSample) maxSample = abs;
                sumSquares += abs * abs;
            }
            float currentFrameRms = (float)Math.Sqrt(sumSquares / finalSampleCount);

            if (maxSample > _maxPeakLevel) _maxPeakLevel = maxSample;
            if (_maxPeakLevel > SilenceThreshold) _hasAudio = true;

            if (currentFrameRms > VadSpeechThreshold)
            {
                _speechFrameCount++;
                _silenceFrameCount = 0;

                if (!_isSpeaking && _speechFrameCount >= VadSpeechFramesToStart)
                {
                    _isSpeaking = true;
                    _hasSpeechContent = true;
                    IsSpeakingChanged?.Invoke(this, true);
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
                }
            }

            AudioLevelUpdated?.Invoke(this, maxSample);

            void WriteToOutputs(byte[] buf, int offset, int count)
            {
                _memWriter?.Write(buf, offset, count);
                _fileWriter?.Write(buf, offset, count);
            }

            try
            {
                if (EnableSilenceTrimming)
                {
                    if (currentFrameRms > VadSpeechThreshold)
                    {
                        if (_batchWritePaused)
                        {
                            _preSpeechBuffer.Flush(_memWriter, _fileWriter);
                            _batchWritePaused = false;
                        }
                        _batchSilenceFrameCount = 0;
                        WriteToOutputs(finalBuffer, 0, finalBytesRecorded);
                    }
                    else
                    {
                        _batchSilenceFrameCount++;

                        if (_batchSilenceFrameCount <= BatchSilenceFrameLimit)
                        {
                            WriteToOutputs(finalBuffer, 0, finalBytesRecorded);
                        }
                        else
                        {
                            _batchWritePaused = true;
                            _preSpeechBuffer.Push(finalBuffer, finalBytesRecorded);
                        }
                    }
                }
                else
                {
                    WriteToOutputs(finalBuffer, 0, finalBytesRecorded);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error writing to output streams: {ex.Message}");
            }

            AudioDataAvailable?.Invoke(this, new AudioDataEventArgs(finalBuffer, finalBytesRecorded));
        }

        private bool _discardRecording;
        private TaskCompletionSource<byte[]?>? _stopRecordingTcs;

        public Task<byte[]?> StopRecordingAsync(bool discard = false)
        {
            if (!_isRecording) return Task.FromResult<byte[]?>(null);

            _discardRecording = discard;
            _stopRecordingTcs = new TaskCompletionSource<byte[]?>();

            try
            {
                _capture?.StopCapture();
            }
            catch (Exception)
            {
                _stopRecordingTcs.TrySetResult(null);
                return _stopRecordingTcs.Task;
            }

            return _stopRecordingTcs.Task;
        }

        private void OnCaptureStopped(object? sender, Exception? exception)
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
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error finalizing audio: {ex.Message}");
            }
            finally
            {
                try { _memWriter?.Dispose(); } catch { }
                _memWriter = null;
                try { _audioMemory?.Dispose(); } catch { }
                _audioMemory = null;

                try { _fileWriter?.Dispose(); } catch { }
                _fileWriter = null;
                try { _audioFile?.Dispose(); } catch { }
                _audioFile = null;
            }

            _capture?.Dispose();
            _capture = null;
            _isRecording = false;

            if (exception != null)
                _stopRecordingTcs?.TrySetException(exception);
            else
                _stopRecordingTcs?.TrySetResult(_discardRecording ? null : audioData);

            RecordingStopped?.Invoke(this, exception);
        }

        public bool HasMeaningfulAudio() =>
            _hasSpeechContent || (_hasAudio && _speechFrameCount > 0);

        public void Dispose()
        {
            if (_isRecording)
            {
                try { _capture?.StopCapture(); } catch { }
            }

            try { _memWriter?.Dispose(); } catch { }
            _memWriter = null;
            try { _audioMemory?.Dispose(); } catch { }
            _audioMemory = null;

            try { _fileWriter?.Dispose(); } catch { }
            _fileWriter = null;
            try { _audioFile?.Dispose(); } catch { }
            _audioFile = null;

            try { _capture?.Dispose(); } catch { }
            _capture = null;
            _pipeline.Dispose();
        }
    }
}
