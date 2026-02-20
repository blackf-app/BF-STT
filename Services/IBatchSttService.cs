using System;
using System.Threading.Tasks;

namespace BF_STT.Services
{
    public interface IBatchSttService
    {
        void UpdateSettings(string apiKey, string model);
        Task<string> TranscribeAsync(string audioFilePath, string language);
    }
}
