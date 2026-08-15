<#
.SYNOPSIS
  Build the BF-STT Android APK on Windows (signed, self-contained, installable).
  PowerShell port of build-apk.sh for machines without a bash shell.

.DESCRIPTION
  Prereqs:
    - .NET 8 SDK (+ android workload)
    - A JDK 17 and an Android SDK. ANDROID_HOME/JAVA_HOME are used if already set by
      the caller (e.g. scripts/release-all.ps1); otherwise this script auto-detects
      them via .agents/scripts/find-android-sdk.ps1, which falls back to the Android
      SDK bundled with an installed Unity Editor (Android Build Support module) if no
      standalone Android Studio SDK is provisioned.

  EmbedAssembliesIntoApk=true (set in the .csproj) makes the APK SELF-CONTAINED, so a
  directly-installed/sideloaded APK does not crash with "No assemblies found ... Exiting".

  Release is signed with a local self-signed dev keystore, created on first run. For a
  Play Store release, swap in your own keystore.

.PARAMETER Config
  Release (default, signed & self-contained) or Debug.

.PARAMETER NoBump
  Skip the automatic version bump (e.g. when retrying a failed build).
#>
param (
    [string]$Config = "Release",
    [switch]$NoBump
)

$ErrorActionPreference = "Stop"

$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
$Proj = Join-Path $Here "BF-STT.Android.csproj"
$Ks = Join-Path $Here "bfstt-release.keystore"

if (-not $env:ANDROID_HOME -or -not $env:JAVA_HOME) {
    . (Join-Path $Here "..\.agents\scripts\find-android-sdk.ps1")
}
$env:ANDROID_SDK_ROOT = $env:ANDROID_HOME

if (-not $NoBump -and $env:BF_NO_BUMP -ne "1") {
    & "$Here\..\.agents\scripts\bump-android-version.ps1" -CsprojPath $Proj
    if ($LASTEXITCODE -ne 0) { throw "Version bump failed" }
}

if (-not (Test-Path $Ks)) {
    Write-Host ">> Creating self-signed release keystore"
    $keytool = Join-Path $env:JAVA_HOME "bin\keytool.exe"
    & $keytool -genkeypair -v `
        -keystore $Ks -alias bfstt -keyalg RSA -keysize 2048 -validity 10000 `
        -storepass bfstt123 -keypass bfstt123 `
        -dname "CN=BF-STT, O=BF Company, L=Hanoi, C=VN"
    if ($LASTEXITCODE -ne 0) { throw "Keystore creation failed" }
}

Write-Host ">> Building $Proj ($Config)"
if ($Config -eq "Release") {
    dotnet publish $Proj -c Release -f net8.0-android `
        -p:AndroidSdkDirectory="$env:ANDROID_HOME" `
        -p:JavaSdkDirectory="$env:JAVA_HOME" `
        -p:AcceptAndroidSdkLicenses=true `
        -p:AndroidKeyStore=true `
        -p:AndroidSigningKeyStore="$Ks" `
        -p:AndroidSigningStorePass=bfstt123 `
        -p:AndroidSigningKeyAlias=bfstt `
        -p:AndroidSigningKeyPass=bfstt123
} else {
    dotnet build $Proj -c Debug `
        -p:AndroidSdkDirectory="$env:ANDROID_HOME" `
        -p:JavaSdkDirectory="$env:JAVA_HOME" `
        -p:AcceptAndroidSdkLicenses=true
}
if ($LASTEXITCODE -ne 0) { throw "dotnet publish/build failed (exit code $LASTEXITCODE)" }

$OutDir = Join-Path $Here "bin\$Config\net8.0-android"
$SignedApk = Get-ChildItem -Path $OutDir -Filter "*-Signed.apk" -ErrorAction SilentlyContinue | Select-Object -First 1
Write-Host ">> APK(s):"
if ($SignedApk) { Write-Host $SignedApk.FullName }

# Copy the signed Release APK into the repo-level dist/ folder (versioned name).
# Debug builds are for testing only and are not published to dist/.
if ($Config -eq "Release" -and $SignedApk) {
    $Dist = Join-Path $Here "..\dist"
    New-Item -ItemType Directory -Force -Path $Dist | Out-Null
    $VerLine = Select-String -Path $Proj -Pattern "<ApplicationDisplayVersion>([^<]*)</ApplicationDisplayVersion>" | Select-Object -First 1
    $Ver = if ($VerLine) { $VerLine.Matches[0].Groups[1].Value } else { "unknown" }
    $Dest = Join-Path $Dist "BF-STT-Android-v$Ver.apk"
    Copy-Item -Force $SignedApk.FullName $Dest
    Write-Host ">> Copied to dist: $Dest"
}
