using BF_STT.Models;
using BF_STT.Services.STT.Abstractions;
using Xunit;

namespace BF_STT.Tests
{
    public class BaseStreamingServiceTests
    {
        // ─── Test double ──────────────────────────────────────────────────────

        /// <summary>
        /// Minimal concrete subclass for testing base class shared behaviour.
        /// StartAsync/StopAsync do nothing (no real WebSocket).
        /// </summary>
        private class StubStreamingService : BaseStreamingService
        {
            public List<string> ReceivedMessages { get; } = new();

            public override Task StartAsync(string language, CancellationToken ct = default)
            {
                IsConnected = true;
                return Task.CompletedTask;
            }

            public override Task StopAsync(CancellationToken ct = default)
            {
                IsConnected = false;
                return Task.CompletedTask;
            }

            public override void UpdateSettings(string apiKey, string model)
            {
                _apiKey = apiKey;
            }

            protected override void ProcessMessage(string message)
            {
                ReceivedMessages.Add(message);
            }

            // Expose Fire* helpers for testing
            public void TriggerTranscriptReceived(string text, bool isFinal) =>
                FireTranscriptReceived(text, isFinal);

            public void TriggerUtteranceEnd() => FireUtteranceEnd();
            public void TriggerError(string msg) => FireError(msg);
        }

        // ─── Initial state ────────────────────────────────────────────────────

        [Fact]
        public void InitialState_IsConnectedFalse()
        {
            var svc = new StubStreamingService();
            Assert.False(svc.IsConnected);
        }

        // ─── StartAsync / StopAsync ───────────────────────────────────────────

        [Fact]
        public async Task StartAsync_SetsIsConnectedTrue()
        {
            var svc = new StubStreamingService();
            await svc.StartAsync("en");
            Assert.True(svc.IsConnected);
        }

        [Fact]
        public async Task StopAsync_SetsIsConnectedFalse()
        {
            var svc = new StubStreamingService();
            await svc.StartAsync("en");
            await svc.StopAsync();
            Assert.False(svc.IsConnected);
        }

        // ─── CancelAsync ──────────────────────────────────────────────────────

        [Fact]
        public async Task CancelAsync_WhenNotConnected_DoesNotThrow()
        {
            var svc = new StubStreamingService();
            // Should not throw even though _webSocket is null
            await svc.CancelAsync();
            Assert.False(svc.IsConnected);
        }

        [Fact]
        public async Task CancelAsync_SetsIsConnectedFalse()
        {
            var svc = new StubStreamingService();
            await svc.StartAsync("en");
            await svc.CancelAsync();
            Assert.False(svc.IsConnected);
        }

        // ─── SendAudioAsync ───────────────────────────────────────────────────

        [Fact]
        public async Task SendAudioAsync_WhenNotConnected_DoesNotThrow()
        {
            var svc = new StubStreamingService();
            // _webSocket is null — should be a no-op
            await svc.SendAudioAsync(new byte[] { 1, 2, 3 }, 3);
        }

        // ─── Event helpers ────────────────────────────────────────────────────

        [Fact]
        public void FireTranscriptReceived_RaisesEvent()
        {
            var svc = new StubStreamingService();
            TranscriptEventArgs? received = null;
            svc.TranscriptReceived += (_, e) => received = e;

            svc.TriggerTranscriptReceived("hello", isFinal: true);

            Assert.NotNull(received);
            Assert.Equal("hello", received!.Text);
            Assert.True(received.IsFinal);
        }

        [Fact]
        public void FireUtteranceEnd_RaisesEvent()
        {
            var svc = new StubStreamingService();
            bool fired = false;
            svc.UtteranceEndReceived += (_, _) => fired = true;

            svc.TriggerUtteranceEnd();

            Assert.True(fired);
        }

        [Fact]
        public void FireError_RaisesEvent()
        {
            var svc = new StubStreamingService();
            string? errorMsg = null;
            svc.Error += (_, msg) => errorMsg = msg;

            svc.TriggerError("connection lost");

            Assert.Equal("connection lost", errorMsg);
        }

        // ─── Dispose ─────────────────────────────────────────────────────────

        [Fact]
        public void Dispose_DoesNotThrow()
        {
            var svc = new StubStreamingService();
            svc.Dispose(); // should not throw
        }

        [Fact]
        public async Task Dispose_AfterConnect_DoesNotThrow()
        {
            var svc = new StubStreamingService();
            await svc.StartAsync("en");
            svc.Dispose(); // should not throw
        }
    }
}
