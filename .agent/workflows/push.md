---
description: Stage all changes, generate an AI commit message, and push to Git
---

Follow these steps to push your changes with an AI-generated commit message:

1. Stage all changes (including untracked files).
```powershell
git add -A
```

2. Generate a concise and descriptive commit message based on the staged changes (DO NOT use prefixes like "feat:", "fix:", etc.). I will review the `git diff --cached` output to understand the context of your work.

3. Commit the changes with the generated message.
```powershell
git commit -m "[INSERT_AI_MESSAGE_HERE]"
```

4. Push the changes to the remote repository.
```powershell
git push
```
