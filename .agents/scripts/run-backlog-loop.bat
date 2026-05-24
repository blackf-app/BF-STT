@echo off
REM Default backlog loop entrypoint.
REM Change this target if your default provider is Codex or Gemini.

setlocal
set "SCRIPT_DIR=%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%run-backlog-loop-claude.ps1" %*
endlocal
