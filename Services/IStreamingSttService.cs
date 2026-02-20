using BF_STT.Models;
using System;
using System.Threading.Tasks;

namespace BF_STT.Services
{
    public interface IStreamingSttService : IDisposable
    {
        bool IsConnected { get; }

        event EventHandler<TranscriptEventArgs>? TranscriptReceived;
        event EventHandler? UtteranceEndReceived;
        event EventHandler<string>? Error;

        void UpdateSettings(string apiKey, string model);
        Task StartAsync(string language);
        Task SendAudioAsync(byte[] buffer, int count);
        Task StopAsync();
        Task CancelAsync();
    }
}
