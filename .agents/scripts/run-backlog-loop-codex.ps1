[CmdletBinding()]
param(
    [int]$MaxIterations = 100,
    [string]$LogDir = "logs/backlog-loop",
    [AllowEmptyString()]
    [string]$Model = "",
    [switch]$NoSkipPermissions
)

$coreArgs = @{
    Provider = "codex"
    MaxIterations = $MaxIterations
    LogDir = $LogDir
    Model = $Model
}

if ($NoSkipPermissions) {
    $coreArgs.NoSkipPermissions = $true
}

& "$PSScriptRoot\run-backlog-loop-core.ps1" @coreArgs
exit $LASTEXITCODE
