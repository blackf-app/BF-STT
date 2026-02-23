using BF_STT.Services.Audio;
using BF_STT.Services.Workflow;

namespace BF_STT.Services.Workflow.States
{
    /// <summary>
    /// Failed state — error occurred, auto-recovers to Idle.
    /// </summary>
    public class FailedState : IRecordingState
    {
        public static readonly FailedState Instance = new();
        public RecordingStateEnum StateId => RecordingStateEnum.Failed;

        public void HandleHotkeyDown(RecordingCoordinator ctx, bool autoSend)
        {
            // Allow recovery — start fresh
            ctx.TransitionTo(IdleState.Instance);
            ctx.CurrentState.HandleHotkeyDown(ctx, autoSend);
        }

        public void HandleHotkeyUp(RecordingCoordinator ctx) { /* No-op */ }
        public void HandleHybridTimeout(RecordingCoordinator ctx) { /* No-op */ }

        public void HandleStartButton(RecordingCoordinator ctx)
        {
            // Allow recovery — start fresh
            ctx.TransitionTo(IdleState.Instance);
            ctx.CurrentState.HandleStartButton(ctx);
        }

        public void HandleAudioData(RecordingCoordinator ctx, AudioDataEventArgs e) { /* No-op */ }
    }
}
