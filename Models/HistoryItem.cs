using System;

namespace BF_STT.Models
{
    public class HistoryItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Text { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
    }
}
