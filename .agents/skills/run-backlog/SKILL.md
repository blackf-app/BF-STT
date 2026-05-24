---
name: Run Backlog
description: Pick the first queued task from BACKLOG.md, implement it, run quality gates, mark it done, commit, and push. Use when the user says "run backlog", "execute backlog", or "implement next task". Reads exactly one task file. Stops on any hard-stop condition.
---

# Run Backlog (Workflow C)

Pick the first queued task, implement it, pass all gates, mark done, commit, push.

## Pipeline

```
[1]  PICK
[2]  BRANCH
[3]  MARK_IN_PROGRESS
[4]  LOAD_CONTEXT
[5]  IMPLEMENT
[6]  PREFLIGHT
[7]  REVIEW + SECURITY
[8]  QA_VERIFY
[9]  FINAL_PREFLIGHT
[10] MARK_DONE
[11] COMMIT + PUSH
[12] REPORT
```

---

## Hard-Stop Tokens

Automation must stop immediately (do NOT commit) when any of these appear:

- `TODO is empty` / no task found
- `NO_CHANGES`
- `PREFLIGHT_BLOCKED`
- `REVIEW_BLOCKED`
- `VERIFY_BLOCKED`
- `manual intervention required`
- Git push conflict that cannot be resolved safely
- Task spec is ambiguous after re-reading

When stopped: report reason clearly, include git status and changed files.

---

## Step 1: Pick

Read `BACKLOG.md` only.

- If `IN PROGRESS` has a task → resume it (read its task file from `backlog/in-progress/`).
- Otherwise → pick the first task under `## TODO`.
- If both `TODO` and `IN PROGRESS` are empty → stop with `TODO is empty`.

Read **exactly one** task file. Extract:
- Title + priority
- Description
- Context & constraints
- Related files
- Acceptance criteria
- Manual verification steps
- Scope-control summary

---

## Step 2: Branch

```powershell
git fetch origin
git checkout agent/dev
git pull origin agent/dev
```

If branch does not exist, create it **from the current branch**:
```powershell
# Capture current branch as base
$baseBranch = git rev-parse --abbrev-ref HEAD
git pull origin $baseBranch
git checkout -b agent/dev
```

---

## Step 3: Mark In Progress

Before writing any code:

```powershell
git mv "backlog/todo/<NNN-slug>.md" "backlog/in-progress/<NNN-slug>.md"
```

Update `BACKLOG.md`:
- Remove task from `## TODO`.
- Add task to `## IN PROGRESS`.

Stage this change but do NOT commit yet.

---

## Step 4: Load Context

Before implementing, read:

1. `BACKLOG_AUTOMATION_WORKFLOW.md` (if needed for clarification)
2. `.agents/rules/architecture.md`
3. `.agents/rules/security.md`
4. `.agents/rules/testing.md`
5. `.agents/rules/code-style.md`
6. Related files listed in the task file
7. Any nearby code required to understand the change

Do NOT skip this step.

---

## Step 5: Implement

Implement exactly as specified:
- No extra features.
- No unrelated refactors.
- No new abstractions unless the task requires them.
- No pattern/dependency/schema changes unless the task explicitly allows.

If implementation reveals that broad out-of-scope changes are necessary:
→ Stop. Report. Create a follow-up pending task using the `pending-task` skill.

After implementation:
```powershell
git add -A
```

Do NOT commit yet.

---

## Step 6: Preflight (First Pass)

Run:
```powershell
powershell -ExecutionPolicy Bypass -File .agents/scripts/backlog-preflight.ps1 -Pretty
```

Parse JSON output. If `summary.has_blocking_definite` is `true`:
- Auto-fix up to **2 rounds**.
- Re-run preflight after each fix.
- If still blocked after 2 rounds → stop with `PREFLIGHT_BLOCKED`.

---

## Step 7: Code Review + Security Audit

### Code Review

Read `.agents/agents/code-reviewer.md` for instructions.

Provide to the reviewer:
- Full task spec (from task file)
- Latest preflight JSON
- Full staged diff (`git diff --cached`)

If verdict is `block`:
- Auto-fix up to **2 rounds**.
- Re-run review after each fix.
- If still blocked → stop with `REVIEW_BLOCKED`.

### Security Audit

Run security audit **only** when the staged diff touches:
- API key / auth / token / session
- Settings / persistence / save
- Provider network calls / auth headers
- Clipboard / input injection / hotkey behavior
- Release scripts / publish artifacts
- Dependency changes (NuGet)
- Credential-like strings

Read `.agents/agents/security-auditor.md` for instructions.

Provide: full task spec, latest preflight JSON, full staged diff.

If verdict is `block`:
- Auto-fix up to **2 rounds**.
- If still blocked → stop with `REVIEW_BLOCKED`.

Run code review and security audit **in parallel** when possible.

---

## Step 8: QA Verify

Read `.agents/agents/qa-verifier.md` for instructions.

Provide:
- Full task spec
- Latest preflight JSON
- Full staged diff

QA verifier must check **every acceptance criterion**, not just code style.

If verdict is `fail`:
- Auto-fix up to **2 rounds**.
- If still failed → stop with `VERIFY_BLOCKED`.

---

## Step 9: Final Preflight

Run preflight one final time before marking done.

If definite critical findings remain → fix or stop with `PREFLIGHT_BLOCKED`.

---

## Step 10: Mark Done

```powershell
git mv "backlog/in-progress/<NNN-slug>.md" "backlog/done/<NNN-slug>.md"
```

Replace task body with a short done summary:

```md
### [PRIORITY] Task title

**Completed:** YYYY-MM-DD

**Summary:**
Implemented ...

**Quality gates:**
- Preflight: pass (rounds: 1)
- Code review: pass (rounds: 1)
- Security review: pass | skipped (rounds: 0)
- QA verify: pass (rounds: 1)

**Manual verify steps:**
1. ...
2. ...
```

Update `BACKLOG.md`:
- Remove task from `## IN PROGRESS`.
- Restore `- (none)` if empty.
- Do NOT list done tasks in BACKLOG.md.

---

## Step 11: Commit + Push

Commit only after all gates pass and task is marked done:

```powershell
git add -A
git commit -m "<concise message describing what was implemented>"
git push -u origin agent/dev
```

---

## Step 12: Final Report to User

- Done task path.
- Files changed (list).
- Commit message + short SHA.
- Branch pushed.
- Gate summary (preflight/review/security/QA — rounds used).
- Manual verification steps (copy exactly from done summary).
- Merge instructions: "Merge `agent/dev` into the base branch after manual verification."
