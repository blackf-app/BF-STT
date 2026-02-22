param(
    [string]$exePath
)

$markerFile = Join-Path $PSScriptRoot "..\obj\was_running.tmp"

if (Test-Path $markerFile) {
    Write-Host "[Post-Build] Marker file found. Restarting application..."
    Remove-Item -Path $markerFile -Force
    
    # If the path is a DLL (common in .NET 8 $(TargetPath)), change it to EXE
    if ($exePath.EndsWith(".dll", [System.StringComparison]::OrdinalIgnoreCase)) {
        $exePath = $exePath.Substring(0, $exePath.Length - 4) + ".exe"
    }

    if (Test-Path $exePath) {
        Write-Host "[Post-Build] Launching: $exePath"
        # Start-Process without waiting
        Start-Process -FilePath $exePath -WorkingDirectory (Split-Path $exePath)
    }
    else {
        Write-Warning "[Post-Build] Could not find executable at $exePath"
    }
}
else {
    Write-Host "[Post-Build] No restart marker found."
}
