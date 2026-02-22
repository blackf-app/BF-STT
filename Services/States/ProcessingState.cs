namespace BF_STT.Services.States
{
    /// <summary>
    /// Processing state — batch API call is in progress.
    /// Allows starting a new recording while the previous batch processes in the background.
    /// </summary>
    public class ProcessingState : IRecordingState
    {
        public static readonly ProcessingState Instance = new();
        public RecordingStateEnum StateId => RecordingStateEnum.Processing;

        public void HandleHotkeyDown(RecordingCoordinator ctx, bool autoSend)
        {
            // Allow starting a new recording while batch processes in background
            ctx.BeginNewSession(autoSend);
            ctx.StartAudioCapture(isKeyTrigger: true);
        }

        public void HandleHotkeyUp(RecordingCoordinator ctx) { /* No-op */ }
        public void HandleHybridTimeout(RecordingCoordinator ctx) { /* No-op */ }

        public void HandleStartButton(RecordingCoordinator ctx)
        {
            // Allow starting a new recording via button while batch processes
            ctx.BeginNewSession(autoSend: false);
            ctx.StartAudioCapture(isKeyTrigger: false);
        }

        public void HandleAudioData(RecordingCoordinator ctx, AudioDataEventArgs e) { /* Ignore while processing */ }
    }
}
