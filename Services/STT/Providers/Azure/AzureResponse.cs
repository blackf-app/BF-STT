using System.Text.Json.Serialization;

namespace BF_STT.Services.STT.Providers.Azure
{
    public class AzureRecognitionResponse
    {
        [JsonPropertyName("RecognitionStatus")]
        public string? RecognitionStatus { get; set; }

        [JsonPropertyName("DisplayText")]
        public string? DisplayText { get; set; }

        [JsonPropertyName("Offset")]
        public long Offset { get; set; }

        [JsonPropertyName("Duration")]
        public long Duration { get; set; }
    }
}
