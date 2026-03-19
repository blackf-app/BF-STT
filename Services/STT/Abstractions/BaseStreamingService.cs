using BF_STT.Models;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;

namespace BF_STT.Services.STT.Abstractions
{
    public abstract class BaseStreamingService : IStreamingSttService
    {
        protected string _apiKey = "";
        protected ClientWebSocket? _webSocket;
        private CancellationTokenSource? _cts;
        private Task? _receiveTask;
        private Task? _keepAliveTask;

        public bool IsConnected { get; protected set; }
        public event EventHandler<TranscriptEventArgs>? TranscriptReceived;
        public event EventHandler? UtteranceEndReceived;
        public event EventHandler<string>? Error;

        public async Task SendAudioAsync(byte[] buffer, int count, CancellationToken ct = default)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open) return;
            try
            {
                await _webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, count),
                    WebSocketMessageType.Binary, true, ct);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{GetType().Name}] SendAudio error: {ex.Message}");
            }
        }

        public async Task CancelAsync(CancellationToken ct = default)
        {
            _cts?.Cancel();
            await CleanupAsync();
        }

        protected async Task ConnectAsync(Uri uri, CancellationToken ct)
        {
            _webSocket = new ClientWebSocket();
            ConfigureWebSocket(_webSocket);
            await _webSocket.ConnectAsync(uri, ct);
            IsConnected = true;
            StartReceiveLoop();
        }

        protected async Task SendTextAsync(string message, CancellationToken ct = default)
        {
            if (_webSocket == null || _webSocket.State != WebSocketState.Open) return;
            var bytes = Encoding.UTF8.GetBytes(message);
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text, true, ct);
        }

        protected async Task WaitForReceiveAsync(TimeSpan timeout)
        {
            if (_receiveTask != null)
                await Task.WhenAny(_receiveTask, Task.Delay(timeout));
        }

        protected async Task CleanupAsync()
        {
            IsConnected = false;
            _cts?.Cancel();
            try
            {
                if (_webSocket?.State == WebSocketState.Open)
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
            }
            catch { }
            _webSocket?.Dispose();
            _webSocket = null;
            _cts?.Dispose();
            _cts = null;
        }

        private void StartReceiveLoop()
        {
            _cts = new CancellationTokenSource();
            _receiveTask = ReceiveLoopAsync(_cts.Token);
            var interval = KeepAliveInterval;
            if (interval.HasValue)
                _keepAliveTask = KeepAliveLoopAsync(_cts.Token, interval.Value);
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[8192];
            try
            {
                while (!ct.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
                {
                    var sb = new StringBuilder();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            Debug.WriteLine($"[{GetType().Name}] Server closed connection.");
                            return;
                        }
                        if (result.MessageType == WebSocketMessageType.Text)
                            sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    } while (!result.EndOfMessage);

                    if (sb.Length > 0)
                        ProcessMessage(sb.ToString());
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException ex) { FireError($"WebSocket error: {ex.Message}"); }
            catch (Exception ex) { FireError($"Receive error: {ex.Message}"); }
        }

        private async Task KeepAliveLoopAsync(CancellationToken ct, TimeSpan interval)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(interval, ct);
                    if (_webSocket?.State == WebSocketState.Open)
                        await SendKeepAliveFrameAsync();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Debug.WriteLine($"[{GetType().Name}] KeepAlive error: {ex.Message}"); }
        }

        protected void FireTranscriptReceived(string text, bool isFinal) =>
            TranscriptReceived?.Invoke(this, new TranscriptEventArgs { Text = text, IsFinal = isFinal });
        protected void FireTranscriptReceived(TranscriptEventArgs args) =>
            TranscriptReceived?.Invoke(this, args);
        protected void FireUtteranceEnd() => UtteranceEndReceived?.Invoke(this, EventArgs.Empty);
        protected void FireError(string msg) => Error?.Invoke(this, msg);

        public abstract Task StartAsync(string language, CancellationToken ct = default);
        public abstract Task StopAsync(CancellationToken ct = default);
        public abstract void UpdateSettings(string apiKey, string model);
        protected abstract void ProcessMessage(string message);

        protected virtual void ConfigureWebSocket(ClientWebSocket ws) { }
        protected virtual TimeSpan? KeepAliveInterval => null;
        protected virtual Task SendKeepAliveFrameAsync() => Task.CompletedTask;

        public void Dispose()
        {
            _cts?.Cancel();
            try { _webSocket?.Dispose(); } catch { }
            _cts?.Dispose();
        }
    }
}
