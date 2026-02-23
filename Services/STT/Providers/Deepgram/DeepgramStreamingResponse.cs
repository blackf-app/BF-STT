using System.Text.Json.Serialization;

namespace BF_STT.Services.STT.Providers.Deepgram
{
    public class DeepgramStreamingResponse
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("channel_index")]
        public List<int>? ChannelIndex { get; set; }

        [JsonPropertyName("duration")]
        public double Duration { get; set; }

        [JsonPropertyName("start")]
        public double Start { get; set; }

        [JsonPropertyName("is_final")]
        public bool IsFinal { get; set; }

        [JsonPropertyName("speech_final")]
        public bool SpeechFinal { get; set; }

        [JsonPropertyName("channel")]
        public StreamingChannel? Channel { get; set; }
    }

    public class StreamingChannel
    {
        [JsonPropertyName("alternatives")]
        public List<StreamingAlternative>? Alternatives { get; set; }
    }

    public class StreamingAlternative
    {
        [JsonPropertyName("transcript")]
        public string? Transcript { get; set; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }
    }
}
