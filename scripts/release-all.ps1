<#
.SYNOPSIS
  Full multi-platform release for BF-STT on Windows: bumps the version, builds Windows
  and Android, then tags and publishes a single GitHub Release with both artifacts
  attached. macOS is intentionally skipped here — it can't be built from Windows; run
  scripts/release-all.sh on a Mac for a release that also includes the .app bundle.

.DESCRIPTION
  Requires: dotnet SDK, gh (authenticated), git.

  Android SDK/JDK are auto-detected (existing provisioned SDK, or falls back to an
  installed Unity Editor's bundled Android SDK) via .agents/scripts/find-android-sdk.ps1.

  On failure before the git tag/push/release step, the version bump is left
  uncommitted in the working tree. Run:
    git checkout -- BF-STT.csproj BF-STT.Android/BF-STT.Android.csproj
  to reset before retrying.
#>
param (
    [string]$CsprojPath = "BF-STT.csproj",
    [string]$AndroidCsprojPath = "BF-STT.Android/BF-STT.Android.csproj",
    [string]$PublishDir = "./publish/win"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

if (!(Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Host "GitHub CLI not found. Please install it first." -ForegroundColor Red; exit 1
}
$auth = gh auth status 2>&1
if ($auth -join "`n" -match "not logged in") {
    Write-Host "You are not logged into GitHub. Run 'gh auth login' manually." -ForegroundColor Red; exit 1
}

$dirty = git status --porcelain
if ($dirty) {
    Write-Host "Working tree is not clean. Commit or stash pending changes before releasing." -ForegroundColor Red
    git status --short
    exit 1
}

# 1. Bump the main app version.
Write-Host "--> Bumping version..." -ForegroundColor Cyan
$content = [System.IO.File]::ReadAllText($CsprojPath)
if ($content -notmatch "<Version>(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)</Version>") {
    Write-Error "Could not find <Version> in $CsprojPath"; exit 1
}
$major = [int]$Matches['major']; $minor = [int]$Matches['minor']; $patch = [int]$Matches['patch']
$patch++
if ($patch -gt 999) { $patch = 0; $minor++ }
$newVersion = "$major.$minor.$patch"
$content = $content -replace "<Version>\d+\.\d+\.\d+</Version>", "<Version>$newVersion</Version>"
$content = $content -replace "<AssemblyVersion>\d+\.\d+\.\d+</AssemblyVersion>", "<AssemblyVersion>$newVersion</AssemblyVersion>"
$content = $content -replace "<FileVersion>\d+\.\d+\.\d+</FileVersion>", "<FileVersion>$newVersion</FileVersion>"
$content = $content -replace "<InformationalVersion>\d+\.\d+\.\d+</InformationalVersion>", "<InformationalVersion>$newVersion</InformationalVersion>"
[System.IO.File]::WriteAllText($CsprojPath, $content)
Write-Host "--> Version updated to $newVersion" -ForegroundColor Green

Write-Host "--> Syncing Android app version to $newVersion" -ForegroundColor Cyan
& "$PSScriptRoot\..\.agents\scripts\bump-android-version.ps1" -CsprojPath $AndroidCsprojPath -TargetDisplayVersion $newVersion

$tagName = "v$newVersion"
git rev-parse $tagName 2>$null | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Host "Tag $tagName already exists. Aborting before touching the working tree further." -ForegroundColor Red
    git checkout -- $CsprojPath $AndroidCsprojPath
    exit 1
}

# 2. Build Windows + Android.
$AssetsDir = "./publish/release-assets"
Remove-Item -Recurse -Force $AssetsDir -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $AssetsDir | Out-Null

Write-Host "--> Building Windows..." -ForegroundColor Cyan
dotnet publish $CsprojPath -c Release -f net8.0-windows -o $PublishDir /p:IsAutoPublishing=true /p:PauseAfterBuild=true -nologo -clp:NoSummary -v:q
if ($LASTEXITCODE -ne 0) { Write-Error "Windows publish failed (exit code $LASTEXITCODE). Aborting release."; exit 1 }
Copy-Item "$PublishDir/BF-STT.exe" "$AssetsDir/BF-STT-Windows-v$newVersion.exe"

Write-Host "--> Skipping macOS build (not supported from Windows). Run scripts/release-all.sh on a Mac to include it." -ForegroundColor Yellow

Write-Host "--> Building Android..." -ForegroundColor Cyan
& "$RepoRoot\BF-STT.Android\build-apk.ps1" -NoBump
if ($LASTEXITCODE -ne 0) { Write-Error "Android build failed. Aborting release before tagging/publishing."; exit 1 }
$ApkSrc = "$RepoRoot/dist/BF-STT-Android-v$newVersion.apk"
if (-not (Test-Path $ApkSrc)) { Write-Error "Expected Android artifact not found at $ApkSrc"; exit 1 }
Copy-Item $ApkSrc $AssetsDir

# 3. Commit the version bump.
git add $CsprojPath $AndroidCsprojPath
git commit -q -m "Bump version to $newVersion for release"

# 4. Release notes from commit log since the last tag.
Write-Host "--> Generating Friendly Release Notes..." -ForegroundColor Cyan
$lastTag = git describe --tags --abbrev=0 HEAD~1 2>$null
$logs = if ($lastTag) { git log "$lastTag..HEAD" --pretty=format:"%s" } else { git log -n 10 --pretty=format:"%s" }

$features = @(); $bugfixes = @(); $improvements = @(); $others = @()
foreach ($line in ($logs -split "`n")) {
    $clean = $line.Trim()
    if ([string]::IsNullOrWhiteSpace($clean) -or $clean -match "^Merge ") { continue }
    if ($clean -match "^(?i)(Add|Implement|Integrate|New|Feat)") { $features += "- $clean" }
    elseif ($clean -match "^(?i)(Fix|Resolve|Bug|Hotfix)") { $bugfixes += "- $clean" }
    elseif ($clean -match "^(?i)(Improve|Refactor|Update|Clean|Perf|Optimize)") { $improvements += "- $clean" }
    else { $others += "- $clean" }
}
$notes = "## Release $tagName`n`n"
if ($features.Count -gt 0) { $notes += "### New Features`n" + ($features -join "`n") + "`n`n" }
if ($bugfixes.Count -gt 0) { $notes += "### Bug Fixes`n" + ($bugfixes -join "`n") + "`n`n" }
if ($improvements.Count -gt 0) { $notes += "### Improvements`n" + ($improvements -join "`n") + "`n`n" }
if ($others.Count -gt 0) { $notes += "### Other Changes`n" + ($others -join "`n") + "`n`n" }
$tempFile = [System.IO.Path]::GetTempFileName()
[System.IO.File]::WriteAllText($tempFile, $notes)

# 5. Tag, push, and publish the GitHub Release with both built assets.
$currentBranch = git rev-parse --abbrev-ref HEAD
Write-Host "--> Pushing $currentBranch and tag $tagName..." -ForegroundColor Cyan
git tag $tagName
git push origin $currentBranch
git push origin $tagName --quiet

Write-Host "--> Creating GitHub Release $tagName..." -ForegroundColor Cyan
$assets = Get-ChildItem $AssetsDir | Select-Object -ExpandProperty FullName
gh release create $tagName $assets --title "Release $tagName" --notes-file $tempFile
Remove-Item $tempFile -ErrorAction SilentlyContinue

Write-Host "`n==> RELEASE SUCCESSFUL: $tagName" -ForegroundColor Green
Write-Host "----------------------------------------------------"
Write-Host $notes
Write-Host "----------------------------------------------------"
