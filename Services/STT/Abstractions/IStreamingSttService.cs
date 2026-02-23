using BF_STT.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BF_STT.Services.STT.Abstractions
{
    public interface IStreamingSttService : IDisposable
    {
        bool IsConnected { get; }

        event EventHandler<TranscriptEventArgs>? TranscriptReceived;
        event EventHandler? UtteranceEndReceived;
        event EventHandler<string>? Error;

        void UpdateSettings(string apiKey, string model);
        Task StartAsync(string language, CancellationToken ct = default);
        Task SendAudioAsync(byte[] buffer, int count, CancellationToken ct = default);
        Task StopAsync(CancellationToken ct = default);
        Task CancelAsync(CancellationToken ct = default);
    }
}
