---
name: Release Preflight
description: Checklist for validating BF-STT before publishing or creating a release artifact.
---

# Release Preflight

Use this skill before running release automation or publishing a distributable build.

## Checklist

1. Inspect repository state:
   ```powershell
   git status --short
   ```
   Confirm no secrets, logs, user settings, `bin/`, `obj/`, or `publish/` files are staged.

2. Validate project metadata:
   - `BF-STT.csproj` version fields are correct.
   - `TargetFramework` remains expected.
   - Publish settings match the intended artifact type.

3. Build:
   ```powershell
   dotnet build
   ```

4. Test when code behavior changed:
   ```powershell
   dotnet test
   ```

5. Publish:
   ```powershell
   dotnet publish -c Release -o ./publish
   ```

6. Inspect publish output:
   - `BF-STT.exe` exists.
   - No API keys or local settings files are present.
   - No logs or unrelated build folders are included.

7. Optional smoke test:
   - Start app from `publish/BF-STT.exe`.
   - Open Settings.
   - Confirm configured providers load.
   - Run a short batch recording if a safe local setup is available.

## Failure Handling

- Stop release if build or tests fail.
- Stop release if publish output contains secrets or local-only files.
- Stop release if version metadata is inconsistent.
- Report the failing command and the shortest useful remediation.
