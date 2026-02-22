namespace BF_STT.Services.States
{
    /// <summary>
    /// BatchRecording state — recording audio in batch/toggle mode.
    /// Second hotkey press stops recording and sends to batch API.
    /// </summary>
    public class BatchRecordingState : IRecordingState
    {
        public static readonly BatchRecordingState Instance = new();
        public RecordingStateEnum StateId => RecordingStateEnum.BatchRecording;

        public void HandleHotkeyDown(RecordingCoordinator ctx, bool autoSend)
        {
            // Second press stops batch recording
            if (autoSend) ctx.ShouldAutoSend = true;
            ctx.StopAndProcessBatch();
        }

        public void HandleHotkeyUp(RecordingCoordinator ctx) { /* No-op in Batch */ }
        public void HandleHybridTimeout(RecordingCoordinator ctx) { /* No-op in Batch */ }

        public void HandleStartButton(RecordingCoordinator ctx)
        {
            // Cancel current recording
            ctx.CancelRecording();
        }

        public void HandleAudioData(RecordingCoordinator ctx, AudioDataEventArgs e)
        {
            // In batch mode, audio is being written to memory by AudioRecordingService.
            // No additional routing needed here.
        }
    }
}
