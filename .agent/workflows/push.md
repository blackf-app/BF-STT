---
description: Stage all changes, generate an AI commit message, and push to Git
---

// turbo-all
# Git Push Workflow
Automate the staging, committing, and pushing of changes with an AI-generated message.

## 1. PREPARE & ANALYZE
- Run `powershell -ExecutionPolicy Bypass -File .agent/scripts/git_prepare.ps1`
- If output is `NO_CHANGES`, stop and inform the user.
- Analyze the output to generate a concise, descriptive commit message (max 50 chars).
- **DO NOT** use prefixes like `feat:`, `fix:`, `refactor:`, or `docs:`.

## 2. FINALIZE
- Run `powershell -ExecutionPolicy Bypass -File .agent/scripts/git_push.ps1 "[Generated Message]"`
- Report the status and the generated message.