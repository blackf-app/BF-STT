namespace BF_STT.Services.States
{
    /// <summary>
    /// HybridPending state — recording started via hotkey, buffering audio
    /// until we decide batch vs streaming (threshold: 300ms).
    /// </summary>
    public class HybridPendingState : IRecordingState
    {
        public static readonly HybridPendingState Instance = new();
        public RecordingStateEnum StateId => RecordingStateEnum.HybridPending;

        public void HandleHotkeyDown(RecordingCoordinator ctx, bool autoSend)
        {
            // Already recording — cancel
            ctx.CancelRecording();
        }

        public void HandleHotkeyUp(RecordingCoordinator ctx)
        {
            // Short press → switch to Batch (Toggle) mode
            var duration = DateTime.Now - ctx.HotkeyDownTime;
            if (duration.TotalMilliseconds < RecordingCoordinator.HybridThresholdMs)
            {
                ctx.StopHybridTimer();
                ctx.ClearAudioBuffer();
                ctx.PlayStartSound();
                ctx.FireStatusChanged("Recording (Batch)...");
                ctx.TransitionTo(BatchRecordingState.Instance);
            }
        }

        public void HandleHybridTimeout(RecordingCoordinator ctx)
        {
            // Long press → switch to Streaming mode
            ctx.EnterStreamingMode();
        }

        public void HandleStartButton(RecordingCoordinator ctx)
        {
            // Cancel
            ctx.CancelRecording();
        }

        public void HandleAudioData(RecordingCoordinator ctx, AudioDataEventArgs e)
        {
            // Buffer audio for potential flush to streaming
            ctx.EnqueueAudioBuffer(e);
        }
    }
}
