using BF_STT.Models;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;

using BF_STT.Services.STT.Abstractions;

namespace BF_STT.Services.STT.Providers.ElevenLabs
{
    public class ElevenLabsStreamingService : BaseStreamingService
    {
        private readonly string _streamingUrl;
        private string _model;

        public ElevenLabsStreamingService(string apiKey, string streamingUrl, string model)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _streamingUrl = string.IsNullOrWhiteSpace(streamingUrl)
                ? "wss://api.elevenlabs.io/v1/speech-to-text/streaming"
                : streamingUrl;
            _model = string.IsNullOrWhiteSpace(model) ? "scribe_v2_realtime" : model;
        }

        public override void UpdateSettings(string apiKey, string model)
        {
            _apiKey = apiKey;
            if (!string.IsNullOrWhiteSpace(model)) _model = model;
        }

        protected override void ConfigureWebSocket(ClientWebSocket ws)
        {
            ws.Options.SetRequestHeader("xi-api-key", _apiKey);
        }

        protected override TimeSpan? KeepAliveInterval => TimeSpan.FromSeconds(5);

        protected override async Task SendKeepAliveFrameAsync()
        {
            await SendTextAsync("{\"type\":\"ping\"}");
            Debug.WriteLine("[ElevenLabsStreaming] Sent ping.");
        }

        /// <summary>
        /// Opens a WebSocket connection to ElevenLabs streaming STT API.
        /// </summary>
        public override async Task StartAsync(string language, CancellationToken ct = default)
        {
            if (IsConnected) return;

            // Build WebSocket URL with query parameters
            var url = $"{_streamingUrl}?model_id={_model}&language_code={language}" +
                      "&encoding=pcm_s16le&sample_rate=16000";

            try
            {
                await ConnectAsync(new Uri(url), CancellationToken.None);
                Debug.WriteLine("[ElevenLabsStreaming] Connected.");
            }
            catch (Exception ex)
            {
                IsConnected = false;
                FireError($"Connection failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Sends EOS (end of stream) message and waits for final results before closing.
        /// </summary>
        public override async Task StopAsync(CancellationToken ct = default)
        {
            if (!IsConnected || _webSocket == null) return;

            try
            {
                if (_webSocket.State == WebSocketState.Open)
                {
                    await SendTextAsync("{\"type\":\"EOS\"}");
                    Debug.WriteLine("[ElevenLabsStreaming] Sent EOS.");

                    // Wait for the receive loop to finish
                    await WaitForReceiveAsync(TimeSpan.FromSeconds(3));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ElevenLabsStreaming] Stop error: {ex.Message}");
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
                Debug.WriteLine($"[ElevenLabsStreaming] RAW: {json}");
#endif

                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                // Check message type
                if (root.TryGetProperty("type", out var typeElement))
                {
                    var type = typeElement.GetString();

                    if (type == "utterance_end" || type == "eos")
                    {
                        Debug.WriteLine("[ElevenLabsStreaming] UtteranceEnd/EOS received.");
                        FireUtteranceEnd();
                        return;
                    }

                    if (type == "pong" || type == "ping")
                    {
                        return; // Ignore keep-alive responses
                    }
                }

                // Extract transcript text
                var transcript = string.Empty;
                var isFinal = false;

                if (root.TryGetProperty("text", out var textElement))
                {
                    transcript = textElement.GetString() ?? string.Empty;
                }

                if (root.TryGetProperty("is_final", out var isFinalElement))
                {
                    isFinal = isFinalElement.GetBoolean();
                }

                // Format final transcripts with period
                if (isFinal && !string.IsNullOrWhiteSpace(transcript))
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
                Debug.WriteLine($"[ElevenLabsStreaming] IsFinal={isFinal} Text=\"{transcript}\"");
#endif

                FireTranscriptReceived(new TranscriptEventArgs
                {
                    Text = transcript,
                    IsFinal = isFinal,
                    SpeechFinal = isFinal
                });
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[ElevenLabsStreaming] JSON parse error: {ex.Message}");
            }
        }
    }
}
