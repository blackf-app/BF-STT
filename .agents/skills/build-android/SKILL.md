---
name: Build Android
description: Build the BF-STT Android APK. Every build auto-increments the version (x.x.x display version + Android versionCode).
---

# Build Android

Use this skill to produce an installable BF-STT Android APK.

## Versioning contract

Every Android build **bumps the version by 1**:

- `ApplicationDisplayVersion` is kept in the **`x.x.x`** form (e.g. `1.2.1`). The **patch**
  component is incremented on each build. A two-part version like `1.2` is normalized to
  `1.2.0` before incrementing.
- `ApplicationVersion` (the Android `versionCode`) is incremented by 1 so every installable
  APK strictly supersedes the previous one.

Both fields live in `BF-STT.Android/BF-STT.Android.csproj`.

The bump is performed automatically by `.agents/scripts/bump-android-version.sh`, which
`BF-STT.Android/build-apk.sh` invokes at the start of every build.

## Steps

1. From the repo root, build (Release is signed & self-contained):
   ```bash
   ./BF-STT.Android/build-apk.sh
   ```
   This auto-bumps the version, then runs `dotnet publish`. Debug builds:
   ```bash
   ./BF-STT.Android/build-apk.sh Debug
   ```

2. To rebuild **without** bumping the version (e.g. retrying a failed build), skip the bump:
   ```bash
   ./BF-STT.Android/build-apk.sh --no-bump
   # or:  BF_NO_BUMP=1 ./BF-STT.Android/build-apk.sh
   ```

3. To bump the version manually without building:
   ```bash
   .agents/scripts/bump-android-version.sh
   ```

## Output

The signed APK is printed at the end of the build. It lives under:

```
BF-STT.Android/bin/Release/net8.0-android/*-Signed.apk
```

and is also copied into the repo-level `dist/` folder with a versioned name:

```
dist/BF-STT-Android-v<x.x.x>.apk
```

The Android application id (package name) is `com.blackface.bfstt` (set via
`<ApplicationId>` in `BF-STT.Android.csproj`).

## Failure handling

- If the bump script cannot find the version fields, it exits non-zero and the build stops;
  check `BF-STT.Android.csproj` still contains `<ApplicationDisplayVersion>` and
  `<ApplicationVersion>`.
- If a build fails after the version was already bumped, re-run with `--no-bump` so the
  version isn't incremented twice for one release.
