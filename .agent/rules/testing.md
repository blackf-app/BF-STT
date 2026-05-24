# BF-STT Testing Rules

Use these rules when adding or reviewing tests.

## Stack

- Use `xUnit` for tests.
- Use `NSubstitute` or small local test doubles for collaborators.
- Do not require real API keys, real provider network access, real microphone input, or real clipboard state in automated tests.

## Coverage Priorities

- Provider base class validation and error behavior.
- Provider response parsing with representative JSON samples.
- `SttProviderRegistry` registration, fallback, capability, validation, and settings update behavior.
- `HallucinationFilter`, silence detection, AGC, VAD, and audio boundary logic.
- Workflow stop/cancel/failure transitions.
- Settings serialization and default handling.

## Test Shape

- Name tests as `MethodOrScenario_Condition_ExpectedResult`.
- Keep tests deterministic and isolated.
- Prefer small focused tests over broad tests with many assertions.
- Use boundary inputs: empty audio, null values, missing key, invalid language, cancellation, timeout, provider error, and malformed response.

## Manual Verification

When behavior is hard to automate, include a manual checklist:
- Settings window fields and dropdowns.
- Batch mode.
- Streaming mode.
- Hybrid short-press/long-press behavior.
- Test Mode provider display.
- Clipboard restoration.
- Published app startup.
