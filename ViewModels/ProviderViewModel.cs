using System;

namespace BF_STT.ViewModels
{
    /// <summary>
    /// Represents the UI state for a single STT provider.
    /// This eliminates hardcoded properties for each provider in the MainViewModel.
    /// </summary>
    public class ProviderViewModel : ViewModelBase
    {
        private string _transcript = string.Empty;
        private bool _isConnected;
        private bool _isStreaming;

        public string Name { get; }

        public string Transcript
        {
            get => _transcript;
            set => SetProperty(ref _transcript, value);
        }

        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }

        public bool IsStreaming
        {
            get => _isStreaming;
            set => SetProperty(ref _isStreaming, value);
        }

        public ProviderViewModel(string name)
        {
            Name = name;
        }
    }
}
