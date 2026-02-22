using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using BF_STT.Models;

namespace BF_STT.Services
{
    public class HistoryService
    {
        private const string AppName = "BF-STT";
        private const string HistoryFileName = "history.json";
        private int _maxHistoryItems;
        private readonly string _historyFilePath;

        public ObservableCollection<HistoryItem> History { get; private set; } = new ObservableCollection<HistoryItem>();

        public HistoryService(int maxItems = 100)
        {
            _maxHistoryItems = maxItems;
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);
            Directory.CreateDirectory(appDataPath);
            _historyFilePath = Path.Combine(appDataPath, HistoryFileName);
            LoadHistory();
        }

        public void UpdateMaxItems(int count)
        {
            _maxHistoryItems = count;
            // Trim if current count exceeds new limit
            while (History.Count > _maxHistoryItems && History.Count > 0)
            {
                History.RemoveAt(History.Count - 1);
            }
            SaveHistory();
        }

        public void LoadHistory()
        {
            if (File.Exists(_historyFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_historyFilePath);
                    var items = JsonSerializer.Deserialize<List<HistoryItem>>(json);
                    if (items != null)
                    {
                        History.Clear();
                        foreach (var item in items.OrderByDescending(i => i.Timestamp))
                        {
                            History.Add(item);
                        }
                    }
                }
                catch
                {
                    // If corrupted, start over
                    History.Clear();
                }
            }
        }

        public void SaveHistory()
        {
            try
            {
                var json = JsonSerializer.Serialize(History.ToList(), new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_historyFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving history: {ex.Message}");
            }
        }

        public void AddEntry(string text, string provider)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            // Remove if already exists with same text recently (optional, but prevents duplicates from streaming/batch overlap)
            var existing = History.FirstOrDefault(h => h.Text == text && (DateTime.Now - h.Timestamp).TotalSeconds < 5);
            if (existing != null) return;

            var newItem = new HistoryItem
            {
                Text = text,
                Provider = provider,
                Timestamp = DateTime.Now
            };

            History.Insert(0, newItem);

            while (History.Count > _maxHistoryItems)
            {
                History.RemoveAt(History.Count - 1);
            }

            SaveHistory();
        }

        public void ClearHistory()
        {
            History.Clear();
            SaveHistory();
        }

        public void RemoveEntry(Guid id)
        {
            var item = History.FirstOrDefault(i => i.Id == id);
            if (item != null)
            {
                History.Remove(item);
                SaveHistory();
            }
        }
    }
}
