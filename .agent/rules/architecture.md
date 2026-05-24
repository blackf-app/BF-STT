# BF-STT Architecture Rules

Use these rules when changing BF-STT code.

## Ownership Boundaries

- `Services/Audio` owns capture, buffering, resampling, VAD, AGC, noise suppression, and audio-related timing.
- `Services/STT` owns provider abstractions, provider implementations, response parsing, registry behavior, and transcription filters.
- `Services/Workflow` owns recording state transitions and orchestration across audio, providers, and output.
- `Services/Platform` owns Windows integration: hotkeys, clipboard, and input injection.
- `Services/Infrastructure` owns settings, secure serialization, DI, logging, history, cleanup, and updates.
- `ViewModels` and XAML own presentation state and UI binding.

## Extension Rules

- Prefer adding providers through `SttProviderRegistry` and provider-specific folders.
- Do not add provider-specific branching across workflow or UI code unless there is a clear product requirement.
- Batch-only providers must be represented explicitly and must not fake streaming support.
- Streaming providers must have a defined stop/cancel/dispose lifecycle.
- Settings changes must be applied across `AppSettings`, UI copy/save paths, DI registration, and default config where relevant.
- Keep external API request/response models close to their provider implementation.

## Workflow Rules

- Model recording behavior through the existing state pattern.
- Any new transition must describe what happens on success, failure, stop, cancel, and timeout.
- Long-running operations must be asynchronous and cancellation-aware.
- Avoid fire-and-forget tasks unless they are logged, owned, and shut down safely.

## UI Rules

- Keep WPF binding names stable and raise property change notifications for displayed state.
- Avoid expensive work in constructors and property getters.
- Do not let UI layout decisions leak into provider or workflow services.
