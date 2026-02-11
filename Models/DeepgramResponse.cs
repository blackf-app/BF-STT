using System.Text.Json.Serialization;

namespace BF_STT.Models
{
    public class DeepgramResponse
    {
        [JsonPropertyName("results")]
        public Results? Results { get; set; }
    }

    public class Results
    {
        [JsonPropertyName("channels")]
        public List<Channel>? Channels { get; set; }
    }

    public class Channel
    {
        [JsonPropertyName("alternatives")]
        public List<Alternative>? Alternatives { get; set; }
    }

    public class Alternative
    {
        [JsonPropertyName("transcript")]
        public string? Transcript { get; set; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }
    }
}
