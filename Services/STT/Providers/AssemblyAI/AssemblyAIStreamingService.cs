using BF_STT.Models;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text.Json;

using BF_STT.Services.STT.Abstractions;

namespace BF_STT.Services.STT.Providers.AssemblyAI
{
    public class AssemblyAIStreamingService : BaseStreamingService
    {
        private readonly string _streamingUrl;
        private string _model;

        public AssemblyAIStreamingService(string apiKey, string streamingUrl, string model)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _streamingUrl = string.IsNullOrWhiteSpace(streamingUrl)
                ? "wss://streaming.assemblyai.com/v3/ws"
                : streamingUrl;
            _model = ResolveStreamingModel(string.IsNullOrWhiteSpace(model) ? "universal-streaming-english" : model);
        }

        /// <summary>
        /// Maps batch/legacy model names to valid streaming model names.
        /// Valid streaming models: universal-streaming-english, universal-streaming-multilingual, u3-rt-pro
        /// </summary>
        private static string ResolveStreamingModel(string model)
        {
            return model.ToLowerInvariant() switch
            {
                "best" or "universal-3-pro" => "universal-streaming-english",
                "nano" or "universal-2" => "universal-streaming-multilingual",
                _ => model
            };
        }

        public override void UpdateSettings(string apiKey, string model)
        {
            _apiKey = apiKey;
            if (!string.IsNullOrWhiteSpace(model)) _model = ResolveStreamingModel(model);
        }

        protected override void ConfigureWebSocket(ClientWebSocket ws)
        {
            ws.Options.SetRequestHeader("Authorization", _apiKey);
        }

        protected override TimeSpan? KeepAliveInterval => TimeSpan.FromSeconds(5);

        protected override async Task SendKeepAliveFrameAsync()
        {
            if (_webSocket?.State != WebSocketState.Open) return;
            await _webSocket.SendAsync(new ArraySegment<byte>(Array.Empty<byte>()),
                WebSocketMessageType.Binary, true, CancellationToken.None);
            Debug.WriteLine("[AssemblyAIStreaming] Sent KeepAlive.");
        }

        public override async Task StartAsync(string language, CancellationToken ct = default)
        {
            if (IsConnected) return;

            // Auto-select multilingual model for non-English languages
            var isEnglish = string.IsNullOrWhiteSpace(language) || language.Equals("en", StringComparison.OrdinalIgnoreCase);
            var effectiveModel = isEnglish ? _model : "universal-streaming-multilingual";

            // Build WebSocket URL with query parameters
            var url = $"{_streamingUrl}?encoding=pcm_s16le&sample_rate=16000&speech_model={effectiveModel}";

            try
            {
                await ConnectAsync(new Uri(url), CancellationToken.None);
                Debug.WriteLine("[AssemblyAIStreaming] Connected.");
            }
            catch (Exception ex)
            {
                IsConnected = false;
                FireError($"Connection failed: {ex.Message}");
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken ct = default)
        {
            if (!IsConnected || _webSocket == null) return;

            try
            {
                if (_webSocket.State == WebSocketState.Open)
                {
                    // Send end_of_audio message
                    await SendTextAsync("{\"type\":\"end_of_audio\"}");
                    Debug.WriteLine("[AssemblyAIStreaming] Sent end_of_audio.");

                    // Wait for final results (timeout 3s)
                    await WaitForReceiveAsync(TimeSpan.FromSeconds(3));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AssemblyAIStreaming] Stop error: {ex.Message}");
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
                Debug.WriteLine($"[AssemblyAIStreaming] RAW: {json}");
#endif

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Check message type
                if (!root.TryGetProperty("type", out var typeElement)) return;
                var type = typeElement.GetString();

                if (type == "Begin")
                {
                    Debug.WriteLine("[AssemblyAIStreaming] Session started.");
                    return;
                }

                if (type == "Turn")
                {
                    // V3 API uses "Turn" messages with transcript text
                    var transcript = "";
                    var isFinal = false;

                    if (root.TryGetProperty("transcript", out var transcriptEl))
                    {
                        transcript = transcriptEl.GetString() ?? "";
                    }

                    // In V3, a turn with end_of_turn=true is final
                    if (root.TryGetProperty("end_of_turn", out var endOfTurnEl))
                    {
                        isFinal = endOfTurnEl.GetBoolean();
                    }

                    FireTranscriptReceived(new TranscriptEventArgs
                    {
                        Text = transcript,
                        IsFinal = isFinal,
                        SpeechFinal = isFinal
                    });

                    if (isFinal)
                    {
                        FireUtteranceEnd();
                    }
                    return;
                }

                // V2 fallback: "SessionBegins", "PartialTranscript", "FinalTranscript"
                if (type == "PartialTranscript" || type == "FinalTranscript")
                {
                    var transcript = "";
                    if (root.TryGetProperty("text", out var textEl))
                    {
                        transcript = textEl.GetString() ?? "";
                    }

                    bool isFinal = type == "FinalTranscript";

                    FireTranscriptReceived(new TranscriptEventArgs
                    {
                        Text = transcript,
                        IsFinal = isFinal,
                        SpeechFinal = isFinal
                    });

                    if (isFinal && !string.IsNullOrWhiteSpace(transcript))
                    {
                        FireUtteranceEnd();
                    }
                }
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[AssemblyAIStreaming] JSON parse error: {ex.Message}");
            }
        }
    }
}
