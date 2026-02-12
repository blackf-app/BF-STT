using BF_STT.Models;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace BF_STT.Services
{
    public class DeepgramStreamingService : IDisposable
    {
        private string _apiKey;
        private readonly string _streamingUrl;
        private string _model;

        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _receiveCts;
        private Task? _receiveTask;
        private bool _isConnected;

        public event EventHandler<TranscriptEventArgs>? TranscriptReceived;
        public event EventHandler<string>? Error;

        public bool IsConnected => _isConnected;

        public DeepgramStreamingService(string apiKey, string streamingUrl, string model)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _streamingUrl = string.IsNullOrWhiteSpace(streamingUrl)
                ? "wss://api.deepgram.com/v1/listen"
                : streamingUrl;
            _model = string.IsNullOrWhiteSpace(model) ? "nova-3" : model;
        }

        public void UpdateSettings(string apiKey, string model)
        {
            _apiKey = apiKey;
            if (!string.IsNullOrWhiteSpace(model)) _model = model;
        }

        /// <summary>
        /// Opens a WebSocket connection to Deepgram streaming API.
        /// </summary>
        public async Task StartAsync(string language)
        {
            if (_isConnected) return;

            _webSocket?.Dispose();
            _webSocket = new ClientWebSocket();
            _webSocket.Options.SetRequestHeader("Authorization", $"Token {_apiKey}");

            // Build WebSocket URL with query parameters
            var url = $"{_streamingUrl}?model={_model}&language={language}" +
                      "&encoding=linear16&sample_rate=16000&channels=1" +
                      "&interim_results=true&smart_format=true&endpointing=300";

            _receiveCts = new CancellationTokenSource();

            try
            {
                await _webSocket.ConnectAsync(new Uri(url), CancellationToken.None);
                _isConnected = true;

                // Start background receive loop
                _receiveTask = ReceiveLoopAsync(_receiveCts.Token);

                Debug.WriteLine("[DeepgramStreaming] Connected.");
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
        /// Buffer should be 16kHz, 16-bit, mono PCM (already processed by AudioRecordingService).
        /// </summary>
        public async Task SendAudioAsync(byte[] buffer, int count)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open) return;

            try
            {
                var segment = new ArraySegment<byte>(buffer, 0, count);
                await _webSocket.SendAsync(segment, WebSocketMessageType.Binary, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DeepgramStreaming] Send error: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends CloseStream message and waits for final results before closing.
        /// </summary>
        public async Task StopAsync()
        {
            if (!_isConnected || _webSocket == null) return;

            try
            {
                // Send CloseStream JSON message to tell Deepgram we're done
                if (_webSocket.State == WebSocketState.Open)
                {
                    var closeMessage = Encoding.UTF8.GetBytes("{\"type\":\"CloseStream\"}");
                    await _webSocket.SendAsync(
                        new ArraySegment<byte>(closeMessage),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);

                    Debug.WriteLine("[DeepgramStreaming] Sent CloseStream.");

                    // Wait for the receive loop to finish (server will close after sending final results)
                    // Timeout after 3 seconds to avoid hanging
                    if (_receiveTask != null)
                    {
                        await Task.WhenAny(_receiveTask, Task.Delay(3000));
                    }
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

        /// <summary>
        /// Cancel and cleanup without waiting for final results.
        /// </summary>
        public async Task CancelAsync()
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
            catch { /* Ignore close errors */ }

            _webSocket?.Dispose();
            _webSocket = null;
            _receiveCts?.Dispose();
            _receiveCts = null;

            Debug.WriteLine("[DeepgramStreaming] Cleaned up.");
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
                            Debug.WriteLine("[DeepgramStreaming] Server closed connection.");
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
                Debug.WriteLine("[DeepgramStreaming] Receive loop cancelled.");
            }
            catch (WebSocketException ex)
            {
                Debug.WriteLine($"[DeepgramStreaming] WebSocket error: {ex.Message}");
                Error?.Invoke(this, $"WebSocket error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DeepgramStreaming] Receive error: {ex.Message}");
                Error?.Invoke(this, ex.Message);
            }
        }

        private void ProcessMessage(string json)
        {
            try
            {
                #if DEBUG
                Debug.WriteLine($"[DeepgramStreaming] RAW: {json}");
                #endif

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var response = JsonSerializer.Deserialize<DeepgramStreamingResponse>(json, options);

                if (response == null) return;

                // Only process "Results" type messages
                if (response.Type != "Results") return;

                var transcript = response.Channel?.Alternatives?.FirstOrDefault()?.Transcript;
                if (transcript == null) transcript = string.Empty;

#if DEBUG
                Debug.WriteLine($"[DeepgramStreaming] Type={response.Type} IsFinal={response.IsFinal} SpeechFinal={response.SpeechFinal} Text=\"{transcript}\"");
#endif

                TranscriptReceived?.Invoke(this, new TranscriptEventArgs
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
