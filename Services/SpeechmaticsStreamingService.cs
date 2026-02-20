using BF_STT.Models;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace BF_STT.Services
{
    public class SpeechmaticsStreamingService : IStreamingSttService, IDisposable
    {
        private string _apiKey;
        private readonly string _streamingUrl;
        
        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _receiveCts;
        private Task? _receiveTask;
        private bool _isConnected;
        private int _seqNo;

        public event EventHandler<TranscriptEventArgs>? TranscriptReceived;
        public event EventHandler? UtteranceEndReceived;
        public event EventHandler<string>? Error;

        public bool IsConnected => _isConnected;

        public SpeechmaticsStreamingService(string apiKey, string streamingUrl)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _streamingUrl = string.IsNullOrWhiteSpace(streamingUrl)
                ? "wss://eu2.rt.speechmatics.com/v2"
                : streamingUrl.TrimEnd('/');
        }

        public void UpdateSettings(string apiKey, string model)
        {
            _apiKey = apiKey;
        }

        public async Task StartAsync(string language)
        {
            if (_isConnected) return;

            _webSocket?.Dispose();
            _webSocket = new ClientWebSocket();
            _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
            
            // Note: Speechmatics realtime requires appending language to the URL path sometimes, 
            // but standard v2 realtime is just wss://eu2.rt.speechmatics.com/v2
            string url = _streamingUrl; 
            if (url.EndsWith("/v2"))
                url += "/" + language; // v2 endpoint usually looks like /v2/{lang} or just /v2 depending on API update. The docs often show /v2/{lang}

            _receiveCts = new CancellationTokenSource();
            _seqNo = 0;

            try
            {
                await _webSocket.ConnectAsync(new Uri(url), CancellationToken.None);
                _isConnected = true;

                // Start receive loop
                _receiveTask = ReceiveLoopAsync(_receiveCts.Token);
                
                // Send StartRecognition
                var startMsg = new
                {
                    message = "StartRecognition",
                    audio_format = new
                    {
                        type = "raw",
                        encoding = "pcm_s16le",
                        sample_rate = 16000
                    },
                    transcription_config = new
                    {
                        language = language,
                        operating_point = "enhanced",
                        enable_partials = true,
                        max_delay = 3.0
                    }
                };

                await SendJsonAsync(startMsg);

                Debug.WriteLine("[SpeechmaticsStreaming] Connected & StartRecognition sent.");
            }
            catch (Exception ex)
            {
                _isConnected = false;
                Error?.Invoke(this, $"Connection failed: {ex.Message}");
                throw;
            }
        }

        public async Task SendAudioAsync(byte[] buffer, int count)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open) return;

            // Speechmatics accepts raw binary audio frames, but they must be wrapped in AddAudio JSON, OR sent as binary.
            // Sending raw binary is supported by Speechmatics RT.
            try
            {
                var segment = new ArraySegment<byte>(buffer, 0, count);
                await _webSocket.SendAsync(segment, WebSocketMessageType.Binary, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SpeechmaticsStreaming] Send error: {ex.Message}");
            }
        }

        public async Task StopAsync()
        {
            if (!_isConnected || _webSocket == null) return;

            try
            {
                if (_webSocket.State == WebSocketState.Open)
                {
                    await SendJsonAsync(new { message = "EndOfStream", last_seq_no = _seqNo });
                    Debug.WriteLine("[SpeechmaticsStreaming] Sent EndOfStream.");

                    if (_receiveTask != null)
                    {
                        await Task.WhenAny(_receiveTask, Task.Delay(3000));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SpeechmaticsStreaming] Stop error: {ex.Message}");
            }
            finally
            {
                await CleanupAsync();
            }
        }

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
            catch { }

            _webSocket?.Dispose();
            _webSocket = null;
            _receiveCts?.Dispose();
            _receiveCts = null;

            Debug.WriteLine("[SpeechmaticsStreaming] Cleaned up.");
        }

        private async Task SendJsonAsync(object obj)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open) return;
            var json = JsonSerializer.Serialize(obj);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
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
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SpeechmaticsStreaming] Receive error: {ex.Message}");
                Error?.Invoke(this, ex.Message);
            }
        }

        private void ProcessMessage(string json)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.TryGetProperty("message", out var msgElement))
                {
                    var msgType = msgElement.GetString();
                    
                    if (msgType == "AddTranscript" || msgType == "AddPartialTranscript")
                    {
                        bool isFinal = msgType == "AddTranscript";
                        string transcript = "";
                        
                        // Parse metadata -> transcript
                        if (document.RootElement.TryGetProperty("metadata", out var metaElement) &&
                            metaElement.TryGetProperty("transcript", out var transElement))
                        {
                            transcript = transElement.GetString() ?? "";
                        }

                        if (!string.IsNullOrEmpty(transcript))
                        {
                            if (isFinal)
                            {
                                // Only add trailing space for separation, let API handle punctuation
                                transcript = transcript.TrimEnd() + " ";
                            }

                            TranscriptReceived?.Invoke(this, new TranscriptEventArgs
                            {
                                Text = transcript,
                                IsFinal = isFinal,
                                SpeechFinal = isFinal
                            });
                        }
                        
                        // UtteranceEnd commit is now handled by MainViewModel when IsFinal=true
                        // No need to fire UtteranceEndReceived here — it caused a race condition
                    }
                    else if (msgType == "EndOfTranscript")
                    {
                         UtteranceEndReceived?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            catch (Exception ex)
            {
                 Debug.WriteLine($"[SpeechmaticsStreaming] Parse error: {ex.Message}\nJSON: {json}");
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
