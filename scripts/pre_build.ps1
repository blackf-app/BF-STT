param(
    [string]$processName = "BF-STT"
)

# Standardize process name (remove .exe if present)
$processName = $processName -replace "\.exe$", ""

$processes = Get-Process -Name $processName -ErrorAction SilentlyContinue

# Marker file location (using project directory / obj to be specific to this project)
# We use a file in the 'obj' folder so it's project-specific and cleaned on Clean
$markerFile = Join-Path $PSScriptRoot "..\obj\was_running.tmp"

if ($processes) {
    Write-Host "[Pre-Build] Process $processName is running. Creating marker and stopping..."
    if (!(Test-Path (Split-Path $markerFile))) {
        New-Item -ItemType Directory -Path (Split-Path $markerFile) -Force | Out-Null
    }
    New-Item -Path $markerFile -ItemType File -Force | Out-Null
    
    # Try to stop gracefully first, then force
    foreach ($p in $processes) {
        $p.CloseMainWindow() | Out-Null
    }
    Start-Sleep -Seconds 1
    $processes = Get-Process -Name $processName -ErrorAction SilentlyContinue
    if ($processes) {
        $processes | Stop-Process -Force
    }
    Write-Host "[Pre-Build] Process stopped."
} else {
    Write-Host "[Pre-Build] Process $processName is not running. No action needed."
    # Ensure no stale marker exists
    if (Test-Path $markerFile) {
        Remove-Item $markerFile -Force
    }
}
