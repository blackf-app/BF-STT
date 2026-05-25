using BF_STT.Services.TTS.Abstractions;

namespace BF_STT.Services.TTS
{
    public sealed class UnsupportedTtsService : ITtsService
    {
        public UnsupportedTtsService(string reason)
        {
            Reason = reason;
        }

        public string Reason { get; }

        public void UpdateSettings(string apiKey, string model, string voice, string baseUrl)
        {
        }

        public Task<TtsAudioResult> SynthesizeAsync(string text, CancellationToken ct = default)
        {
            throw new NotSupportedException(Reason);
        }
    }
}
