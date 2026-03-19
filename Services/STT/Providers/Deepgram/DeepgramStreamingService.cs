using BF_STT.Models;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;

using BF_STT.Services.STT.Abstractions;

namespace BF_STT.Services.STT.Providers.Deepgram
{
    public class DeepgramStreamingService : BaseStreamingService
    {
        private readonly string _streamingUrl;
        private string _model;

        public DeepgramStreamingService(string apiKey, string streamingUrl, string model)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _streamingUrl = string.IsNullOrWhiteSpace(streamingUrl)
                ? "wss://api.deepgram.com/v1/listen"
                : streamingUrl;
            _model = string.IsNullOrWhiteSpace(model) ? "nova-3" : model;
        }

        public override void UpdateSettings(string apiKey, string model)
        {
            _apiKey = apiKey;
            if (!string.IsNullOrWhiteSpace(model)) _model = model;
        }

        protected override void ConfigureWebSocket(ClientWebSocket ws)
        {
            ws.Options.SetRequestHeader("Authorization", $"Token {_apiKey}");
        }

        protected override TimeSpan? KeepAliveInterval => TimeSpan.FromSeconds(5);

        protected override async Task SendKeepAliveFrameAsync()
        {
            await SendTextAsync("{\"type\":\"KeepAlive\"}");
            Debug.WriteLine("[DeepgramStreaming] Sent KeepAlive.");
        }

        /// <summary>
        /// Opens a WebSocket connection to Deepgram streaming API.
        /// </summary>
        public override async Task StartAsync(string language, CancellationToken ct = default)
        {
            if (IsConnected) return;

            // Build WebSocket URL with query parameters
            var url = $"{_streamingUrl}?model={_model}&language={language}" +
                      "&encoding=linear16&sample_rate=16000&channels=1" +
                      "&interim_results=true&smart_format=true&endpointing=300&utterance_end_ms=1000";

            try
            {
                await ConnectAsync(new Uri(url), CancellationToken.None);
                Debug.WriteLine("[DeepgramStreaming] Connected.");
            }
            catch (Exception ex)
            {
                IsConnected = false;
                FireError($"Connection failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Sends CloseStream message and waits for final results before closing.
        /// </summary>
        public override async Task StopAsync(CancellationToken ct = default)
        {
            if (!IsConnected || _webSocket == null) return;

            try
            {
                if (_webSocket.State == WebSocketState.Open)
                {
                    await SendTextAsync("{\"type\":\"CloseStream\"}");
                    Debug.WriteLine("[DeepgramStreaming] Sent CloseStream.");

                    // Wait for the receive loop to finish (server will close after sending final results)
                    await WaitForReceiveAsync(TimeSpan.FromSeconds(3));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DeepgramStreaming] Stop error: {ex.Message}");
            }
            finally
            {
                await CleanupAsync();
            }
        }

        protected override void ProcessMessage(string json)
        {
            try
            {
#if DEBUG
                Debug.WriteLine($"[DeepgramStreaming] RAW: {json}");
#endif

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var response = JsonSerializer.Deserialize<DeepgramStreamingResponse>(json, options);

                if (response == null) return;

                if (response.Type == "UtteranceEnd")
                {
                    Debug.WriteLine("[DeepgramStreaming] UtteranceEnd received.");
                    FireUtteranceEnd();
                    return;
                }

                // Only process "Results" type messages
                if (response.Type != "Results") return;

                var transcript = response.Channel?.Alternatives?.FirstOrDefault()?.Transcript;
                if (transcript == null) transcript = string.Empty;

                if (response.IsFinal && !string.IsNullOrWhiteSpace(transcript))
                {
                    var trimmed = transcript.TrimEnd();
                    if (trimmed.EndsWith("."))
                    {
                        transcript = trimmed + " ";
                    }
                    else
                    {
                        transcript = trimmed + ". ";
                    }
                }

#if DEBUG
                Debug.WriteLine($"[DeepgramStreaming] Type={response.Type} IsFinal={response.IsFinal} SpeechFinal={response.SpeechFinal} Text=\"{transcript}\"");
#endif

                FireTranscriptReceived(new TranscriptEventArgs
                {
                    Text = transcript,
                    IsFinal = response.IsFinal,
                    SpeechFinal = response.SpeechFinal
                });
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[DeepgramStreaming] JSON parse error: {ex.Message}");
            }
        }
    }
}
