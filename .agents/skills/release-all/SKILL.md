---
name: Release All Platforms
description: Bump the app version and publish a single GitHub Release for BF-STT with all relevant platform artifacts attached (Windows .exe, macOS .app, Android .apk). Trigger phrases include "release phiên bản hiện tại", "release toàn bộ platform", "full release", "release all platforms".
---

# Release All Platforms

Use this skill when the user asks to cut a full release across platforms — not just the
Windows-only quick release covered by `.agents/workflows/release.md`.

## What this does

1. Bumps `<Version>` (and `AssemblyVersion`/`FileVersion`/`InformationalVersion`) in
   `BF-STT.csproj` by one patch version.
2. Syncs `BF-STT.Android/BF-STT.Android.csproj`'s `<ApplicationDisplayVersion>` to the
   **same** version number (so Windows, macOS, and Android all ship as e.g. `1.0.283`
   under one tag). `<ApplicationVersion>` (the Android versionCode) still increments by
   1 on top of that, since it must strictly increase for every installable APK.
3. Commits both csproj changes as `Bump version to X.X.X for release`.
4. Builds every platform relevant to the host OS (see matrix below).
5. Tags the commit `vX.X.X`, pushes the branch + tag, and creates one GitHub Release
   with all built artifacts attached and auto-generated notes (grouped from commit
   subjects since the last tag into New Features / Bug Fixes / Improvements / Other).

## How to run it

- **From macOS**: `./scripts/release-all.sh`
- **From Windows**: `powershell -ExecutionPolicy Bypass -File "scripts/release-all.ps1"`

Both scripts are `// turbo-all` safe to run directly — they only touch this repo, a git
tag/push, and a GitHub Release; nothing outside the project.

## Platform coverage matrix

| Host OS | Windows .exe | macOS .app (zipped) | Android .apk |
|---|---|---|---|
| macOS   | ✅ cross-published single-file (`dotnet publish -f net8.0-windows -r win-x64`) | ✅ native build via `scripts/build-mac.sh` | ✅ |
| Windows | ✅ native build | ❌ skipped — can't build a macOS bundle from Windows | ✅ |

Asset naming in the GitHub Release: `BF-STT-Windows-vX.X.X.exe`,
`BF-STT-macOS-vX.X.X.zip`, `BF-STT-Android-vX.X.X.apk`.

## Android SDK resolution

Android builds no longer require a manually-provisioned Android Studio SDK. Both
`BF-STT.Android/build-apk.sh` (macOS/Linux) and `BF-STT.Android/build-apk.ps1`
(Windows) resolve `ANDROID_HOME`/`JAVA_HOME` in this order:

1. Already-exported `ANDROID_HOME`/`ANDROID_SDK_ROOT` and `JAVA_HOME`.
2. A standalone Android Studio SDK at its default install location
   (`~/Library/Android/sdk` on macOS, `%LOCALAPPDATA%\Android\Sdk` on Windows).
3. The Android SDK + OpenJDK **bundled inside an installed Unity Editor** (requires the
   "Android Build Support" module in Unity Hub) — the newest installed editor version
   wins. This is what makes the skill work on a machine that only has Unity installed,
   with no separate Android Studio setup.

See `.agents/scripts/find-android-sdk.sh` / `.agents/scripts/find-android-sdk.ps1` for
the exact search logic.

## Preflight

Both scripts abort before making any changes if:
- `gh` isn't installed or isn't authenticated (`gh auth login`).
- The git working tree isn't clean (`git status --short` must be empty) — commit or
  stash unrelated work first so the release commit only contains the version bump.
- A tag for the computed version already exists.

## Failure handling

- Any platform build failure aborts the release **before** the git tag/push/GitHub
  Release step — nothing is pushed or published on a partial build.
- A failed run does leave the version bump **uncommitted** in the working tree (the
  commit only happens after all builds succeed). Before retrying, reset with:
  ```
  git checkout -- BF-STT.csproj BF-STT.Android/BF-STT.Android.csproj
  ```
  otherwise the next run will bump the version again on top of the already-bumped
  (but uncommitted) files.
- The macOS `.app` bundle is unsigned/not notarized (matches the existing
  `build-mac.sh` behavior) — Gatekeeper will warn on first launch on another Mac.

## Relevant files

- `scripts/release-all.sh`, `scripts/release-all.ps1` — orchestrators.
- `scripts/build-mac.sh` — macOS build (now stamps `Info.plist` from the real csproj
  version via `APP_VERSION`, instead of a hardcoded string).
- `BF-STT.Android/build-apk.sh`, `BF-STT.Android/build-apk.ps1` — Android build.
- `.agents/scripts/bump-android-version.sh`, `.agents/scripts/bump-android-version.ps1`
  — version bump/sync (accepts an optional target version to sync to the main app
  version; with none given, self-increments patch — this is what the standalone
  `build-android` skill still uses for one-off Android-only builds).
- `.agents/scripts/find-android-sdk.sh`, `.agents/scripts/find-android-sdk.ps1` — SDK
  auto-detection.

This skill builds on top of, but does not replace, the existing single-platform flows:
`.agents/workflows/release.md` (Windows-only quick release) and the `build-android`
skill (Android-only build without touching the main app version).
