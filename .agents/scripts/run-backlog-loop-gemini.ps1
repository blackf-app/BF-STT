<#
.SYNOPSIS
    BF-STT Backlog Loop — Gemini CLI wrapper.

.DESCRIPTION
    Thin wrapper around run-backlog-loop-core.ps1 that sets the Provider to "gemini".
    Requires the `gemini` CLI to be installed and authenticated.

.EXAMPLE
    # Run up to 5 tasks with default Gemini model:
    .\.agents\scripts\run-backlog-loop-gemini.ps1

    # Run up to 3 tasks with a specific model:
    .\.agents\scripts\run-backlog-loop-gemini.ps1 -MaxIterations 3 -Model "gemini-2.5-pro"

    # Run with a custom log directory:
    .\.agents\scripts\run-backlog-loop-gemini.ps1 -LogDir ".agents/logs/gemini"
#>

param(
    [int]   $MaxIterations = 10,
    [string]$LogDir        = ".agents/logs/backlog",
    [string]$Model         = ""
)

$scriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Path
$coreScript = Join-Path $scriptDir "run-backlog-loop-core.ps1"

$coreParams = @{
    Provider      = "gemini"
    MaxIterations = $MaxIterations
    LogDir        = $LogDir
}

if ($Model) {
    $coreParams["Model"] = $Model
}

& powershell -ExecutionPolicy Bypass -File $coreScript @coreParams
exit $LASTEXITCODE
