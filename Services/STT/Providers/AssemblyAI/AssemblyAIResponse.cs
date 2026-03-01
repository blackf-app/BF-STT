using System.Text.Json.Serialization;

namespace BF_STT.Services.STT.Providers.AssemblyAI
{
    // Upload response
    public class AssemblyAIUploadResponse
    {
        [JsonPropertyName("upload_url")]
        public string? UploadUrl { get; set; }
    }

    // Transcript response (for polling)
    public class AssemblyAITranscriptResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}
