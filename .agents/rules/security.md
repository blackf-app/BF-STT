# BF-STT Security Rules

Use these rules when handling settings, providers, logs, clipboard, input injection, or release artifacts.

## Secrets

- Do not commit API keys, auth headers, or local secret config.
- Do not print API keys in logs, exceptions, debug output, test output, or release notes.
- Redact values when diagnostic output must mention credential presence.
- Treat provider base URLs, models, and auth header formats as less sensitive than API keys, but still avoid noisy logging.

## Settings

- Keep secret persistence centralized in the existing settings infrastructure.
- Any new setting must have a default, save path, load path, and UI behavior if user-configurable.
- Settings migration should be backward compatible with existing user installs.

## Provider Calls

- Build auth headers per provider and do not reuse one provider's header convention for another provider.
- Validate missing credentials before network calls where possible.
- Handle provider errors without exposing credentials or raw request bodies.
- Do not send audio to a provider unless the user-triggered workflow requires it.

## Platform Integration

- Clipboard operations must preserve and restore existing user content where possible.
- Input injection must happen only after explicit user-triggered workflow states.
- Hotkey handling must not create repeated unintended actions when a key is held or interrupted.

## Release

- Release artifacts must not include local secrets, logs, user settings, or unrelated build output.
- PowerShell scripts must quote paths and validate risky targets before deleting or moving files.
