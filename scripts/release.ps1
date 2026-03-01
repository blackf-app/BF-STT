param (
    [string]$csprojPath = "BF-STT.csproj",
    [string]$publishDir = "./publish"
)

$ErrorActionPreference = "Stop"

# 1. Quick Environment Checks
if (!(Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Host "GitHub CLI not found. Please install it first." -ForegroundColor Red; exit 1
}

# 2. Check Auth Status
$auth = gh auth status 2>&1
if ($auth -join "`n" -match "not logged in") {
    Write-Host "You are not logged into GitHub. Run 'gh auth login' manually." -ForegroundColor Red; exit 1
}

# 3. Fast Version Increment
Write-Host "--> Incrementing version..." -ForegroundColor Cyan
$content = [System.IO.File]::ReadAllText($csprojPath)
if ($content -match "<Version>(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)</Version>") {
    $major = [int]$Matches['major']
    $minor = [int]$Matches['minor']
    $patch = [int]$Matches['patch']
    $patch++
    if ($patch -gt 999) { $patch = 0; $minor++ }
    $newVersion = "$major.$minor.$patch"
    
    $content = $content -replace "<Version>\d+\.\d+\.\d+</Version>", "<Version>$newVersion</Version>"
    $content = $content -replace "<AssemblyVersion>\d+\.\d+\.\d+</AssemblyVersion>", "<AssemblyVersion>$newVersion</AssemblyVersion>"
    $content = $content -replace "<FileVersion>\d+\.\d+\.\d+</FileVersion>", "<FileVersion>$newVersion</FileVersion>"
    $content = $content -replace "<InformationalVersion>\d+\.\d+\.\d+</InformationalVersion>", "<InformationalVersion>$newVersion</InformationalVersion>"
    [System.IO.File]::WriteAllText($csprojPath, $content)
    Write-Host "--> Version updated to $newVersion" -ForegroundColor Green
}
else {
    Write-Error "Could not find <Version> in $csprojPath"
}

$tagName = "v$newVersion"

# 4. Fast Build
Write-Host "--> Building/Publishing $tagName..." -ForegroundColor Cyan
dotnet publish -c Release -o $publishDir /p:IsAutoPublishing=true /p:PauseAfterBuild=true -nologo -clp:NoSummary -v:q

# 5. Smart Release Notes Generation
Write-Host "--> Generating Friendly Release Notes..." -ForegroundColor Cyan

$lastTag = git describe --tags --abbrev=0 2>$null
$logs = if ($lastTag) { git log "$lastTag..HEAD" --pretty=format:"%s" } else { git log -n 10 --pretty=format:"%s" }

$features = @()
$bugfixes = @()
$improvements = @()
$others = @()

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

$notes += "*Built with Love by BF-STT Auto-Release Tool*"

# Save notes temp for gh cli
$tempFile = [System.IO.Path]::GetTempFileName()
[System.IO.File]::WriteAllText($tempFile, $notes)

# 6. Fast Release (Tag, Push, Release)
Write-Host "--> Pushing Git Tag..." -ForegroundColor Cyan
git tag $tagName
git push origin $tagName --quiet

Write-Host "--> Creating GitHub Release..." -ForegroundColor Cyan
gh release create $tagName "$publishDir/BF-STT.exe" --title "Release $tagName" --notes-file $tempFile

if (Test-Path $tempFile) { Remove-Item $tempFile }

Write-Host "`n==> RELEASE SUCCESSFUL: $tagName" -ForegroundColor Green
Write-Host "----------------------------------------------------"
Write-Host $notes
Write-Host "----------------------------------------------------"
