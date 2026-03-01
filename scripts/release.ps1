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
    
    # Update all version strings in one pass
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

# 4. Fast Build (Using existing settings from .csproj)
Write-Host "--> Building/Publishing $tagName..." -ForegroundColor Cyan
# Added /p:PauseAfterBuild=true to skip app restart during release build
# and -v:q for quiet output
dotnet publish -c Release -o $publishDir /p:IsAutoPublishing=true /p:PauseAfterBuild=true -nologo -clp:NoSummary -v:q

# 5. Fast Release (Tag, Push, Release)
Write-Host "--> Pushing Git Tag..." -ForegroundColor Cyan
git tag $tagName
git push origin $tagName --quiet

Write-Host "--> Creating GitHub Release..." -ForegroundColor Cyan
# Generate notes using GitHub's automatic changelog engine
gh release create $tagName "$publishDir/BF-STT.exe" --title "Release $tagName" --generate-notes

Write-Host "`n==> RELEASE SUCCESSFUL: $tagName" -ForegroundColor Green
