---
name: Build
description: Build the application to verify there are no compilation errors. Use when the user says "build", "compile", or wants to verify code changes.
---

# Build Skill

When the user asks to build the application, or as a step to verify your code changes, follow this process:

1. **Run the Build Command:**
   Execute the standard `dotnet build` command for the project.
   ```powershell
   dotnet build
   ```

2. **Analyze Output:**
   - If the build succeeds (0 errors), report the success to the user.
   - If the build fails, carefully read the error messages. Identify which files and lines caused the errors.

3. **Resolve or Report:**
   - If you can confidently fix the errors (e.g., syntax errors, missing using directives, typos), apply the fixes and build again.
   - If the errors are complex or ambiguous, present the errors to the user and suggest a plan to fix them.
