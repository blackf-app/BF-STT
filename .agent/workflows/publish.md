---
description: Build and publish the application to the ./publish directory
---

To publish the application, follow these steps:

// turbo
1. Stop the application if it's running
```powershell
Get-Process BF-STT -ErrorAction SilentlyContinue | Stop-Process -Force
```

// turbo
2. Execute the publish command
```powershell
dotnet publish -c Release -o ./publish
```

// turbo
3. Start the application again
```powershell
Start-Process "./publish/BF-STT.exe"
```

4. The published files are available in the `publish` directory, and the application has been restarted.
