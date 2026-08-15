<#
.SYNOPSIS
  Resolve ANDROID_HOME and JAVA_HOME for building the BF-STT Android APK on Windows,
  without requiring a manually-provisioned Android Studio SDK.

.DESCRIPTION
  Resolution order:
    1. Already-set ANDROID_HOME / ANDROID_SDK_ROOT and JAVA_HOME env vars, if valid.
    2. The Android Studio default SDK location on this machine, if present.
    3. The Android SDK + JDK bundled inside an installed Unity Editor (Android Build
       Support module), auto-detected under the Unity Hub install root. The newest
       installed editor version that has an Android SDK bundle wins.

  Sets $env:ANDROID_HOME, $env:ANDROID_SDK_ROOT, $env:JAVA_HOME in the CALLING scope
  when dot-sourced, e.g.:
    . .\.agents\scripts\find-android-sdk.ps1

  Throws a terminating error with a diagnostic if no SDK/JDK combination is found.
#>

function Test-AndroidSdk {
    param([string]$Path)
    return ($Path -and (Test-Path "$Path\platforms") -and (Test-Path "$Path\build-tools"))
}

function Test-Jdk {
    param([string]$Path)
    return ($Path -and (Test-Path "$Path\bin\java.exe"))
}

$androidHome = $env:ANDROID_HOME
if (-not (Test-AndroidSdk $androidHome)) { $androidHome = $env:ANDROID_SDK_ROOT }
if (-not (Test-AndroidSdk $androidHome)) { $androidHome = $null }

$javaHome = $env:JAVA_HOME
if (-not (Test-Jdk $javaHome)) { $javaHome = $null }

# 2. Common pre-provisioned locations (Android Studio default install path).
if (-not $androidHome) {
    $default = Join-Path $env:LOCALAPPDATA "Android\Sdk"
    if (Test-AndroidSdk $default) { $androidHome = $default }
}

# 3. Fall back to a Unity Editor's bundled Android SDK / OpenJDK (Android Build
#    Support module). Pick the newest editor version that actually has one installed.
if (-not $androidHome -or -not $javaHome) {
    $hubRoots = @(
        "$env:ProgramFiles\Unity\Hub\Editor",
        "${env:ProgramFiles(x86)}\Unity\Hub\Editor"
    ) | Where-Object { $_ -and (Test-Path $_) }

    $editors = foreach ($base in $hubRoots) {
        Get-ChildItem -Path $base -Directory -ErrorAction SilentlyContinue
    }
    $editors = $editors | Sort-Object Name -Descending

    foreach ($editor in $editors) {
        $pe = Join-Path $editor.FullName "Editor\Data\PlaybackEngines\AndroidPlayer"
        if (-not $androidHome) {
            $sdk = Join-Path $pe "SDK"
            if (Test-AndroidSdk $sdk) { $androidHome = $sdk }
        }
        if (-not $javaHome) {
            $jdk = Join-Path $pe "OpenJDK"
            if (Test-Jdk $jdk) { $javaHome = $jdk }
        }
        if ($androidHome -and $javaHome) { break }
    }
}

if (-not $androidHome) {
    throw "Could not locate an Android SDK (checked ANDROID_HOME, %LOCALAPPDATA%\Android\Sdk, and installed Unity Editors). Install 'Android Build Support' via Unity Hub, or set ANDROID_HOME manually."
}
if (-not $javaHome) {
    throw "Could not locate a JDK (checked JAVA_HOME and installed Unity Editors' bundled OpenJDK). Install a JDK 17, or set JAVA_HOME manually."
}

$env:ANDROID_HOME = $androidHome
$env:ANDROID_SDK_ROOT = $androidHome
$env:JAVA_HOME = $javaHome

Write-Host ">> Using ANDROID_HOME=$androidHome"
Write-Host ">> Using JAVA_HOME=$javaHome"
