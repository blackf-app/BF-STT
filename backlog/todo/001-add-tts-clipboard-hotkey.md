### [HIGH] Add TTS from clipboard with F2 hotkey (L)

**Tier:** L - cross-cutting work: new provider surface, global hotkey, clipboard flow, audio playback, settings migration, and UI changes

---

## Description

Add a Text-to-Speech (TTS) feature to BF-STT. When the user presses F2, the app reads the current copied text from the clipboard, sends it to the selected TTS provider/server, receives audio data, and plays it automatically. The feature should support TTS for providers already represented in the project where the provider has a usable TTS API; providers without TTS support must be handled explicitly in UI and validation instead of pretending to support synthesis. Settings must separate API keys into STT and TTS categories so users can configure transcription and synthesis independently.

## Context & Constraints

- Existing patterns to follow: STT abstractions, provider registry, DI registration, secure settings serialization, global hotkey service, clipboard helper, NAudio usage.
- Must not change: existing STT recording, batch/streaming provider behavior, F3/F4 hotkey behavior, history behavior, startup validation semantics for STT.
- Related systems: Settings window, App startup hotkey wiring, provider registration, secure settings, clipboard access, audio playback.
- Dependencies: verify current TTS API support and request/response contracts for existing providers during implementation.

## Related Files

- `App.xaml.cs` - wire the new F2 hotkey callback without breaking existing STT hotkeys.
- `Services/Platform/HotkeyService.cs` - register and dispatch the TTS hotkey.
- `Services/Platform/ClipboardHelper.cs` - read copied text safely from the clipboard.
- `ViewModels/MainViewModel.cs` - expose the TTS command flow from hotkey to service.
- `Services/Infrastructure/ServiceRegistration.cs` - register TTS registry, providers, playback service, and workflow services.
- `Services/Infrastructure/SettingsService.cs` - add TTS settings, encrypted TTS API keys, model/voice/base URL fields, and backward-compatible load/save behavior.
- `SettingsWindow.xaml` - split API Key settings into STT and TTS categories.
- `SettingsWindow.xaml.cs` - load, validate, save, and migrate STT/TTS key fields and TTS hotkey settings.
- `Services/STT/SttProviderRegistry.cs` - use as the reference pattern for a new TTS provider registry, not as a place to mix STT and TTS responsibilities.
- `Services/STT/Providers/*` - use existing provider folders/names as the compatibility map for TTS provider implementations.
- `BF-STT.Tests/*` - add focused unit tests for settings migration, registry selection, unsupported providers, and request validation where practical.

## Acceptance Criteria

- [ ] Pressing F2 reads non-empty text from the clipboard and starts TTS synthesis without blocking the UI thread.
- [ ] The selected TTS provider sends clipboard text to its server/API and receives playable audio bytes or stream data.
- [ ] Returned audio plays automatically through the local output device and disposes all audio buffers/streams afterward.
- [ ] Empty, whitespace-only, or unavailable clipboard text is handled gracefully with a user-visible error/status and no provider request.
- [ ] TTS provider support exists for all current providers that actually expose a compatible TTS API, with unsupported current providers listed as unavailable in UI/validation.
- [ ] Settings has separate STT and TTS API-key categories, and users can save/load keys for both without overwriting the other category.
- [ ] Existing STT API keys remain readable after upgrade, and old settings files load without data loss.
- [ ] F2 does not conflict with F3 recording or F4 Stop & Send; any configured duplicate hotkeys are rejected or resolved clearly.
- [ ] API keys and synthesized text are not written to logs.
- [ ] Regression: batch STT still works.
- [ ] Regression: streaming STT still works for providers that already supported streaming.
- [ ] Regression: Settings window still opens, saves, cancels, and loads existing values correctly.
- [ ] Regression: Test Mode behavior remains STT-only unless a separate TTS test behavior is intentionally added.

## Phases & Checkpoints

### Phase 1: TTS Abstractions and Settings
- [ ] Add TTS service interfaces and a TTS provider registry following the STT registry pattern.
- [ ] Add `AppSettings` fields for selected TTS provider, TTS hotkey defaulting to VK_F2, provider-specific TTS API keys, models, voices, and base URLs as needed.
- [ ] Extend secure settings encryption/decryption for TTS keys.
- [ ] Add settings load/save compatibility for users with existing STT-only settings.
- **Observable checkpoint:** App starts with existing settings and exposes TTS configuration data without changing STT behavior.

