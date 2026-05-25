using System.Threading;
using System.Threading.Tasks;

namespace BF_STT.Services.TTS.Abstractions
{
    public interface ITtsService
    {
        void UpdateSettings(string apiKey, string model, string voice, string baseUrl);
        Task<TtsAudioResult> SynthesizeAsync(string text, CancellationToken ct = default);
    }

    public sealed record TtsAudioResult(byte[] AudioData, string ContentType);
}
