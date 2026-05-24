# BF-STT Release Rules

Use these rules when changing build, publish, versioning, or release automation.

## Build Behavior

- Debug builds must not publish automatically.
- Release publish must not recurse through the `AutoPublishOnRelease` target.
- Version changes must be deliberate and visible in `BF-STT.csproj`.
- `bin/`, `obj/`, and `publish/` output must stay uncommitted.

## Preflight

Before release:
- Check `git status --short`.
- Confirm no local secret files are staged.
- Run `dotnet build`.
- Run `dotnet test` when behavior changed.
- Run publish command or release script as appropriate.
- Inspect publish output for expected executable and absence of secrets/logs/settings.

## PowerShell

- Quote paths.
- Use `-LiteralPath` for file operations when possible.
- Validate target paths before cleanup.
- Fail fast on errors.
- Print clear final output paths.

## Artifact Rules

- Publish output should contain only files needed to run BF-STT.
- Do not package user logs, user settings, API keys, or local debug artifacts.
- Release notes should mention version, important changes, and manual verification.
