namespace BF_STT.Models
{
    public class TranscriptEventArgs : EventArgs
    {
        public string Text { get; set; } = string.Empty;
        public bool IsFinal { get; set; }
        public bool SpeechFinal { get; set; }
    }
}
