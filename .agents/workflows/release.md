---
description: Build application, create a git tag, and publish a release to GitHub with the .exe artifact.
---

# Release Application

> For a full release across Windows, macOS, and Android in one GitHub Release, use the
> `release-all` skill (`.agents/skills/release-all/SKILL.md`) instead. This workflow only
> builds a Windows `.exe` and creates a GitHub Release with that single artifact.

To release the application, follow these steps.

// turbo-all
1. Run the release script
```powershell
powershell -ExecutionPolicy Bypass -File "scripts/release.ps1"
```

