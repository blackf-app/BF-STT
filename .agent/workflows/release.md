---
description: Build application, create a git tag, and publish a release to GitHub with the .exe artifact.
---

To release the application, follow these steps. If any step fails, stop immediately.

// turbo
1. Check or install GitHub CLI
```powershell
if (!(Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Host "GitHub CLI not found. Installing..." -ForegroundColor Yellow
    winget install --id GitHub.cli --silent --accept-source-agreements --accept-package-agreements
    $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
} else {
    Write-Host "GitHub CLI is already installed." -ForegroundColor Green
}
```

2. Check GitHub Authentication Status
```powershell
gh auth status
# If not logged in, you MUST run 'gh auth login' manually in your terminal first.
```

// turbo
3. Increment version
```powershell
powershell -ExecutionPolicy Bypass -File "scripts/increment_version.ps1" -csprojPath "BF-STT.csproj"
```

// turbo
4. Build the application (Published to ./publish)
```powershell
dotnet publish -c Release -o ./publish /p:IsAutoPublishing=true
```

// turbo
5. Get the new Version and Create Release
```powershell
# Extract version from .csproj
[xml]$doc = Get-Content "BF-STT.csproj"
$version = $doc.Project.PropertyGroup.Version
$tagName = "v$version"
$exePath = "publish/BF-STT.exe"

Write-Host "Releasing version: $version" -ForegroundColor Cyan

# Prepare Release Notes from Git History
$lastTag = git describe --tags --abbrev=0 2>$null
if ($lastTag) {
    $releaseNotes = (git log "$lastTag..HEAD" --pretty=format:"- %s") -join "`n"
} else {
    $releaseNotes = (git log -n 5 --pretty=format:"- %s") -join "`n"
}

if ([string]::IsNullOrWhiteSpace($releaseNotes)) {
    $releaseNotes = "Manual release for version $version"
}

# Create and push git tag
git tag $tagName
git push origin $tagName

# Create GitHub Release and upload the .exe
gh release create $tagName $exePath --title "Release $tagName" --notes "$releaseNotes"
```
