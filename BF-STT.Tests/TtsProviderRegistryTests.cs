using BF_STT.Services.Infrastructure;
using BF_STT.Services.TTS;
using BF_STT.Services.TTS.Abstractions;
using Xunit;

namespace BF_STT.Tests
{
    public class TtsProviderRegistryTests
    {
        [Fact]
        public void ValidateProvider_UnsupportedProvider_ReturnsUnavailableReason()
        {
            var registry = new TtsProviderRegistry();
            registry.Register(
                "AssemblyAI",
                new UnsupportedTtsService("No standalone TTS."),
                false,
                _ => "",
                _ => "",
                _ => "",
                _ => "",
                "AssemblyAI TTS is only available inside its Voice Agent pipeline.");

            var error = registry.ValidateProvider("AssemblyAI", new AppSettings());

            Assert.Equal("AssemblyAI TTS is only available inside its Voice Agent pipeline.", error);
        }

        [Fact]
        public void ValidateProvider_MissingApiKey_ReturnsMissingKeyError()
        {
            var registry = new TtsProviderRegistry();
            registry.Register(
                "OpenAI",
                new FakeTtsService(),
                true,
                s => s.OpenAITtsApiKey,
                s => s.OpenAITtsModel,
                s => s.OpenAITtsVoice,
                s => s.OpenAITtsBaseUrl);

            var error = registry.ValidateProvider("OpenAI", new AppSettings { OpenAITtsApiKey = "" });

            Assert.Equal("OpenAI TTS API Key missing.", error);
        }

        [Fact]
        public void UpdateAllSettings_ConfiguredProvider_PushesTtsSettingsToService()
        {
            var service = new FakeTtsService();
            var registry = new TtsProviderRegistry();
            registry.Register(
                "OpenAI",
                service,
                true,
                s => s.OpenAITtsApiKey,
                s => s.OpenAITtsModel,
                s => s.OpenAITtsVoice,
                s => s.OpenAITtsBaseUrl);

            registry.UpdateAllSettings(new AppSettings
            {
                OpenAITtsApiKey = "tts-key",
                OpenAITtsModel = "tts-model",
                OpenAITtsVoice = "voice",
                OpenAITtsBaseUrl = "https://example.test/tts"
            });

            Assert.Equal("tts-key", service.ApiKey);
            Assert.Equal("tts-model", service.Model);
            Assert.Equal("voice", service.Voice);
            Assert.Equal("https://example.test/tts", service.BaseUrl);
        }

        [Fact]
        public async Task SynthesizeAsync_WhitespaceText_DoesNotCallProvider()
        {
            var service = new FakeTtsService();

            await Assert.ThrowsAsync<ArgumentException>(() => service.SynthesizeAsync("   "));

            Assert.False(service.WasCalled);
        }

        private sealed class FakeTtsService : ITtsService
        {
            public string ApiKey { get; private set; } = "";
            public string Model { get; private set; } = "";
            public string Voice { get; private set; } = "";
            public string BaseUrl { get; private set; } = "";
            public bool WasCalled { get; private set; }

            public void UpdateSettings(string apiKey, string model, string voice, string baseUrl)
            {
                ApiKey = apiKey;
                Model = model;
                Voice = voice;
                BaseUrl = baseUrl;
            }

            public Task<TtsAudioResult> SynthesizeAsync(string text, CancellationToken ct = default)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new ArgumentException("TTS text is empty.", nameof(text));
                }

                WasCalled = true;
                return Task.FromResult(new TtsAudioResult(new byte[] { 1, 2, 3 }, "audio/wav"));
            }
        }
    }
}
