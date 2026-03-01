using System.Net.Http;
using System.Text;
using System.Text.Json;

using BF_STT.Services.STT.Abstractions;

namespace BF_STT.Services.STT.Providers.Google
{
    public class GoogleBatchService : BaseBatchSttService
    {
        public GoogleBatchService(HttpClient httpClient, string apiKey, string baseUrl, string model)
            : base(httpClient, apiKey, baseUrl, "https://speech.googleapis.com/v1/speech:recognize",
                   string.IsNullOrWhiteSpace(model) ? "default" : model)
        { }

        protected override async Task<string> TranscribeCore(byte[] audioData, string language, CancellationToken ct)
        {
            // ── Parse WAV header to extract actual sample rate and raw PCM data ──
            // Google's speech:recognize with encoding=LINEAR16 expects raw PCM,
            // not a full WAV file. We also append silence so trailing words aren't clipped.
            int sampleRate = 16000;
            byte[] pcmData = audioData;

            if (audioData.Length > 44
                && audioData[0] == 'R' && audioData[1] == 'I'
                && audioData[2] == 'F' && audioData[3] == 'F')
            {
                // Read sample rate from WAV header (bytes 24-27, little-endian)
                sampleRate = BitConverter.ToInt32(audioData, 24);

                // Skip the standard 44-byte WAV header to get raw PCM
                pcmData = new byte[audioData.Length - 44];
                Array.Copy(audioData, 44, pcmData, 0, pcmData.Length);
            }

            // Append 300ms of silence (zeroes) so Google fully processes trailing speech.
            // Google's recognizer can clip the last fraction of audio unlike other APIs.
            int silenceBytes = sampleRate * 2 * 300 / 1000; // 16-bit mono = 2 bytes/sample
            byte[] paddedPcm = new byte[pcmData.Length + silenceBytes];
            Array.Copy(pcmData, 0, paddedPcm, 0, pcmData.Length);
            // remaining bytes are already zero (silence)

            // Google v1 REST API uses the API key as a query parameter
            var url = $"{BaseUrl}?key={ApiKey}";

            int maxRetries = 1;
            for (int i = 0; i <= maxRetries; i++)
            {
                var requestBody = new
                {
                    config = new
                    {
                        encoding = "LINEAR16",
                        sampleRateHertz = sampleRate,
                        languageCode = string.IsNullOrWhiteSpace(language) ? "vi" : language,
                        model = Model == "default" ? "default" : Model
                    },
                    audio = new
                    {
                        content = Convert.ToBase64String(paddedPcm)
                    }
                };

                var jsonBody = JsonSerializer.Serialize(requestBody);

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                try
                {
                    using var response = await HttpClient.SendAsync(request, ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync();

                        // Retry on server errors (5xx)
                        if ((int)response.StatusCode >= 500 && i < maxRetries)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"[Google] Server error {response.StatusCode}, retrying ({i + 1}/{maxRetries})...");
                            await Task.Delay(1000 * (i + 1), ct);
                            continue;
                        }

                        throw new HttpRequestException($"Google STT API Error: {response.StatusCode} - {errorBody}");
                    }

                    var json = await response.Content.ReadAsStringAsync();

#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[Google] RAW RESPONSE: {json}");
#endif

                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var result = JsonSerializer.Deserialize<GoogleResponse>(json, options);

                    // Concatenate all result transcripts
                    if (result?.Results != null)
                    {
                        var transcripts = result.Results
                            .Where(r => r.Alternatives != null && r.Alternatives.Count > 0)
                            .Select(r => r.Alternatives![0].Transcript)
                            .Where(t => !string.IsNullOrEmpty(t));

                        return string.Join(" ", transcripts);
                    }

                    return string.Empty;
                }
                catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[Google] Request failed (attempt {i + 1}/{maxRetries + 1}): {ex.Message}");

                    if (i == maxRetries)
                    {
                        throw new HttpRequestException($"Google STT API Error after retry: {ex.Message}", ex);
                    }

                    await Task.Delay(1000 * (i + 1), ct);
                }
            }
            return string.Empty;
        }
    }
}
