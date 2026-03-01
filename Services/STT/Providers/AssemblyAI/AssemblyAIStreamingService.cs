using BF_STT.Models;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

using BF_STT.Services.STT.Abstractions;

namespace BF_STT.Services.STT.Providers.AssemblyAI
{
    public class AssemblyAIStreamingService : IStreamingSttService, IDisposable
    {
        private string _apiKey;
        private readonly string _streamingUrl;
        private string _model;

        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _receiveCts;
        private CancellationTokenSource? _keepAliveCts;
        private Task? _receiveTask;
        private Task? _keepAliveTask;
        private bool _isConnected;

        public event EventHandler<TranscriptEventArgs>? TranscriptReceived;
        public event EventHandler? UtteranceEndReceived;
        public event EventHandler<string>? Error;

        public bool IsConnected => _isConnected;

        public AssemblyAIStreamingService(string apiKey, string streamingUrl, string model)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _streamingUrl = string.IsNullOrWhiteSpace(streamingUrl)
                ? "wss://streaming.assemblyai.com/v3/ws"
                : streamingUrl;
            _model = string.IsNullOrWhiteSpace(model) ? "best" : model;
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
            _webSocket.Options.SetRequestHeader("Authorization", _apiKey);

            // Build WebSocket URL with query parameters
            var url = $"{_streamingUrl}?encoding=pcm_s16le&sample_rate=16000";

            _receiveCts = new CancellationTokenSource();
            _keepAliveCts = new CancellationTokenSource();

            try
            {
                await _webSocket.ConnectAsync(new Uri(url), CancellationToken.None);
                _isConnected = true;

                _receiveTask = ReceiveLoopAsync(_receiveCts.Token);
                _keepAliveTask = KeepAliveLoopAsync(_keepAliveCts.Token);

                Debug.WriteLine("[AssemblyAIStreaming] Connected.");
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
                Debug.WriteLine($"[AssemblyAIStreaming] Send error: {ex.Message}");
            }
        }

        public async Task StopAsync(CancellationToken ct = default)
        {
            if (!_isConnected || _webSocket == null) return;

            try
            {
                if (_webSocket.State == WebSocketState.Open)
                {
                    // Send end_of_audio message
                    var endMessage = Encoding.UTF8.GetBytes("{\"type\":\"end_of_audio\"}");
                    await _webSocket.SendAsync(
                        new ArraySegment<byte>(endMessage),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);

                    Debug.WriteLine("[AssemblyAIStreaming] Sent end_of_audio.");

                    // Wait for final results (timeout 3s)
                    if (_receiveTask != null)
                    {
                        await Task.WhenAny(_receiveTask, Task.Delay(3000));
                    }
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

        public async Task CancelAsync(CancellationToken ct = default)
        {
            _receiveCts?.Cancel();
            await CleanupAsync();
        }

        private async Task CleanupAsync()
        {
            _isConnected = false;
            _receiveCts?.Cancel();
            _keepAliveCts?.Cancel();

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
            _keepAliveCts?.Dispose();
            _keepAliveCts = null;

            Debug.WriteLine("[AssemblyAIStreaming] Cleaned up.");
        }

        private async Task KeepAliveLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(5000, cancellationToken);

                    if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                    {
                        // Send empty binary frame as keepalive
                        await _webSocket.SendAsync(
                            new ArraySegment<byte>(Array.Empty<byte>()),
                            WebSocketMessageType.Binary,
                            true,
                            CancellationToken.None);

                        Debug.WriteLine("[AssemblyAIStreaming] Sent KeepAlive.");
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AssemblyAIStreaming] KeepAlive error: {ex.Message}");
            }
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
                            Debug.WriteLine("[AssemblyAIStreaming] Server closed connection.");
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
                Debug.WriteLine("[AssemblyAIStreaming] Receive loop cancelled.");
            }
            catch (WebSocketException ex)
            {
                Debug.WriteLine($"[AssemblyAIStreaming] WebSocket error: {ex.Message}");
                Error?.Invoke(this, $"WebSocket error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AssemblyAIStreaming] Receive error: {ex.Message}");
                Error?.Invoke(this, ex.Message);
            }
        }

        private void ProcessMessage(string json)
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
                    var turnIsFormatted = false;

                    if (root.TryGetProperty("transcript", out var transcriptEl))
                    {
                        transcript = transcriptEl.GetString() ?? "";
                    }

                    if (root.TryGetProperty("turn_is_formatted", out var formattedEl))
                    {
                        turnIsFormatted = formattedEl.GetBoolean();
                    }

                    // In V3, a turn with end_of_turn=true is final
                    if (root.TryGetProperty("end_of_turn", out var endOfTurnEl))
                    {
                        isFinal = endOfTurnEl.GetBoolean();
                    }

                    TranscriptReceived?.Invoke(this, new TranscriptEventArgs
                    {
                        Text = transcript,
                        IsFinal = isFinal,
                        SpeechFinal = isFinal
                    });

                    if (isFinal)
                    {
                        UtteranceEndReceived?.Invoke(this, EventArgs.Empty);
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

                    TranscriptReceived?.Invoke(this, new TranscriptEventArgs
                    {
                        Text = transcript,
                        IsFinal = isFinal,
                        SpeechFinal = isFinal
                    });

                    if (isFinal && !string.IsNullOrWhiteSpace(transcript))
                    {
                        UtteranceEndReceived?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[AssemblyAIStreaming] JSON parse error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _receiveCts?.Cancel();
            try
            {
                _webSocket?.Dispose();
            }
            catch { }
            _receiveCts?.Dispose();
        }
    }
}
