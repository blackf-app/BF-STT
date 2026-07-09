# BF-STT for Android

A minimal Android port of BF-STT. It **reuses the desktop project's STT provider logic
verbatim** (the HTTP-only batch services) and wraps it in a lightweight, Android-native UX:

> **Floating microphone bubble** over every app:
> - **Tap** → start recording
> - **Tap again** → stop, transcribe, and paste the text into the focused field
> - **Hold (long-press)** → open Settings
> - **Drag** → move the bubble anywhere on screen

This is intentionally *not* a 1:1 copy of the desktop UI — only the core dictation logic
is shared. No Avalonia, no NAudio, no OpenAL, no RNNoise: those are desktop-only. Android
uses its own `AudioRecord` mic capture and a native overlay written in C# (.NET for Android).

Verified running on an Android 15 (API 35) arm64 emulator and on Redmi HyperOS (Android 16).

---

## What is reused from the desktop project

Linked directly via `<Compile Include="..\Services\STT\...">` in `BF-STT.Android.csproj`
(same source files, same `BF_STT.Services.STT.*` namespaces — no fork):

| Reused file | Purpose |
|---|---|
| `Services/STT/Abstractions/IBatchSttService.cs` | batch STT contract |
| `Services/STT/Abstractions/BaseBatchSttService.cs` | shared boilerplate/validation |
| `Services/STT/Providers/Deepgram/DeepgramService.cs` (+`DeepgramResponse`) | Deepgram batch |
| `Services/STT/Providers/OpenAI/OpenAIBatchService.cs` | OpenAI Whisper batch |
| `Services/STT/Providers/ElevenLabs/ElevenLabsBatchService.cs` (+`ElevenLabsResponse`) | ElevenLabs Scribe batch |
| `Services/STT/Filters/HallucinationFilter.cs` | drops Whisper "thanks for watching"-type noise |

## Android-specific code (in this folder)

| File | Role |
|---|---|
| `MainActivity.cs` | config screen (provider, API key, language) + in-app record test; also the "settings" opened on long-press |
| `BubbleService.cs` | foreground service drawing the floating bubble; tap/hold/drag gestures; recording → transcribe → deliver |
| `Audio/AndroidAudioRecorder.cs` | mono 16 kHz 16-bit PCM capture via `AudioRecord` |
| `Audio/WavWriter.cs` | wraps PCM into a WAV buffer the STT providers accept |
| `Stt/TranscriptionEngine.cs` | picks the provider from settings, calls the reused service, applies the hallucination filter |
| `PasteAccessibilityService.cs` | optional: finds the focused editable field and inserts text (PASTE → SET_TEXT fallback) |
| `AppSettings.cs` | `SharedPreferences`-backed settings (per-provider API keys, `BubbleEnabled` flag) |
| `BootReceiver.cs` | re-launches the bubble service after reboot (if the user had it enabled) |
| `BfSttApp.cs` | `Application` subclass; initialises settings before any component |

Text delivery: always copied to the **clipboard**; if the accessibility service is enabled,
it is also **inserted** straight into the currently focused text field of whatever app is open.

## Keeping the bubble alive (always-on)

The bubble is meant to stay up like a system helper. To make that reliable:

- The foreground service runs as a **`specialUse`** FGS while idle and only elevates to the
  **`microphone`** type while actually recording (dropping it again afterwards). `specialUse`
  is used because a `microphone`-typed service cannot be (re)started from the background,
  which would break the recovery paths below. *(specialUse requires API 34+; older devices
  fall back to the microphone type.)*
- **Survives swipe-from-Recents / process kill:** `BubbleService.OnTaskRemoved` schedules a
  ~1 s `AlarmManager` restart (a `startForegroundService` `PendingIntent` that outlives the
  process). Combined with `START_STICKY`.
- **Survives reboot:** `BootReceiver` re-starts the service on `BOOT_COMPLETED` /
  `QUICKBOOT_POWERON` when `BubbleEnabled` is set and the overlay permission is still granted.

> ⚠️ **OEM battery killers (Xiaomi/HyperOS, Oppo, Vivo, Huawei…).** No app-side code can
> fully override an OEM that force-kills background processes. The user must grant, once:
> **Autostart** and **"No battery restrictions"**, and it helps to **lock the app in Recents**.
> The settings screen has buttons for both (`Tắt tối ưu pin`, `Mở Tự khởi động`). A normal
> sideloaded APK cannot become a true persistent system process (`android:persistent` is
> system-app-only) — this is as close as it gets without rooting the device.

---

## Toolchain setup (one-time, per build machine)

macOS (Apple Silicon) example — adjust versions/paths as needed:

