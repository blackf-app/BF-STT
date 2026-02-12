param (
    [string]$csprojPath = "BF-STT.csproj"
)

if (-not (Test-Path $csprojPath)) {
    Write-Error "Project file not found: $csprojPath"
    exit 1
}

$content = Get-Content $csprojPath -Raw

# Match <Version>X.Y.Z</Version>
if ($content -match "<Version>(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)</Version>") {
    $major = [int]$Matches['major']
    $minor = [int]$Matches['minor']
    $patch = [int]$Matches['patch']

    $patch++

    if ($patch -gt 999) {
        $patch = 0
        $minor++
    }

    $newVersion = "$major.$minor.$patch"
    
    # Update Version, AssemblyVersion, FileVersion, and InformationalVersion
    $content = $content -replace "<Version>(\d+\.\d+\.\d+)</Version>", "<Version>$newVersion</Version>"
    $content = $content -replace "<AssemblyVersion>(\d+\.\d+\.\d+)</AssemblyVersion>", "<AssemblyVersion>$newVersion</AssemblyVersion>"
    $content = $content -replace "<FileVersion>(\d+\.\d+\.\d+)</FileVersion>", "<FileVersion>$newVersion</FileVersion>"
    $content = $content -replace "<InformationalVersion>(\d+\.\d+\.\d+)</InformationalVersion>", "<InformationalVersion>$newVersion</InformationalVersion>"
    
    [System.IO.File]::WriteAllText($csprojPath, $content)
    Write-Host "Version successfully incremented to $newVersion"
} else {
    Write-Warning "No version tag found in $csprojPath. Ensure <Version>X.Y.Z</Version> exists."
}
