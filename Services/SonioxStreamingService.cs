using BF_STT.Models;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace BF_STT.Services
{
    public class SonioxStreamingService : IStreamingSttService, IDisposable
    {
        private string _apiKey;
        private readonly string _streamingUrl;
        private string _model;

        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _receiveCts;
        private Task? _receiveTask;
        private bool _isConnected;

        public event EventHandler<TranscriptEventArgs>? TranscriptReceived;
        public event EventHandler? UtteranceEndReceived;
        public event EventHandler<string>? Error;

        public bool IsConnected => _isConnected;

        public SonioxStreamingService(string apiKey, string streamingUrl)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _streamingUrl = string.IsNullOrWhiteSpace(streamingUrl)
                ? "wss://stt-rt.soniox.com/transcribe-websocket"
                : streamingUrl;
            _model = "stt-rt-v4"; // Correct default model for Soniox streaming/real-time APIs
        }

        public void UpdateSettings(string apiKey, string model)
        {
            _apiKey = apiKey;
            if (!string.IsNullOrWhiteSpace(model)) _model = model;
        }

        public async Task StartAsync(string language, CancellationToken ct = default)
        {
            if (_isConnected) return;

            _webSocket?.Dispose();
            _webSocket = new ClientWebSocket();
            // Soniox typically requires you to send the API key as the first message or in the header/URL.
            // Often it's requested in the first message to configure the streaming session.
            
            _receiveCts = new CancellationTokenSource();

            try
            {
                await _webSocket.ConnectAsync(new Uri(_streamingUrl), CancellationToken.None);
                _isConnected = true;

                // Send configuration message
                var configMsg = new
                {
                    api_key = _apiKey,
                    model = _model,
                    include_nonfinal = true,
                    // Additional parameters like sample rate might be required depending on exact API shape
                };

                var configBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(configMsg));
                await _webSocket.SendAsync(new ArraySegment<byte>(configBytes), WebSocketMessageType.Text, true, CancellationToken.None);

                // Start background receive loop
                _receiveTask = ReceiveLoopAsync(_receiveCts.Token);

                Debug.WriteLine("[SonioxStreaming] Connected.");
            }
            catch (Exception ex)
            {
                _isConnected = false;
                Error?.Invoke(this, $"Connection failed: {ex.Message}");
                throw;
            }
        }

        public async Task SendAudioAsync(byte[] buffer, int count, CancellationToken ct = default)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open) return;

            try
            {
                var segment = new ArraySegment<byte>(buffer, 0, count);
                await _webSocket.SendAsync(segment, WebSocketMessageType.Binary, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SonioxStreaming] Send error: {ex.Message}");
            }
        }

        public async Task StopAsync(CancellationToken ct = default)
        {
            if (!_isConnected || _webSocket == null) return;

            try
            {
                if (_webSocket.State == WebSocketState.Open)
                {
                    // Sending empty audio chunk or specific EOS JSON to indicate end of stream
                    // For now, sending a close frame directly
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "End of Stream", CancellationToken.None);
                    Debug.WriteLine("[SonioxStreaming] Close requested.");

                    if (_receiveTask != null)
                    {
                        await Task.WhenAny(_receiveTask, Task.Delay(3000));
                    }
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

        public async Task CancelAsync(CancellationToken ct = default)
        {
            _receiveCts?.Cancel();
            await CleanupAsync();
        }

        private async Task CleanupAsync()
        {
            _isConnected = false;
            _receiveCts?.Cancel();

            try
            {
                if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
                }
            }
            catch { }

            _webSocket?.Dispose();
            _webSocket = null;
            _receiveCts?.Dispose();
            _receiveCts = null;

            Debug.WriteLine("[SonioxStreaming] Cleaned up.");
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];

            try
            {
                while (!cancellationToken.IsCancellationRequested &&
                       _webSocket != null &&
                       _webSocket.State == WebSocketState.Open)
                {
                    var messageBuilder = new StringBuilder();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await _webSocket.ReceiveAsync(
                            new ArraySegment<byte>(buffer), cancellationToken);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            Debug.WriteLine("[SonioxStreaming] Server closed connection.");
                            return;
                        }

                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                        }
                    } while (!result.EndOfMessage);

                    if (messageBuilder.Length > 0)
                    {
                        ProcessMessage(messageBuilder.ToString());
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[SonioxStreaming] Receive loop cancelled.");
            }
            catch (WebSocketException ex)
            {
                Debug.WriteLine($"[SonioxStreaming] WebSocket error: {ex.Message}");
                Error?.Invoke(this, $"WebSocket error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SonioxStreaming] Receive error: {ex.Message}");
                Error?.Invoke(this, ex.Message);
            }
        }

        private void ProcessMessage(string json)
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
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
                    TranscriptReceived?.Invoke(this, new TranscriptEventArgs
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

        public void Dispose()
        {
            _receiveCts?.Cancel();
            try { _webSocket?.Dispose(); } catch { }
            _receiveCts?.Dispose();
        }
    }
}
