using BF_STT.Models;
using System.Diagnostics;
using System.Threading.Tasks;
using System;

using BF_STT.Services.STT.Abstractions;

namespace BF_STT.Services.STT.Providers.OpenAI
{
    // Note: The standard OpenAI Whisper API does NOT support WebSockets/Streaming currently via their REST API natively
    // for standard audio files without generating continuous chunks or using the new Realtime logic which requires different models 
    // and complex SDP setups. 
    // For this implementation, since IBatchSttService is standard, we'll create a mock/fallback for streaming
    // that won't do anything or returns an error indicating it's not supported natively, to keep interfaces clean.
    public class OpenAIStreamingService : IStreamingSttService, IDisposable
    {
        private string _apiKey;
        private bool _isConnected;

        public event EventHandler<TranscriptEventArgs>? TranscriptReceived;
        public event EventHandler? UtteranceEndReceived;
        public event EventHandler<string>? Error;

        public bool IsConnected => _isConnected;

        public OpenAIStreamingService(string apiKey)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        }

        public void UpdateSettings(string apiKey, string model)
        {
            _apiKey = apiKey;
        }

        public Task StartAsync(string language, CancellationToken ct = default)
        {
            _isConnected = true;
            // Native Open AI doesn't support streaming websockets for whisper in this simple way yet.
            // A more complex workaround using WebRTC / Realtime API would be needed.
            Error?.Invoke(this, "Standard OpenAI Whisper does not support WebSocket streaming natively yet.");
            return Task.CompletedTask;
        }

        public Task SendAudioAsync(byte[] buffer, int count, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct = default)
        {
            _isConnected = false;
            return Task.CompletedTask;
        }

        public Task CancelAsync(CancellationToken ct = default)
        {
            _isConnected = false;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _isConnected = false;
        }
    }
}
