using BF_STT.Models;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

using BF_STT.Services.STT.Abstractions;

namespace BF_STT.Services.STT.Providers.Speechmatics
{
    public class SpeechmaticsStreamingService : BaseStreamingService
    {
        private readonly string _streamingUrl;
        private int _seqNo;

        public SpeechmaticsStreamingService(string apiKey, string streamingUrl)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _streamingUrl = string.IsNullOrWhiteSpace(streamingUrl)
                ? "wss://eu2.rt.speechmatics.com/v2"
                : streamingUrl.TrimEnd('/');
        }

        public override void UpdateSettings(string apiKey, string model)
        {
            _apiKey = apiKey;
        }

        protected override void ConfigureWebSocket(ClientWebSocket ws)
        {
            ws.Options.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
        }

        // Speechmatics has no keep-alive; KeepAliveInterval returns null (base default)

        public override async Task StartAsync(string language, CancellationToken ct = default)
        {
            if (IsConnected) return;

            // Note: Speechmatics realtime requires appending language to the URL path sometimes,
            // but standard v2 realtime is just wss://eu2.rt.speechmatics.com/v2
            string url = _streamingUrl;
            if (url.EndsWith("/v2"))
                url += "/" + language; // v2 endpoint usually looks like /v2/{lang} or just /v2 depending on API update

            _seqNo = 0;

            try
            {
                await ConnectAsync(new Uri(url), CancellationToken.None);

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
                    await SendJsonAsync(new { message = "EndOfStream", last_seq_no = _seqNo });
                    Debug.WriteLine("[SpeechmaticsStreaming] Sent EndOfStream.");

                    await WaitForReceiveAsync(TimeSpan.FromSeconds(3));
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

        private async Task SendJsonAsync(object obj)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open) return;
            var json = JsonSerializer.Serialize(obj);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        protected override void ProcessMessage(string json)
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

                            FireTranscriptReceived(new TranscriptEventArgs
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
                        FireUtteranceEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SpeechmaticsStreaming] Parse error: {ex.Message}\nJSON: {json}");
            }
        }
    }
}
