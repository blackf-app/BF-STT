using NAudio.Wave;
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

        // Audio processing pipeline (HPF, AGC, soft clip, NS, resampling)
        private readonly AudioPipeline _pipeline = new AudioPipeline();

        // Silent / speech detection state
        private bool _hasAudio;
        private bool _hasSpeechContent;
        private float _maxPeakLevel;
        private const float SilenceThreshold = 0.01f;

        // VAD (Voice Activity Detection) state
        private bool _isSpeaking;
        private int _silenceFrameCount;
        private int _speechFrameCount;

        // VAD constants
        private const float VadSpeechThreshold = 0.02f;
        private const int VadSilenceFramesToPause = 10;
        private const int VadSpeechFramesToStart = 2;

        // Batch silence trimming
        public bool EnableSilenceTrimming { get; set; }
        private bool _batchWritePaused;
        private int _batchSilenceFrameCount;
        private const int BatchSilenceFrameLimit = 30; // 1.5 s / 50 ms = 30 frames

        // Pre-speech ring buffer (~200 ms = 4 × 50 ms frames)
        private const int RingBufferCapacity = 4;
        private const int CrossfadeSamples = 80; // ~5 ms at 16 kHz
        private readonly PreSpeechRingBuffer _preSpeechBuffer =
            new PreSpeechRingBuffer(RingBufferCapacity, CrossfadeSamples);

        public event EventHandler<StoppedEventArgs>? RecordingStopped;
        public event EventHandler<float>? AudioLevelUpdated;
        public event EventHandler<bool>? IsSpeakingChanged;
        /// <summary>Fired with processed PCM audio data ready to send to the streaming API.</summary>
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

        /// <summary>
        /// Starts recording. Writes audio to an in-memory WAV buffer
        /// AND fires <see cref="AudioDataAvailable"/> events (for streaming/buffering).
        /// </summary>
        public void StartRecording()
        {
            if (_isRecording) return;

            if (WaveIn.DeviceCount == 0)
                throw new InvalidOperationException("No microphone detected.");

            // Clean up any previous recording
            Dispose();

            // Reset detection state
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
            _preSpeechBuffer.Reset();

            // Initialise audio processing pipeline
            _pipeline.Initialize(EnableNoiseSuppression);

            int sampleRate = EnableNoiseSuppression ? 48000 : 16000;

            _waveIn = new WaveInEvent
            {
                DeviceNumber = DeviceNumber,
                WaveFormat = new WaveFormat(sampleRate, 16, 1),
                BufferMilliseconds = 50,
                NumberOfBuffers = 3
            };

            _waveFormat = new WaveFormat(16000, 16, 1); // output is always 16 kHz

            // In-memory WAV buffer
            _audioMemory = new MemoryStream();
            _memWriter = new BinaryWriter(_audioMemory);
            var (mRiff, mData) = WriteWavHeader(_memWriter, _waveFormat);
            _memRiffPos = mRiff;
            _memDataPos = mData;

            // Optional file buffer for Test Mode
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

            _waveIn.DataAvailable += OnWaveInDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;
            _waveIn.StartRecording();
            _isRecording = true;
        }

        // ── WAV header helpers ───────────────────────────────────────────────────

        private (long riffPos, long dataPos) WriteWavHeader(BinaryWriter writer, WaveFormat format)
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            long riffPos = writer.BaseStream.Position;
            writer.Write(0); // placeholder
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
            writer.Write(0); // placeholder
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

        // ── Core audio callback ──────────────────────────────────────────────────

        private void OnWaveInDataAvailable(object? sender, WaveInEventArgs e)
        {
            // 1. Run HPF → AGC → soft clip → optional NS + resample
            var (finalBuffer, finalBytesRecorded) = _pipeline.Process(e.Buffer, e.BytesRecorded);

            // 2. Compute peak and RMS for VAD / level meter
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

            // Track peak for silence detection
            if (maxSample > _maxPeakLevel) _maxPeakLevel = maxSample;
            if (_maxPeakLevel > SilenceThreshold) _hasAudio = true;

            // 3. VAD logic
            if (currentFrameRms > VadSpeechThreshold)
            {
                _speechFrameCount++;
                _silenceFrameCount = 0;

                if (!_isSpeaking && _speechFrameCount >= VadSpeechFramesToStart)
                {
                    _isSpeaking = true;
                    _hasSpeechContent = true;
                    IsSpeakingChanged?.Invoke(this, true);
                    System.Diagnostics.Debug.WriteLine(
                        $"[AudioRecording] VAD: Speech Started (RMS: {currentFrameRms:F4}, Peak: {maxSample:F4})");
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

            // 4. Write to output streams (with optional silence trimming)
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

            // 5. Fire event for streaming
            AudioDataAvailable?.Invoke(this, new AudioDataEventArgs(finalBuffer, finalBytesRecorded));
        }

        // ── Stop / finalise ──────────────────────────────────────────────────────

        private bool _discardRecording;
        private TaskCompletionSource<byte[]?>? _stopRecordingTcs;

        /// <summary>
        /// Stops recording and returns the complete WAV audio data as a byte array.
        /// Returns null if <paramref name="discard"/> is true.
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
                    System.Diagnostics.Debug.WriteLine(
                        $"[AudioRecording] Test Mode: Saved recording to {_tempFilePath}");
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

            _waveIn?.Dispose();
            _waveIn = null;
            _isRecording = false;

            if (e.Exception != null)
                _stopRecordingTcs?.TrySetException(e.Exception);
            else
                _stopRecordingTcs?.TrySetResult(_discardRecording ? null : audioData);

            RecordingStopped?.Invoke(this, e);
        }

        /// <summary>
        /// Returns true if the last recording contained meaningful audio above the silence threshold.
        /// Must be called after <see cref="StopRecordingAsync"/> completes.
        /// </summary>
        public bool HasMeaningfulAudio() =>
            _hasSpeechContent || (_hasAudio && _speechFrameCount > 0);

        // ── IDisposable ──────────────────────────────────────────────────────────

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
            _pipeline.Dispose();
        }
    }
}
