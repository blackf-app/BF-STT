namespace BF_STT.Services.States
{
    /// <summary>
    /// Idle state — not recording, not processing.
    /// Responds to hotkey/button to start recording.
    /// </summary>
    public class IdleState : IRecordingState
    {
        public static readonly IdleState Instance = new();
        public RecordingStateEnum StateId => RecordingStateEnum.Idle;

        public void HandleHotkeyDown(RecordingCoordinator ctx, bool autoSend)
        {
            ctx.BeginNewSession(autoSend);
            ctx.StartAudioCapture(isKeyTrigger: true);
        }

        public void HandleHotkeyUp(RecordingCoordinator ctx) { /* No-op in Idle */ }
        public void HandleHybridTimeout(RecordingCoordinator ctx) { /* No-op in Idle */ }

        public void HandleStartButton(RecordingCoordinator ctx)
        {
            ctx.BeginNewSession(autoSend: false);
            ctx.StartAudioCapture(isKeyTrigger: false);
        }

        public void HandleAudioData(RecordingCoordinator ctx, AudioDataEventArgs e) { /* No-op in Idle */ }
    }
}
