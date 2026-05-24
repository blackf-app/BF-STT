@echo off
REM Wrapper to run the Codex backlog loop with execution policy bypassed.

setlocal
set "SCRIPT_DIR=%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%run-backlog-loop-codex.ps1" %*
endlocal
