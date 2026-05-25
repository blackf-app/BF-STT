### [HIGH] Add TTS from clipboard with F2 hotkey

**Completed:** 2026-05-25

**Summary:**
Implemented a TTS provider registry, provider services for Deepgram, Speechmatics, Soniox, OpenAI, ElevenLabs, Google, and Azure, explicit AssemblyAI unavailable handling, F2 clipboard synthesis workflow, local audio playback, separate STT/TTS settings, hotkey validation, and focused TTS registry tests.

**Quality gates:**
- Preflight: pass (rounds: 2)
- Code review: pass (rounds: 1)
- Security review: pass (rounds: 1)
- QA verify: pass (rounds: 1)

**Manual verify steps:**
1. Copy a short Vietnamese sentence to the clipboard.
2. Configure a supported TTS provider and API key in Settings under the TTS category.
3. Press F2 and confirm audio is generated and played automatically.
4. Copy empty/whitespace text or clear clipboard text, press F2, and confirm no provider request is made.
5. Confirm F3/F4 STT workflows still behave as before.
6. Reopen Settings and confirm STT keys and TTS keys persist independently.
7. Restart the app with an existing STT-only settings file and confirm startup does not lose STT configuration.
