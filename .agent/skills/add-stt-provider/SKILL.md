---
name: Add STT Provider
description: Checklist and instructions for adding a new Speech-to-Text API provider to BF-STT. Use this skill whenever the user asks to integrate a new STT API.
---

# Add STT Provider

This skill defines the **complete checklist** of files to create and modify when adding a new STT provider. **Do NOT skip any step.**

## Prerequisites

Before starting, research the new provider's API:
- **Batch endpoint**: URL, auth method, request/response format
- **Streaming endpoint** (if available): WebSocket URL, auth method, message format
- **Model IDs**: default model name(s)
- **Auth header**: e.g. `Authorization: Bearer`, `xi-api-key`, `Token`, etc.

## Checklist (8 files, all required)

Use `{Provider}` as the provider name (e.g. `ElevenLabs`, `AssemblyAI`).

### 1. [NEW] Response Model
**File**: `Services/STT/Providers/{Provider}/{Provider}Response.cs`
- Namespace: `BF_STT.Services.STT.Providers.{Provider}`
- Model the JSON response from the batch API (at minimum: `Text` property)

### 2. [NEW] Batch Service
**File**: `Services/STT/Providers/{Provider}/{Provider}BatchService.cs`
- Inherit from `BaseBatchSttService`
- Constructor: `(HttpClient httpClient, string apiKey, string baseUrl, string model)`
- Call `base(httpClient, apiKey, baseUrl, defaultBaseUrl, defaultModel)`
- Override `TranscribeCore(byte[] audioData, string language, CancellationToken ct)`
- Follow the same retry pattern as existing providers (see `DeepgramService.cs`)

### 3. [NEW] Streaming Service (if provider supports WebSocket streaming)
**File**: `Services/STT/Providers/{Provider}/{Provider}StreamingService.cs`
- Implement `IStreamingSttService, IDisposable`
- Follow the exact pattern of `DeepgramStreamingService.cs`:
  - `StartAsync` → connect WebSocket with auth + query params
  - `SendAudioAsync` → send binary PCM frames
  - `StopAsync` → send EOS/close message, wait for final results
  - `CancelAsync` → cancel without waiting
  - `ReceiveLoopAsync` → read JSON messages, fire `TranscriptReceived`/`UtteranceEndReceived`
  - `KeepAliveLoopAsync` → periodic ping
  - `CleanupAsync` → dispose WebSocket + CTS
- If streaming is NOT supported, pass `null` as streamingService in step 5

### 4. [MODIFY] AppSettings
**File**: `Services/Infrastructure/SettingsService.cs` → class `AppSettings`
- Add properties (4-5 fields):
  ```csharp
  // {Provider}
  public string {Provider}ApiKey { get; set; } = "";
  public string {Provider}BaseUrl { get; set; } = "{default_batch_url}";
  public string {Provider}StreamingUrl { get; set; } = "{default_ws_url}"; // if streaming supported
  public string {Provider}Model { get; set; } = "{default_model}";
  ```
- Place BEFORE the `// Noise Suppression` comment

### 5. [MODIFY] Service Registration (DI)
**File**: `Services/Infrastructure/ServiceRegistration.cs`
- Add `using BF_STT.Services.STT.Providers.{Provider};` at the top
- Inside the `SttProviderRegistry` factory, add:
  ```csharp
  // {Provider}
  var {provider}Batch = new {Provider}BatchService(httpClient, settings.{Provider}ApiKey, settings.{Provider}BaseUrl, settings.{Provider}Model);
  var {provider}Streaming = new {Provider}StreamingService(settings.{Provider}ApiKey, settings.{Provider}StreamingUrl, settings.{Provider}Model);
  registry.Register("{Provider}", {provider}Batch, {provider}Streaming,
      s => s.{Provider}ApiKey, s => s.{Provider}Model);
  ```
  - If no streaming: pass `null` instead of streaming service instance

### 6. [MODIFY] MainViewModel
**File**: `ViewModels/MainViewModel.cs`
- Add `"{Provider}"` to the `AvailableApis` collection (line ~83)
- Add transcript property for Test Mode:
  ```csharp
  public string {Provider}Transcript
  {
      get => _coordinator.GetProviderTranscript("{Provider}");
      set => OnPropertyChanged();
  }
  ```

### 7. [MODIFY] SettingsWindow XAML + Code-Behind
**Files**: `SettingsWindow.xaml` + `SettingsWindow.xaml.cs`

#### SettingsWindow.xaml:
- Add `<RowDefinition Height="Auto"/>` to the Grid
- Add `<ComboBoxItem Content="{Provider}"/>` to **both** `BatchModeApiComboBox` and `StreamingModeApiComboBox`
- Add a new `StackPanel` with `TextBlock` label + `TextBox` for the API key (follow existing pattern)
- Shift all subsequent `Grid.Row` indices by +1

#### SettingsWindow.xaml.cs:
- **Constructor**: copy `{Provider}ApiKey` (and other settings) into `_tempSettings`
- **Constructor**: bind `{Provider}ApiKeyTextBox.Text = _tempSettings.{Provider}ApiKey`
- **Constructor**: add `else if (_tempSettings.BatchModeApi == "{Provider}") BatchModeApiComboBox.SelectedIndex = {N};` (same for Streaming)
- **SaveButton_Click**: read `_tempSettings.{Provider}ApiKey = {Provider}ApiKeyTextBox.Text;`
- **SaveButton_Click**: add `if (BatchModeApiComboBox.SelectedIndex == {N}) _tempSettings.BatchModeApi = "{Provider}";` (same for Streaming)
- ⚠️ Update ALL existing index checks — new provider shifts indices

### 8. [MODIFY] MainWindow.xaml (Test Mode area)
**File**: `MainWindow.xaml`
- In the Test Mode transcript `<Grid>` (inside `IsTestMode` triggered border):
  - Add 2 more `<ColumnDefinition>` (`Width="5"` spacer + `Width="*"`)
  - Add a new `<DockPanel>` with label + readonly TextBox binding to `{Provider}Transcript`

### 9. [MODIFY] appsettings.json (optional defaults)
**File**: `appsettings.json`
- Add a new section with default config values

## Build & Verify

After all changes:
1. Run `dotnet build` — must have **0 errors**
2. Launch app → open Settings → verify new provider appears in **both** Batch and Streaming dropdowns
3. Verify API Key input field is present
4. If Test Mode is enabled, verify the new provider's transcript panel is visible
