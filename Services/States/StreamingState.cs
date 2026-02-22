using System.Diagnostics;

namespace BF_STT.Services.States
{
    /// <summary>
    /// Streaming state — audio is being sent live to the streaming API.
    /// Key release stops streaming and finalizes.
    /// </summary>
    public class StreamingState : IRecordingState
    {
        public static readonly StreamingState Instance = new();
        public RecordingStateEnum StateId => RecordingStateEnum.Streaming;

        public void HandleHotkeyDown(RecordingCoordinator ctx, bool autoSend)
        {
            // Ignore repeated KeyDown while streaming
        }

        public void HandleHotkeyUp(RecordingCoordinator ctx)
        {
            // Release stops streaming
            ctx.StopStreamingAndFinalize();
        }

        public void HandleHybridTimeout(RecordingCoordinator ctx) { /* No-op in Streaming */ }

        public void HandleStartButton(RecordingCoordinator ctx)
        {
            // Cancel streaming
            ctx.CancelRecording();
        }

        public async void HandleAudioData(RecordingCoordinator ctx, AudioDataEventArgs e)
        {
            if (!ctx.AudioService.IsSpeaking) return;

            try
            {
                if (ctx.IsTestMode)
                {
                    var sendTasks = ctx.Registry.GetAllProviders()
                        .Select(p => p.StreamingService.SendAudioAsync(e.Buffer, e.BytesRecorded));
                    await Task.WhenAll(sendTasks);
                }
                else
                {
                    await ctx.Registry.GetStreamingService(ctx.StreamingModeApi)
                        .SendAudioAsync(e.Buffer, e.BytesRecorded);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StreamingState] Audio send error: {ex.Message}");
            }
        }
    }
}
