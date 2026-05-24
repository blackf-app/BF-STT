<#
.SYNOPSIS
    BF-STT Backlog Loop — Claude CLI wrapper.

.DESCRIPTION
    Thin wrapper around run-backlog-loop-core.ps1 that sets the Provider to "claude".
    Customize MaxIterations, Model, and LogDir as needed.

.EXAMPLE
    # Run up to 5 tasks with default Claude model:
    .\.agent\scripts\run-backlog-loop-claude.ps1

    # Run up to 3 tasks with a specific model:
    .\.agent\scripts\run-backlog-loop-claude.ps1 -MaxIterations 3 -Model "claude-sonnet-4-5"

    # Run without skipping permissions (interactive approval per tool call):
    .\.agent\scripts\run-backlog-loop-claude.ps1 -NoSkipPermissions
#>

param(
    [int]   $MaxIterations      = 10,
    [string]$LogDir             = ".agent/logs/backlog",
    [string]$Model              = "",
    [switch]$NoSkipPermissions
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$coreScript = Join-Path $scriptDir "run-backlog-loop-core.ps1"

$coreParams = @{
    Provider      = "claude"
    MaxIterations = $MaxIterations
    LogDir        = $LogDir
}

if ($Model) {
    $coreParams["Model"] = $Model
}

if ($NoSkipPermissions) {
    $coreParams["NoSkipPermissions"] = $true
}

& powershell -ExecutionPolicy Bypass -File $coreScript @coreParams
exit $LASTEXITCODE
