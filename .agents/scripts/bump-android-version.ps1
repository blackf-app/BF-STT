<#
.SYNOPSIS
  Bump the BF-STT Android app version. PowerShell port of bump-android-version.sh.

.DESCRIPTION
  * Normalizes <ApplicationDisplayVersion> to the x.x.x form (e.g. 1.2 -> 1.2.0).
  * Increments the patch component (1.2.0 -> 1.2.1), unless -TargetDisplayVersion
    is given, in which case <ApplicationDisplayVersion> is set to exactly that value
    instead — used by scripts/release-all.ps1 to keep the Android display version in
    lockstep with the main app version.
  * Increments <ApplicationVersion> (Android versionCode) by 1 either way — it must
    strictly increase for every installable build.

  Called automatically by BF-STT.Android/build-apk.ps1 on every build (see -NoBump).
#>
param (
    [string]$CsprojPath = (Join-Path $PSScriptRoot "..\..\BF-STT.Android\BF-STT.Android.csproj"),
    [string]$TargetDisplayVersion = ""
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $CsprojPath)) {
    Write-Error "csproj not found: $CsprojPath"
    exit 1
}

$content = Get-Content $CsprojPath -Raw

$dispMatch = [regex]::Match($content, "<ApplicationDisplayVersion>([^<]*)</ApplicationDisplayVersion>")
$codeMatch = [regex]::Match($content, "<ApplicationVersion>([^<]*)</ApplicationVersion>")

if (-not $dispMatch.Success) { Write-Error "<ApplicationDisplayVersion> not found in $CsprojPath"; exit 1 }
if (-not $codeMatch.Success) { Write-Error "<ApplicationVersion> not found in $CsprojPath"; exit 1 }

$disp = $dispMatch.Groups[1].Value
$code = [int]$codeMatch.Groups[1].Value

if ($TargetDisplayVersion) {
    $newDisp = $TargetDisplayVersion
} else {
    $parts = $disp.Split(".")
    $major = if ($parts.Length -ge 1) { [int]($parts[0] -replace '\D', '') } else { 1 }
    $minor = if ($parts.Length -ge 2) { [int]($parts[1] -replace '\D', '') } else { 0 }
    $patch = if ($parts.Length -ge 3) { [int]($parts[2] -replace '\D', '') } else { 0 }
    $newPatch = $patch + 1
    $newDisp = "$major.$minor.$newPatch"
}
$newCode = $code + 1

$content = $content -replace "<ApplicationDisplayVersion>[^<]*</ApplicationDisplayVersion>", "<ApplicationDisplayVersion>$newDisp</ApplicationDisplayVersion>"
$content = $content -replace "<ApplicationVersion>[^<]*</ApplicationVersion>", "<ApplicationVersion>$newCode</ApplicationVersion>"
[System.IO.File]::WriteAllText($CsprojPath, $content)

Write-Host ">> Android version bumped: $disp (code $code) -> $newDisp (code $newCode)"
