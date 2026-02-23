using BF_STT.Models;

namespace BF_STT.Services.STT.Abstractions
{
    /// <summary>
    /// A no-op streaming service for providers that only support batch mode.
    /// Fires an error on StartAsync to inform the user that streaming is not available.
    /// </summary>
    internal sealed class NullStreamingService : IStreamingSttService
    {
        public static readonly NullStreamingService Instance = new();

        public bool IsConnected => false;

        public event EventHandler<TranscriptEventArgs>? TranscriptReceived;
        public event EventHandler? UtteranceEndReceived;
        public event EventHandler<string>? Error;

        public void UpdateSettings(string apiKey, string model) { }

        public Task StartAsync(string language, CancellationToken ct = default)
        {
            Error?.Invoke(this, "This provider does not support streaming mode.");
            return Task.CompletedTask;
        }

        public Task SendAudioAsync(byte[] buffer, int count, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task StopAsync(CancellationToken ct = default)
            => Task.CompletedTask;

        public Task CancelAsync(CancellationToken ct = default)
            => Task.CompletedTask;

        public void Dispose() { }
    }
}
