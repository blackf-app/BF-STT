<#
.SYNOPSIS
    BF-STT Backlog Loop — Codex (OpenAI) CLI wrapper.

.DESCRIPTION
    Thin wrapper around run-backlog-loop-core.ps1 that sets the Provider to "codex".
    Requires the `codex` CLI (OpenAI Codex CLI) to be installed and configured with
    OPENAI_API_KEY environment variable.

    The core loop will call: codex [--model <Model>] <prompt>

.EXAMPLE
    # Run up to 5 tasks with default model:
    .\.agents\scripts\run-backlog-loop-codex.ps1

    # Run up to 3 tasks with a specific model:
    .\.agents\scripts\run-backlog-loop-codex.ps1 -MaxIterations 3 -Model "o4-mini"

    # Run with a custom log directory:
    .\.agents\scripts\run-backlog-loop-codex.ps1 -LogDir ".agents/logs/codex"
#>

param(
    [int]   $MaxIterations = 10,
    [string]$LogDir        = ".agents/logs/backlog",
    [string]$Model         = ""
)

$scriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$coreScript = Join-Path $scriptDir "run-backlog-loop-core.ps1"

$coreParams = @{
    Provider      = "codex"
    MaxIterations = $MaxIterations
    LogDir        = $LogDir
    # Codex CLI does not use --dangerously-skip-permissions, always pass NoSkipPermissions
    NoSkipPermissions = $true
}

if ($Model) {
    $coreParams["Model"] = $Model
}

& powershell -ExecutionPolicy Bypass -File $coreScript @coreParams
exit $LASTEXITCODE