```bash
# 1. .NET android workload (installs the API-34 Mono.Android packs)
~/.dotnet/dotnet workload install android

# 2. A JDK 17 (Microsoft OpenJDK shown here)
curl -L -o msjdk17.tgz https://aka.ms/download-jdk/microsoft-jdk-17-macos-aarch64.tar.gz
mkdir -p ~/android-jdk && tar -xzf msjdk17.tgz -C ~/android-jdk

# 3. Android SDK platform + build-tools matching the workload (API 34)
export ANDROID_HOME="$HOME/Library/Android/sdk"
yes | "$ANDROID_HOME/cmdline-tools/latest/bin/sdkmanager" \
  "platforms;android-34" "build-tools;34.0.0" "platform-tools"
```

The build script below already exports `DOTNET_ROOT`, `JAVA_HOME`, `ANDROID_HOME` to point at
these locations.

## Build

```bash
cd BF-STT.Android
./build-apk.sh            # Release (default) -> SIGNED, self-contained, installable APK
./build-apk.sh Debug      # Debug variant (also self-contained)
```

The script sets the toolchain env vars, auto-creates a self-signed release keystore
(`bfstt-release.keystore`, gitignored) and runs `dotnet publish`.

**Output APK:** `bin/Release/net8.0-android/vn.easygoing.bfstt-Signed.apk`
(a copy is also placed at `../dist/BF-STT-android-<version>-release.apk`, ~11 MB).

- Package: `vn.easygoing.bfstt` · minSdk **26** (Android 8) · targetSdk **34**
- ABIs: `arm64-v8a`, `armeabi-v7a`, `x86`, `x86_64` (universal)
- Signed with a release cert (v2+v3 schemes); self-contained (all managed assemblies embedded)

### Build configuration that matters (set in `BF-STT.Android.csproj`)

| Property | Why |
|---|---|
| `EmbedAssembliesIntoApk=true` | makes the APK **self-contained** so a sideloaded APK doesn't crash with *"No assemblies found … Exiting"* (that abort means it expected IDE Fast Deployment) |
| `AndroidEnableMarshalMethods=false` | the marshal-methods JNI path (`[UnmanagedCallersOnly]`) trips a Mono startup assertion `!only_unmanaged_callers_only` → SIGABRT before `MainActivity` loads, on this workload (34.0.43) |
| `JsonSerializerIsReflectionEnabledByDefault=true` | keeps reflection-based `System.Text.Json` transcript parsing working under Release trimming |

---

## Install & run on a device

```bash
export ANDROID_HOME="$HOME/Library/Android/sdk"
"$ANDROID_HOME/platform-tools/adb" install -r dist/BF-STT-android-1.0-release.apk
```
…or copy the `.apk` to the phone and open it (enable "install unknown apps").

> Rebuilds are signed with the **same** keystore, so `install -r` updates in place.
> If you ever switch keystores, the signature changes → `adb uninstall vn.easygoing.bfstt` first.

### First-run setup
1. Grant **Microphone** and **Notifications** when prompted.
2. Enter your **provider + API key** (Deepgram / OpenAI / ElevenLabs), set language (`vi`), **Save**.
3. Tap **"Bật icon nổi"** → grant **"Display over other apps"**. The floating mic appears.
4. Use **"Thu âm"** on the main screen to test the pipeline without the overlay.

### Enabling auto-paste into other apps (accessibility)
Without this, transcripts still land on the clipboard (long-press a field → Paste). To make
the bubble type straight into the focused field:

1. Tap **"Mở cài đặt Trợ năng"** (or Settings → Additional settings → Accessibility).
2. Under *Downloaded/Installed services* pick **BF-STT Paste** and turn it **on**; confirm the
   "full control" dialog (wait for the button to become tappable).
3. In use: **tap into the target text field first** (blinking cursor), then tap the bubble to
   record, tap again to stop → the text is inserted.

**Xiaomi / HyperOS / MIUI extra steps** (sideloaded apps are restricted):
- Settings → Apps → BF-STT → ⋮ → **Allow restricted settings** — do this *before* enabling the
  service, otherwise the toggle is greyed out.
- Settings → Apps → BF-STT → **Autostart: ON**, and Battery saver → **No restrictions**, so the
  service isn't killed in the background.

The bubble's toast tells you what happened: `Da dan` = inserted, `Da copy (bat Tro nang…)` =
service not enabled, `Da copy` = auto-paste off or no field focused.

---

## Troubleshooting
- **`SIGABRT` / `abort_application` at launch, logcat shows
  `Assertion … condition '!only_unmanaged_callers_only' not met`** — marshal methods;
  fixed by `AndroidEnableMarshalMethods=false` (see build-config table).
- **`No assemblies found … Exiting`** — APK not self-contained; `EmbedAssembliesIntoApk=true` fixes it.
- Reproduce/inspect on an emulator: `adb logcat -d | grep -iE "Assertion|SIGABRT|abort_application"`.

## Notes / limitations (MVP)
- **Batch mode only** (record fully → one request). The desktop streaming mode is not ported.
- Cross-app auto-paste needs the accessibility service; otherwise text lands on the clipboard.
- Signed with a **self-signed dev keystore** (`bfstt-release.keystore`, pass `bfstt123`). For a
  Play Store release, replace it with your own keystore.
