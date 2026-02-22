namespace BF_STT.Services.States
{
    /// <summary>
    /// Represents the possible states of the recording coordinator.
    /// </summary>
    public enum RecordingStateEnum
    {
        /// <summary>Not recording, not processing.</summary>
        Idle,

        /// <summary>Recording started via hotkey, waiting for hybrid decision (&lt; 300ms).</summary>
        HybridPending,

        /// <summary>Decided on batch mode (toggle), recording audio.</summary>
        BatchRecording,

        /// <summary>Decided on streaming mode, audio being sent live.</summary>
        Streaming,

        /// <summary>Recording stopped, batch API call in progress.</summary>
        Processing,

        /// <summary>Error occurred — auto-recovers to Idle.</summary>
        Failed
    }
}
