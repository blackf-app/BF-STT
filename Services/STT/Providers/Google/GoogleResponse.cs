using System.Text.Json.Serialization;

namespace BF_STT.Services.STT.Providers.Google
{
    public class GoogleResponse
    {
        [JsonPropertyName("results")]
        public List<GoogleResult>? Results { get; set; }
    }

    public class GoogleResult
    {
        [JsonPropertyName("alternatives")]
        public List<GoogleAlternative>? Alternatives { get; set; }
    }

    public class GoogleAlternative
    {
        [JsonPropertyName("transcript")]
        public string? Transcript { get; set; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }
    }
}
