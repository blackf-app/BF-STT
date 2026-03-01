using BF_STT.Models;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

using BF_STT.Services.STT.Abstractions;

namespace BF_STT.Services.STT.Providers.ElevenLabs
{
    public class ElevenLabsStreamingService : IStreamingSttService, IDisposable
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

        public ElevenLabsStreamingService(string apiKey, string streamingUrl, string model)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _streamingUrl = string.IsNullOrWhiteSpace(streamingUrl)
                ? "wss://api.elevenlabs.io/v1/speech-to-text/streaming"
                : streamingUrl;
            _model = string.IsNullOrWhiteSpace(model) ? "scribe_v2_realtime" : model;
        }

        public void UpdateSettings(string apiKey, string model)
        {
            _apiKey = apiKey;
            if (!string.IsNullOrWhiteSpace(model)) _model = model;
        }

        /// <summary>
        /// Opens a WebSocket connection to ElevenLabs streaming STT API.
        /// </summary>
        public async Task StartAsync(string language, CancellationToken ct = default)
        {
            if (_isConnected) return;

            _webSocket?.Dispose();
            _webSocket = new ClientWebSocket();
            _webSocket.Options.SetRequestHeader("xi-api-key", _apiKey);

            // Build WebSocket URL with query parameters
            var url = $"{_streamingUrl}?model_id={_model}&language_code={language}" +
                      "&encoding=pcm_s16le&sample_rate=16000";

            _receiveCts = new CancellationTokenSource();
            _keepAliveCts = new CancellationTokenSource();

            try
            {
                await _webSocket.ConnectAsync(new Uri(url), CancellationToken.None);
                _isConnected = true;

                // Start background receive loop
                _receiveTask = ReceiveLoopAsync(_receiveCts.Token);

                // Start KeepAlive loop
                _keepAliveTask = KeepAliveLoopAsync(_keepAliveCts.Token);

                Debug.WriteLine("[ElevenLabsStreaming] Connected.");
            }
            catch (Exception ex)
            {
                _isConnected = false;
                Error?.Invoke(this, $"Connection failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Sends raw PCM audio bytes to the WebSocket.
        /// Buffer should be 16kHz, 16-bit, mono PCM.
        /// </summary>
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
                Debug.WriteLine($"[ElevenLabsStreaming] Send error: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends EOS (end of stream) message and waits for final results before closing.
        /// </summary>
        public async Task StopAsync(CancellationToken ct = default)
        {
            if (!_isConnected || _webSocket == null) return;

            try
            {
                if (_webSocket.State == WebSocketState.Open)
                {
                    // Send EOS (empty string) to signal end of audio
                    var eosMessage = Encoding.UTF8.GetBytes("{\"type\":\"EOS\"}");
                    await _webSocket.SendAsync(
                        new ArraySegment<byte>(eosMessage),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);

                    Debug.WriteLine("[ElevenLabsStreaming] Sent EOS.");

                    // Wait for the receive loop to finish
                    if (_receiveTask != null)
                    {
                        await Task.WhenAny(_receiveTask, Task.Delay(3000));
                    }
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

        /// <summary>
        /// Cancel and cleanup without waiting for final results.
        /// </summary>
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
            catch { /* Ignore close errors */ }

            _webSocket?.Dispose();
            _webSocket = null;
            _receiveCts?.Dispose();
            _receiveCts = null;
            _keepAliveCts?.Dispose();
            _keepAliveCts = null;

            Debug.WriteLine("[ElevenLabsStreaming] Cleaned up.");
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
                        var keepAliveMessage = Encoding.UTF8.GetBytes("{\"type\":\"ping\"}");
                        await _webSocket.SendAsync(
                            new ArraySegment<byte>(keepAliveMessage),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None);

                        Debug.WriteLine("[ElevenLabsStreaming] Sent ping.");
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ElevenLabsStreaming] KeepAlive error: {ex.Message}");
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
                            Debug.WriteLine("[ElevenLabsStreaming] Server closed connection.");
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
                Debug.WriteLine("[ElevenLabsStreaming] Receive loop cancelled.");
            }
            catch (WebSocketException ex)
            {
                Debug.WriteLine($"[ElevenLabsStreaming] WebSocket error: {ex.Message}");
                Error?.Invoke(this, $"WebSocket error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ElevenLabsStreaming] Receive error: {ex.Message}");
                Error?.Invoke(this, ex.Message);
            }
        }

        private void ProcessMessage(string json)
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
                        UtteranceEndReceived?.Invoke(this, EventArgs.Empty);
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

                TranscriptReceived?.Invoke(this, new TranscriptEventArgs
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
