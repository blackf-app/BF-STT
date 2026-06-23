# BF-STT Project Context

This project uses the `.agents` directory to store all tools, AI agents, rules, and skills.
Absolutely DO NOT use or search for information in a `.claude` directory. Always reference the `.agents` directory when needed.

- **Skills**: When you need to execute or search for skills, read from `@.agents/skills`.
- **Agents**: When you need to adopt a persona or find specifications for sub-agents (e.g., security-auditor, qa-verifier, bf-code-reviewer...), read from `@.agents/agents`.
- **Rules**: When you need to check coding styles, architectures, or security rules, read from `@.agents/rules`.
- **Workflows**: Automated processes and workflows are located in `@.agents/workflows`.
- **Scripts**: Automation loop scripts or preflight checks are located in `@.agents/scripts`.

All of your actions must strictly adhere to the guidelines defined in this `.agents` directory. Do not automatically create a `.claude/` directory or duplicate any information.

---

## Project Memory & Core Architecture

BF-STT is a high-performance cross-platform Speech-to-Text helper application built on **Avalonia UI** and **.NET 8**. It allows users to quickly dictate text and automatically type/paste it into any active application.

### Key Technical Details
- **UI Framework:** Avalonia UI (configured in Dark Mode).
- **Runtime:** .NET 8 (with dual targets `net8.0` and `net8.0-windows` to handle Windows-specific audio libraries).
- **Audio Processing:** 
  - Uses `NAudio` on Windows for recording audio at 48kHz.
  - Implements **RNNoise (AI)** for noise suppression.
  - Uses **WdlResampling** (Sinc Interpolation) to downsample from 48kHz to 16kHz with anti-aliasing filters.
  - Implements Auto Gain Control (AGC) and average energy RMS-based Voice Activity Detection (VAD).
  - Cross-platform audio fallback uses OpenAL (`OpenTK.Audio.OpenAL`).
- **State Machine:** Governed by `State Pattern` (Idle, Pending, Batch, Streaming, Processing, Failed).

### Critical UI & Window Features
- **Hidden taskbar presence:** `ShowInTaskbar="False"`, `SystemDecorations="None"`, and `TransparencyLevelHint="Transparent"`.
- **Topmost & Z-Order behavior:**
  - The window is aligned to the bottom-center of the screen overlaying the taskbar: `top = screen.Bounds.Y + screen.Bounds.Height - newHeightPx`.
  - **Always-on-Top:** Because Windows often draws the taskbar over topmost windows, `MainWindow.axaml.cs` runs a `DispatcherTimer` every 1000ms calling `ForceTopmost()`.
  - **Z-Order Trick:** `ForceTopmost()` must toggle `this.Topmost = false;` followed by `this.Topmost = true;` to force the Windows DWM (Desktop Window Manager) to reset Z-order and keep the window above the taskbar.

### Important Hotkeys
- **F3 (Tap/Short Press):** Triggers **Batch mode** STT (captures complete audio, transcribes on release).
- **F3 (Hold/Long Press):** Triggers **Streaming mode** (sends live audio buffers and types out text in real-time).
- **F4 (Stop & Send):** Immediately stops the active recording, fetches final text, and simulates **Enter** keypress.
- **Ctrl + Click on Resend:** Re-submits the last recorded audio buffer to a different STT provider.

### Commands
- Build project: `dotnet build`
- Run project (Windows): `dotnet run --framework net8.0-windows`