### Phase 2: Provider Implementations
- [ ] Verify which existing providers support TTS and document unsupported providers in code/UI.
- [ ] Implement provider-specific TTS requests and audio response parsing for supported providers.
- [ ] Add validation errors for unsupported or unconfigured TTS providers.
- **Observable checkpoint:** A supported provider can synthesize clipboard text into audio bytes in isolation.

### Phase 3: Hotkey, Clipboard, and Playback Workflow
- [ ] Register F2 as the default TTS hotkey.
- [ ] Read clipboard text safely on the UI thread where required by WPF clipboard APIs.
- [ ] Send text to the selected TTS provider asynchronously with cancellation support.
- [ ] Play returned audio via NAudio or the existing audio infrastructure.
- [ ] Prevent overlapping TTS playback or define clear behavior for repeated F2 presses.
- **Observable checkpoint:** Pressing F2 with copied text produces audible playback and keeps the app responsive.

### Phase 4: Settings UI and Validation
- [ ] Split API Key settings into STT and TTS categories within the Settings window.
- [ ] Add TTS provider/model/voice fields only where they are needed.
- [ ] Add validation for duplicate hotkeys across TTS, record, and Stop & Send.
- [ ] Keep unsupported TTS providers visibly disabled or unavailable.
- **Observable checkpoint:** Users can configure STT and TTS keys independently and save/reopen Settings with values intact.

### Phase 5: Tests and Regression
- [ ] Add unit tests for TTS registry behavior.
- [ ] Add unit tests for settings encryption/decryption and backward compatibility where feasible.
- [ ] Add tests or mocked coverage for empty clipboard/request validation.
- [ ] Run existing test suite and manual verification.
- **Observable checkpoint:** Existing tests pass, new focused tests pass, and manual hotkey playback works.

## Scope-Control Summary

- **Broad change?** Yes - this introduces a new product capability across settings, providers, hotkeys, clipboard, and audio playback.
- **Affected areas:** `Services/Infrastructure`, `Services/Platform`, provider services, WPF Settings UI, `App.xaml.cs`, `MainViewModel`, tests.
- **Migration plan:** Preserve existing STT settings and add nullable/defaulted TTS settings so old `settings.json` files remain valid.
- **Test/regression plan:** Cover settings compatibility, registry behavior, unsupported provider validation, empty clipboard handling, and existing STT workflows.
- **Checkpoints:** See phases above.
- **Rollback/fallback:** Feature can be disabled by leaving no TTS provider/key configured; STT paths must remain independent.
- **Out-of-scope:** Do not redesign the main STT workflow, do not replace existing STT provider APIs, do not log clipboard text, do not add cloud provider accounts or secrets.

## Applicable Guardrails

- No API key in logs.
- Do not log synthesized clipboard text.
- CancellationToken on all async provider calls.
- Dispose HttpClient responses, audio streams, and playback buffers.
- No UI thread blocking.
- Clipboard access must follow WPF threading requirements.
- XAML bindings use INotifyPropertyChanged where applicable.
- Use a TTS provider registry instead of provider-specific if/else chains spread through the app.
- Settings backward compatibility.

## Risks

- Provider support mismatch: some existing STT providers may not have TTS APIs. Mitigation: verify support during implementation and expose unsupported providers clearly.
- Audio format mismatch: providers may return different formats. Mitigation: normalize playback handling or constrain supported formats per provider.
- Hotkey conflict: F2 may conflict with user/system shortcuts. Mitigation: make default F2 configurable and validate duplicates.
- Privacy risk: clipboard text may contain sensitive content. Mitigation: require explicit F2 action, avoid logging text, and show provider-bound behavior clearly in settings/status.

## Manual Verification

1. Copy a short Vietnamese sentence to the clipboard.
2. Configure a supported TTS provider and API key in Settings under the TTS category.
3. Press F2 and confirm audio is generated and played automatically.
4. Copy empty/whitespace text or clear clipboard text, press F2, and confirm no provider request is made.
5. Confirm F3/F4 STT workflows still behave as before.
6. Reopen Settings and confirm STT keys and TTS keys persist independently.
7. Restart the app with an existing STT-only settings file and confirm startup does not lose STT configuration.

## Assumptions

- F2 should be the default TTS hotkey.
- TTS provider selection should be configurable instead of hardcoded.
- Only providers with real TTS API support should be enabled for synthesis.
- Existing STT API keys should not automatically be reused for TTS unless the provider's API supports the same key and the UI makes that relationship clear.
