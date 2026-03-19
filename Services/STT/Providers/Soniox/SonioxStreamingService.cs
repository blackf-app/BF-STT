using BF_STT.Models;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

using BF_STT.Services.STT.Abstractions;

namespace BF_STT.Services.STT.Providers.Soniox
{
    public class SonioxStreamingService : BaseStreamingService
    {
        private readonly string _streamingUrl;
        private string _model;

        public SonioxStreamingService(string apiKey, string streamingUrl)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _streamingUrl = string.IsNullOrWhiteSpace(streamingUrl)
                ? "wss://stt-rt.soniox.com/transcribe-websocket"
                : streamingUrl;
            _model = "stt-rt-v4"; // Correct default model for Soniox streaming/real-time APIs
        }

        public override void UpdateSettings(string apiKey, string model)
        {
            _apiKey = apiKey;
            if (!string.IsNullOrWhiteSpace(model)) _model = model;
        }

        // Soniox has no keep-alive; KeepAliveInterval returns null (base default)

        public override async Task StartAsync(string language, CancellationToken ct = default)
        {
            if (IsConnected) return;

            try
            {
                await ConnectAsync(new Uri(_streamingUrl), CancellationToken.None);

                // Send configuration message (API key is sent as first message, not in header)
                var configMsg = new
                {
                    api_key = _apiKey,
                    model = _model,
                    include_nonfinal = true,
                    // Additional parameters like sample rate might be required depending on exact API shape
                };

                var configBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(configMsg));
                await _webSocket!.SendAsync(new ArraySegment<byte>(configBytes), WebSocketMessageType.Text, true, CancellationToken.None);

                Debug.WriteLine("[SonioxStreaming] Connected.");
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
                    // Sending a close frame directly to signal end of stream
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "End of Stream", CancellationToken.None);
                    Debug.WriteLine("[SonioxStreaming] Close requested.");

                    await WaitForReceiveAsync(TimeSpan.FromSeconds(3));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SonioxStreaming] Stop error: {ex.Message}");
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
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Assuming standard Soniox format: checking if we have words/tokens
                string text = "";
                bool isFinal = false;

                if (root.TryGetProperty("fw", out var fwElement))
                {
                    var words = new System.Collections.Generic.List<string>();
                    foreach (var w in fwElement.EnumerateArray())
                    {
                        if (w.TryGetProperty("t", out var tProp))
                        {
                            var s = tProp.GetString();
                            if (!string.IsNullOrEmpty(s)) words.Add(s);
                        }
                    }
                    text = string.Join(" ", words);
                    isFinal = true;
                }
                else if (root.TryGetProperty("nw", out var nwElement))
                {
                    var words = new System.Collections.Generic.List<string>();
                    foreach (var w in nwElement.EnumerateArray())
                    {
                        if (w.TryGetProperty("t", out var tProp))
                        {
                            var s = tProp.GetString();
                            if (!string.IsNullOrEmpty(s)) words.Add(s);
                        }
                    }
                    text = string.Join(" ", words);
                    isFinal = false; // non-final
                }

                if (!string.IsNullOrEmpty(text))
                {
                    FireTranscriptReceived(new TranscriptEventArgs
                    {
                        Text = text,
                        IsFinal = isFinal,
                        SpeechFinal = isFinal
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SonioxStreaming] JSON parse error: {ex.Message}. RAW JSON: {json}");
            }
        }
    }
}
