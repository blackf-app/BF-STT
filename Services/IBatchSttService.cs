using System;
using System.Threading;
using System.Threading.Tasks;

namespace BF_STT.Services
{
    public interface IBatchSttService
    {
        void UpdateSettings(string apiKey, string model);
        Task<string> TranscribeAsync(string audioFilePath, string language, CancellationToken ct = default);
        Task<string> TranscribeAsync(byte[] audioData, string language, CancellationToken ct = default);
    }
}
