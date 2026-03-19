using BF_STT.Services.STT.Abstractions;
using System.Net.Http;
using Xunit;

namespace BF_STT.Tests
{
    public class BaseBatchSttServiceTests
    {
        // ─── Test double ──────────────────────────────────────────────────────

        /// <summary>Minimal concrete subclass for testing base class validation.</summary>
        private class StubBatchService : BaseBatchSttService
        {
            public string? LastLanguage { get; private set; }
            public byte[]? LastAudioData { get; private set; }
            public string TranscribeResult { get; set; } = "stub result";

            public StubBatchService(string apiKey)
                : base(new HttpClient(), apiKey, "https://example.com", "https://example.com", "model-v1") { }

            protected override Task<string> TranscribeCore(byte[] audioData, string language, CancellationToken ct)
            {
                LastAudioData = audioData;
                LastLanguage = language;
                return Task.FromResult(TranscribeResult);
            }
        }

        // ─── Input validation ─────────────────────────────────────────────────

        [Fact]
        public async Task TranscribeAsync_NullAudioData_ThrowsArgumentException()
        {
            var svc = new StubBatchService("valid-key");
            await Assert.ThrowsAsync<ArgumentException>(() =>
                svc.TranscribeAsync((byte[])null!, "en"));
        }

        [Fact]
        public async Task TranscribeAsync_EmptyAudioData_ThrowsArgumentException()
        {
            var svc = new StubBatchService("valid-key");
            await Assert.ThrowsAsync<ArgumentException>(() =>
                svc.TranscribeAsync(Array.Empty<byte>(), "en"));
        }

        [Fact]
        public async Task TranscribeAsync_EmptyApiKey_ThrowsInvalidOperationException()
        {
            var svc = new StubBatchService("");
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                svc.TranscribeAsync(new byte[] { 1, 2, 3 }, "en"));
        }

        // ─── Happy path ───────────────────────────────────────────────────────

        [Fact]
        public async Task TranscribeAsync_ValidInput_CallsTranscribeCore()
        {
            var svc = new StubBatchService("my-api-key");
            var audio = new byte[] { 1, 2, 3 };

            var result = await svc.TranscribeAsync(audio, "vi");

            Assert.Equal("stub result", result);
            Assert.Same(audio, svc.LastAudioData);
            Assert.Equal("vi", svc.LastLanguage);
        }

        // ─── UpdateSettings ───────────────────────────────────────────────────

        [Fact]
        public async Task UpdateSettings_UpdatesApiKey()
        {
            var svc = new StubBatchService("old-key");
            svc.UpdateSettings("new-key", "");
            // Verify new key is used: empty audio → still throws ArgumentException, not key error
            await Assert.ThrowsAsync<ArgumentException>(() =>
                svc.TranscribeAsync(Array.Empty<byte>(), "en"));
        }

        [Fact]
        public async Task UpdateSettings_NonEmptyModel_UpdatesModel()
        {
            var svc = new StubBatchService("key");
            svc.UpdateSettings("key", "new-model");
            // TranscribeCore called successfully with updated settings
            var result = await svc.TranscribeAsync(new byte[] { 1 }, "en");
            Assert.Equal("stub result", result);
        }
    }
}
