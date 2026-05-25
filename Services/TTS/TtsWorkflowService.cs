using BF_STT.Services.Infrastructure;
using BF_STT.Services.Platform;

namespace BF_STT.Services.TTS
{
    public sealed class TtsWorkflowService
    {
        private readonly SettingsService _settingsService;
        private readonly TtsProviderRegistry _registry;
        private readonly TtsPlaybackService _playbackService;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly object _sync = new();
        private CancellationTokenSource? _activePlaybackCts;

        public event Action<bool>? PlaybackStateChanged;
        public event Action? SynthesisCompleted;

        public TtsWorkflowService(
            SettingsService settingsService,
            TtsProviderRegistry registry,
            TtsPlaybackService playbackService)
        {
            _settingsService = settingsService;
            _registry = registry;
            _playbackService = playbackService;
        }

        public async Task SpeakClipboardAsync(CancellationToken ct = default)
        {
            if (!ClipboardHelper.TryGetText(out var text) || string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("Clipboard text is empty or unavailable.");
            }

            if (!await _gate.WaitAsync(0, ct))
            {
                throw new InvalidOperationException("TTS playback is already running.");
            }

            try
            {
                var settings = _settingsService.CurrentSettings;
                var validationError = _registry.ValidateProvider(settings.SelectedTtsProvider, settings);
                if (validationError != null)
                {
                    throw new InvalidOperationException(validationError);
                }

                var provider = _registry.GetEntry(settings.SelectedTtsProvider);
                using var playbackCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                lock (_sync)
                {
                    _activePlaybackCts = playbackCts;
                }

                PlaybackStateChanged?.Invoke(true);
                var result = await provider.Service.SynthesizeAsync(text, playbackCts.Token);
                SynthesisCompleted?.Invoke();
                var volume = settings.GetTtsProviderVolume(provider.Name);
                var rate = settings.GetTtsProviderRate(provider.Name);
                await _playbackService.PlayAsync(result.AudioData, result.ContentType, volume, rate, playbackCts.Token);
            }
            finally
            {
                lock (_sync)
                {
                    _activePlaybackCts = null;
                }

                PlaybackStateChanged?.Invoke(false);
                _gate.Release();
            }
        }

        public void StopPlayback()
        {
            CancellationTokenSource? activePlaybackCts;
            lock (_sync)
            {
                activePlaybackCts = _activePlaybackCts;
            }

            activePlaybackCts?.Cancel();
            _playbackService.Stop();
        }

        public void UpdateSettingsFromRegistry()
        {
            _registry.UpdateAllSettings(_settingsService.CurrentSettings);
        }
    }
}
