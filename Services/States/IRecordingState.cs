namespace BF_STT.Services.States
{
    /// <summary>
    /// Interface for the State Pattern. Each state handles
    /// hotkey events, audio data, and button actions differently.
    /// </summary>
    public interface IRecordingState
    {
        RecordingStateEnum StateId { get; }

        void HandleHotkeyDown(RecordingCoordinator ctx, bool autoSend);
        void HandleHotkeyUp(RecordingCoordinator ctx);
        void HandleHybridTimeout(RecordingCoordinator ctx);
        void HandleStartButton(RecordingCoordinator ctx);
        void HandleAudioData(RecordingCoordinator ctx, AudioDataEventArgs e);
    }
}
