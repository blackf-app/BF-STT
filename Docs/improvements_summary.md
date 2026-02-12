# Implementation Plan - Improvements

I have implemented the following features as requested:

## 1. Sound Notifications
- Created `Services/SoundService.cs` to generate and play gentle beep sounds.
- **Start Sound:** A high-pitched tone (880Hz, 150ms) with fade-in/out.
- **Stop Sound:** A lower-pitched tone (440Hz, 150ms) with fade-in/out.
- Sounds play asynchronously without blocking the UI.

## 2. Temp File Auto-Cleanup
- The temporary WAV file created during recording is now automatically deleted after the transcription attempt (whether successful or failed).
- This logic is in `MainViewModel.SendToDeepgramAsync`'s `finally` block.

## 3. Recording Timer
- Added a `DispatcherTimer` to `MainViewModel`.
- Updates `StatusText` every second to show the duration (e.g., `Recording... 00:05`).
- Does not affect window size as it reuses the existing status bar.

## 4. API Retry Mechanism
- Modified `DeepgramService.TranscribeAsync` to retry the API call **once** if it fails with a network or HTTP error.
- Added a 500ms delay between attempts.
- Properly recreates the `HttpRequestMessage` for the retry attempt.

## Verification
- **Build:** Success (Release build passed).
- **Manual Test:** 
    - Press F3 (or Start button) -> Hear Start beep -> See timer counting up "00:01", "00:02"...
    - Press F3 again (or Stop button) -> Hear Stop beep -> Timer stops -> Status changes to "Sending...".
    - After completion, the temp file in `%TEMP%` should be gone.
    - Disconnect internet -> Try recording -> Should see it try longer (retry) before showing error.
