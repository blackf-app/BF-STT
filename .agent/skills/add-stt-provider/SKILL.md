---
name: Add STT Provider
description: Checklist and implementation workflow for adding a new Speech-to-Text provider to BF-STT. Use whenever integrating a new batch or streaming STT API.
---

# Add STT Provider

This skill defines the required workflow for adding a Speech-to-Text provider to BF-STT. Do not treat providers as only a service class. A provider addition can affect settings, DI registration, UI, Test Mode, docs, and tests.

## Prerequisite: Research First

Before implementation, use `research-stt-provider` or otherwise confirm:

- Provider supports batch, streaming, or both.
- Batch endpoint URL, auth method, request format, audio format, and response JSON.
- Streaming endpoint URL, auth method, audio frame format, keepalive/finalization protocol, and response JSON.
- Default model ID.
- Language behavior for Vietnamese and English.
- Rate limits, max audio size/duration, and concurrency limits.
- Error response shape.
- Data retention/privacy constraints.

Do not start coding if endpoint, auth, model, response shape, and audio format are unclear.

## Naming

Use `{Provider}` as the display and class prefix, for example `ElevenLabs`, `AssemblyAI`, or `Deepgram`.

Provider files live under:

```text
Services/STT/Providers/{Provider}/
```

Namespaces should follow:

```csharp
BF_STT.Services.STT.Providers.{Provider}
```

## Implementation Checklist

### 1. Response Models

Create response model classes for the provider API:

```text
Services/STT/Providers/{Provider}/{Provider}Response.cs
```

Requirements:
- Model enough JSON to extract final transcript text.
- Include streaming response models if streaming JSON differs from batch JSON.
- Keep provider DTOs inside the provider folder.
- Do not expose API key or request body details through `ToString()` or logging.

### 2. Batch Service

If the provider supports batch transcription, create:

```text
Services/STT/Providers/{Provider}/{Provider}BatchService.cs
```

Requirements:
- Implement or inherit from the existing batch abstraction pattern.
- Constructor should accept `HttpClient`, API key, base URL, and model where applicable.
- Validate missing API key before network calls.
- Send the audio format required by the provider.
- Parse provider errors without exposing credentials.
- Support cancellation.
- Follow existing retry/error style used by current providers where appropriate.

### 3. Streaming Service

If the provider supports streaming, create:

```text
Services/STT/Providers/{Provider}/{Provider}StreamingService.cs
```

Requirements:
- Implement `IStreamingSttService` or inherit the shared streaming base pattern when applicable.
- Define lifecycle clearly: `StartAsync`, `SendAudioAsync`, `StopAsync`, `CancelAsync`, cleanup/dispose.
- Send PCM/audio frames in the provider-required format.
- Handle final transcript, interim transcript, utterance/end-of-speech, keepalive, and provider errors.
- Ensure receive/send loops stop on cancellation and disposal.
- Never log API keys or auth headers.

If streaming is not supported, register the provider as batch-only and pass `null` for streaming service in `SttProviderRegistry.Register`.

### 4. Settings

Modify `Services/Infrastructure/SettingsService.cs`:

Add only the settings the provider actually needs, for example:

```csharp
// {Provider}
public string {Provider}ApiKey { get; set; } = "";
public string {Provider}BaseUrl { get; set; } = "{default_batch_url}";
public string {Provider}StreamingUrl { get; set; } = "{default_ws_url}";
public string {Provider}Model { get; set; } = "{default_model}";
```

Rules:
- Do not add streaming URL if the provider cannot stream.
- Keep defaults safe and non-secret.
- Preserve backward compatibility with existing user settings.

### 5. Dependency Injection And Registry

Modify `Services/Infrastructure/ServiceRegistration.cs`:

- Add the provider namespace.
- Instantiate batch and streaming services.
- Register the provider through `SttProviderRegistry`.

Example:

```csharp
var providerBatch = new {Provider}BatchService(
    httpClient,
    settings.{Provider}ApiKey,
    settings.{Provider}BaseUrl,
    settings.{Provider}Model);

var providerStreaming = new {Provider}StreamingService(
    settings.{Provider}ApiKey,
    settings.{Provider}StreamingUrl,
    settings.{Provider}Model);

registry.Register("{Provider}", providerBatch, providerStreaming,
    s => s.{Provider}ApiKey,
    s => s.{Provider}Model);
```

For batch-only providers:

```csharp
registry.Register("{Provider}", providerBatch, null,
    s => s.{Provider}ApiKey,
    s => s.{Provider}Model);
```

Do not add provider-specific if/else chains outside the registry unless unavoidable.

### 6. ViewModel Updates

Modify `ViewModels/MainViewModel.cs` if the provider list or Test Mode transcript properties are currently explicit there.

Requirements:
- Add provider display name where the UI expects explicit provider names.
- Add Test Mode transcript binding if current UI requires one property per provider.
- Prefer registry-driven provider lists if the surrounding code already supports it.

### 7. Settings UI

Modify:

```text
SettingsWindow.xaml
SettingsWindow.xaml.cs
```

Requirements:
- Add API key field and any required model/base URL field if the app exposes them.
- Add provider to batch and/or streaming dropdowns based on real capability.
- Update temporary settings copy, field binding, save path, and selected provider mapping.
- Avoid fragile selected-index logic when a name-based mapping is practical.
- Keep layout rows and bindings consistent.

### 8. Main UI / Test Mode

Modify `MainWindow.xaml` only if the current Test Mode UI uses fixed per-provider panels.

Requirements:
- Add transcript display for the provider if needed.
- Do not break layout for existing providers.
- For batch-only providers, make the UI behavior clear.

### 9. appsettings Defaults

Modify `appsettings.json` only for non-secret defaults:

- Default base URL.
- Default streaming URL if applicable.
- Default model.

Never commit real API keys.

### 10. Tests

Add or update tests under `BF-STT.Tests`:

- Response parsing for representative successful JSON.
- Error parsing or error handling for provider failure JSON.
- Missing API key behavior.
- Empty audio behavior for batch services.
- Cancellation behavior where deterministic.
- Registry behavior if registration logic changes.
- Streaming message parsing if streaming is supported and parsing can be tested without network access.

Automated tests must not call real provider APIs.

### 11. Documentation

Update docs when user-facing setup changes:

- Provider name.
- Batch/streaming support.
- Required API key.
- Default model.
- Any provider-specific limitations.
- Troubleshooting notes for common provider errors.

## Build And Verify

Run:

```powershell
dotnet build
```

Run tests when code behavior changed:

```powershell
dotnet test
```

Manual verification:
- Open Settings and confirm provider fields appear.
- Confirm provider appears only in the modes it supports.
- Save settings and reopen Settings.
- Run batch mode with a configured API key if safe.
- Run streaming mode only if the provider supports it.
- Enable Test Mode and confirm layout remains usable.

## Security Requirements

- Do not commit real API keys.
- Do not log API keys, auth headers, or full request payloads.
- Redact provider error details if they include request metadata or credentials.
- Confirm release artifacts do not include local settings or secrets.

## Completion Report

When finished, report:
- Provider capability: batch, streaming, or both.
- Files changed.
- Settings added.
- Tests added or skipped with reason.
- Build/test result.
- Manual verification still needed.
