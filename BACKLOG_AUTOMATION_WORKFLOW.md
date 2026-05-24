# Backlog Automation Workflow

This document describes how to build and operate an AI-driven backlog automation system. It is meant to be portable: copy it into another project and an AI agent should be able to use it as the blueprint for implementing a similar workflow.

The system separates work into three stages:

1. **Capture**: turn a user request into a fully specified task file in `backlog/pending/`.
2. **Queue**: let the user intentionally promote pending tasks into `backlog/todo/` and `BACKLOG.md`.
3. **Execute**: let an autonomous agent pick one queued task, implement it, review it, verify it, mark it done, commit, and push.

The most important principle: a backlog task is an **implementation contract**, not a rough note. If the contract is wrong, automation can pass review and QA while still implementing the wrong thing.

---

## 1. File Layout

Use a split-file layout so token usage remains stable as the backlog grows:

```text
BACKLOG.md
backlog/
  pending/
    .gitkeep
    <timestamp>-<TIER>-<slug>.md
  todo/
    .gitkeep
    NNN-<slug>.md
  in-progress/
    .gitkeep
    NNN-<slug>.md
  done/
    .gitkeep
    NNN-<slug>.md
  _TEMPLATE_XS.md
  _TEMPLATE_S.md
  _TEMPLATE_M.md
  _TEMPLATE_L.md
.agents/
  skills/
    pending-task/
      SKILL.md
    add-to-backlog/
      SKILL.md
    run-backlog/
      SKILL.md
    execute-backlog-tasks/
      SKILL.md
  agents/
    code-reviewer.md
    security-auditor.md
    qa-verifier.md
  rules/
    code-style.md
    architecture.md
    data-persistence.md
    third-party.md
  scripts/
    backlog-preflight.ps1
    run-backlog-loop.bat
    run-backlog-loop-core.ps1
    run-backlog-loop-<provider>.ps1
```

In another project, rename `.agents/` if needed, but preserve the state model:

- `BACKLOG.md` is only a short index of queued tasks.
- `backlog/pending/` contains drafted tasks that are not queued yet.
- `backlog/todo/` contains tasks automation is allowed to implement.
- `backlog/in-progress/` contains the task currently being implemented.
- `backlog/done/` contains completed task summaries.

The execution agent should read `BACKLOG.md` plus **exactly one** selected task file. It must not scan all task files.

---

## 2. `BACKLOG.md` Format

`BACKLOG.md` should be a small index:

```md
# Backlog

Tasks live as individual files in `backlog/{todo,in-progress,done}/`.
This file is only the queued index.

## TODO

- [HIGH] [M] [Example task title](backlog/todo/001-example-task.md)
- [MEDIUM] [S] [Another task](backlog/todo/002-another-task.md)

## IN PROGRESS

- (none)

## DONE

See `backlog/done/`.
```

Ordering rules:

- `HIGH` tasks go first.
- `MEDIUM` tasks go next.
- `LOW` tasks go last.
- Within the same priority, preserve insertion order.
- Execution order is the order in `BACKLOG.md`, not filename order.

---

## 3. Task Tiers

Every pending task must be triaged into one of four tiers:

| Tier | Use When | Pipeline |
|---|---|---|
| `XS` | CSV tweak, constant adjustment, dead-code removal, one-file rename, no new logic | Direct draft |
| `S` | Single-file logic tweak or small bug fix, up to 2 files, no new UI/save/event | Light exploration |
| `M` | Multi-file feature, new UI/popup/controller/save field/event, 3-8 files | Planning pass |
| `L` | Cross-cutting work: IAP, backend surface, save migration, system integration, 9+ files | Planning pass + phases/risk |

Auto-bump to a higher tier when:

- The task touches purchase/IAP/receipt/payment code: at least `M`.
- The task adds a save field or save module: at least `M`.
- The task adds a cross-system event: at least `M`.
- The task adds a backend endpoint or database table: at least `M`.
- The task touches auth/token/session/security: at least `M`.
- The task touches more than 2 feature modules or more than 8 files: `L`.

---

## 4. Workflow A: Create a Pending Task

Trigger examples: "create pending task", "draft task", "create task X".

Goal: create **one new file** in `backlog/pending/`. Do not edit `BACKLOG.md`. Do not create anything in `backlog/todo/`. Do not commit.

Pipeline:

```text
[0] TRIAGE
[1] EXTRACT + CLARIFY
[2] DRAFT
[3] FILENAME
[4] WRITE
[5] CHECK
[6] REPORT
```

### 4.1 Triage

Choose the tier using concrete signals, not intuition. If scope is genuinely unclear, choose the smaller tier only when it is safe to do so. If the request touches backend/security/save/IAP surfaces, apply the auto-bump rules.

### 4.2 Extract + Clarify

Parse the user request into:

- What
- Why
- Scope
- Priority, defaulting to `MEDIUM`
- Constraints
- Acceptance criteria, if provided
- Do-not-touch / out-of-scope items, if provided
- Manual verification expectations, if provided

If intent is ambiguous in any way that affects behavior, scope, acceptance criteria, verification steps, product/economy/save/backend/IAP/security/UX flow, the agent must clarify before writing the task file.

Clarification rules:

- Ask in small batches, maximum 3 questions per turn.
- Ask as many rounds as needed while important ambiguity remains.
- Do not guess product behavior, reward/economy values, save migration, backend/auth/security behavior, IAP/purchase behavior, or acceptance criteria.
- Only assume low-risk implementation details that do not change the outcome, and document those assumptions in the task.
- Do not ask questions that can be answered by reading or searching the codebase.

### 4.3 Scope-Control Gate

This gate prevents a small task from becoming uncontrolled broad editing.

If a small task (`XS/S`) requires touching modules outside the user's stated scope, the agent must ask the user or split the task. It must not expand the task on its own for refactoring, cleanup, or pattern rewrites.

The agent must not silently:

- Add new abstractions.
- Change established patterns.
- Add or replace dependencies.
- Change schema or save formats.
- Modify related flows the user did not ask for.
- Touch multiple modules without explanation.
- Make the current task pass by breaking future tasks or existing behavior.

If broad changes are necessary, the task must explicitly document:

- Why broad changes are necessary.
- Affected areas.
- Migration plan, if data/schema/config/save is touched.
- Test/regression plan.
- Checkpoints.
- Rollback or fallback path.
- Out-of-scope items.

If the agent cannot explain those points, it must not create a broad pending task. It should ask the user to narrow the scope or split the work into multiple tasks.

### 4.4 Draft by Tier

`XS`:

- Draft directly from the user request.
- Do not use a planning subagent.
- Do not search the codebase unless needed to avoid a wrong path/name.

`S`:

- Search/read 1-3 files to confirm paths and local patterns.
- Do not use a planning subagent.

`M/L`:

- Use a planning pass or planning subagent.
- The planning pass may read code, rules, and relevant docs.
- The planning pass must **not implement** and must **not modify files**.
- Its output should be structured JSON:

```json
{
  "summary": "one-sentence restatement",
  "files_to_touch": [{ "path": "path/to/file", "why": "reason" }],
  "pattern_to_follow": "existing pattern",
  "scope_control": {
    "is_broad_change": false,
    "why_broad_change_is_needed": "none | required because ...",
    "affected_areas": ["module/system"],
    "migration_plan": "none | migration steps",
    "test_regression_plan": ["specific regression/test checkpoint"],
    "checkpoints": ["observable checkpoint"],
    "rollback_or_fallback": "none | rollback/fallback path",
    "out_of_scope": ["things implementer must not touch"]
  },
  "completion_criteria": ["observable criterion"],
  "verify_steps": ["happy path", "edge case", "regression check"],
  "risks": ["risk + mitigation"],
  "applicable_guardrails": ["pattern", "security", "save"],
  "not_applicable": {
    "security": "no sensitive surface touched"
  },
  "open_questions": []
}
```

If `open_questions` affects the implementation contract, do not write the pending task yet. Ask the user for clarification.

### 4.5 Filename

Pending filename:

```text
backlog/pending/<timestamp>-<TIER>-<slug>.md
```

Where:

- `timestamp`: UTC, millisecond precision, `YYYYMMDDTHHmmssSSS`.
- `TIER`: `XS`, `S`, `M`, or `L`.
- `slug`: 2-5 kebab-case words.

Example:

```text
backlog/pending/20260524T141233456-M-login-api.md
```

Do not assign `NNN` in pending. `NNN` is assigned only when the task is queued into `todo`.

### 4.6 Pending Task Template Requirements

Every task file should include at least:

- Title + priority.
- Description.
- Context & constraints.
- Related files.
- Acceptance criteria.
- Scope-control summary.
- Applicable guardrails.
- Manual verification steps.
- Assumptions, if any.
- Out-of-scope items, if any.

`M/L` tasks should also include:

- Risks.
- Phases/checkpoints when `L`.
- Migration plan when touching data/schema/config/save.
- Rollback/fallback when changes are broad.

### 4.7 Pending Quality Check

Before reporting:

- The title is specific, not vague.
- No ambiguity remains that affects behavior/completion/verification.
- File paths are real for `S/M/L`.
- Scope has not expanded without explanation.
- Acceptance criteria are verifiable.
- At least one regression check exists.
- Broad changes include why/impact/migration/tests/checkpoints/rollback.
- Excluded guardrails have clear reasons.

Report to the user:

- Selected tier + reason.
- Created file path in `backlog/pending/`.
- Scope-control summary.
- Assumptions.
- Top acceptance criteria.
- Reminder to use add-to-backlog when ready to queue the task.

---

## 5. Workflow B: Add Pending Task to Backlog

Trigger examples: "add task to backlog", "pick task", "promote task".

Goal: move tasks from `backlog/pending/` to `backlog/todo/`, assign `NNN`, and update `BACKLOG.md`.

Pipeline:

```text
[0] CONFIRM_INTENT
[1] LIST
[2] DISPLAY
[3] PICK
[4] OVERRIDE PRIORITY
[5] ASSIGN_NNN
[6] MOVE
[7] UPDATE_BACKLOG
[8] REPORT
```

### 5.1 List Pending

Glob:

```text
backlog/pending/*.md
```

Ignore `.gitkeep`.

Parse filename:

```text
<timestamp>-<TIER>-<slug>.md
```

Parse priority/title from the first heading:

```md
### [HIGH] Task title
```

Sort newest first.

### 5.2 Display + Pick

Show the list:

```text
[1] [M]  [HIGH]   Login API                 - 2026-05-24 14:23
[2] [S]  [MEDIUM] Fix notification badge    - 2026-05-24 14:25
```

Accept:

- `1`
- `1,3`
- `1 3 5`
- `1-3`
- `1-2,4`
- `all`

### 5.3 Priority Override

Allow the user to override priority during pick:

```text
2:HIGH, 4:LOW
```

Tier does not change at this step. If the tier is wrong, the user should edit the pending file or create a replacement task.

### 5.4 Assign NNN

Scan filenames in:

```text
backlog/todo/
backlog/in-progress/
backlog/done/
```

Find the max `NNN` prefix and assign the next numbers:

```text
021-login-api.md
022-fix-badge.md
```

### 5.5 Move + Update Index

Use `git mv`:

```bash
git mv backlog/pending/<timestamp>-<TIER>-<slug>.md backlog/todo/<NNN>-<slug>.md
```

Update `BACKLOG.md` once and insert bullets into the correct priority bucket:

```md
- [HIGH] [M] [Login API](backlog/todo/021-login-api.md)
```

Do not commit at this step. The user may want to review the queue before running automation.

---

## 6. Workflow C: Run a Backlog Task

Trigger examples: "run backlog", "execute backlog tasks", or an automation loop calling the run-backlog workflow.

Goal: pick the first queued task, implement it, pass gates, mark it done, commit, and push.

Pipeline:

```text
[1] PICK
[2] BRANCH
[3] MARK IN PROGRESS
[4] CONTEXT
[5] IMPLEMENT
[6] REVIEW + SECURITY
[7] QA VERIFY
[8] MARK DONE
[9] COMMIT + PUSH
[10] REPORT
```

### 6.1 Pick

Read `BACKLOG.md` only.

- If `IN PROGRESS` has a task, resume it.
- Otherwise, pick the first task in `TODO`.
- If neither `TODO` nor `IN PROGRESS` has a task, stop.

Read exactly one task file. Extract:

- Title/priority.
- Description.
- Context & constraints.
- Related files.
- Acceptance criteria.
- Manual verification steps.
- Scope-control summary.

### 6.2 Branch

Use a dedicated automation branch, for example:

```bash
git fetch origin
git checkout agent/dev
git pull origin agent/dev
```

If the branch does not exist yet:

```bash
git checkout <base-release-branch>
git pull origin <base-release-branch>
git checkout -b agent/dev
```

Each project should define its own branch policy.

### 6.3 Mark In Progress

Before writing code:

```bash
git mv backlog/todo/<NNN-slug>.md backlog/in-progress/<NNN-slug>.md
```

Update `BACKLOG.md`:

- Remove the task from `TODO`.
- Add it to `IN PROGRESS`.

### 6.4 Load Context

Before implementing, the agent must read:

- Project guide, such as `CLAUDE.md` or `AGENTS.md`.
- `.agents/rules/*`.
- Relevant skill/system docs.
- Related files listed in the task.
- Any nearby code required to understand the change.

Do not skip this step.

### 6.5 Implement

Implement the task exactly as specified:

- No extra features.
- No unrelated refactors.
- No new abstractions unless the task requires them.
- No pattern/dependency/schema/save-flow changes unless the task explicitly allows them.
- If implementation reveals that broad out-of-scope changes are necessary, stop and report, or create a follow-up/pending task according to project policy.

After implementation:

```bash
git add -A
```

Do not commit yet.

---

## 7. Quality Gates

Quality gates run on the staged diff.

### 7.1 Deterministic Preflight

Run a deterministic script before LLM review, for example:

```powershell
powershell -ExecutionPolicy Bypass -File .agents/scripts/backlog-preflight.ps1 -Pretty
```

Preflight should catch rules that are cheap and deterministic to scan:

- Hardcoded secrets/API keys/tokens.
- Direct database writes when policy requires backend workers.
- Forbidden time APIs.
- Forbidden persistence APIs.
- Async/coroutine violations.
- UI show/hide violations.
- Noisy logging.
- Expensive lookups in hot paths.
- Sensitive file patterns.

Output should be JSON:

```json
{
  "summary": {
    "has_blocking_definite": false,
    "definite_critical_count": 0
  },
  "sensitive": {
    "value": false,
    "reasons": []
  },
  "findings": []
}
```

If there are definite critical findings:

- Auto-fix up to 2 rounds when the count is small.
- If still present, stop with `PREFLIGHT_BLOCKED`.

### 7.2 Code Review

Always run a code reviewer.

Reviewer prompt should include:

- Full task spec.
- Latest preflight JSON.
- Full staged diff.

Reviewer returns:

```json
{
  "verdict": "pass | warn | block",
  "findings": [
    {
      "severity": "critical | major | minor",
      "file": "path",
      "line": 123,
      "issue": "description",
      "suggestion": "fix"
    }
  ]
}
```

If `block`, auto-fix up to 2 rounds. If still blocked, stop with `REVIEW_BLOCKED`.

### 7.3 Security Audit

Run a security auditor when the staged diff touches sensitive surfaces:

- Backend/auth/token/session.
- Payment/IAP/receipt.
- Save/persistence.
- Leaderboard/social.
- Anti-cheat/validation/integrity.
- Env/config/secrets.
- Credential-like strings.

Run security audit in parallel with code review when possible.

If security returns `block`, auto-fix up to 2 rounds. If still blocked, stop with `REVIEW_BLOCKED`.

### 7.4 QA Verify

Run QA verification after review.

QA verifier should not only check style. It must verify every acceptance criterion.

Prompt should include:

- Full task spec.
- Latest preflight JSON.
- Full staged diff.

QA output:

```json
{
  "verdict": "pass | warn | fail",
  "criteria_check": [],
  "manual_verify_steps": []
}
```

If `fail`, auto-fix up to 2 rounds. If still failed, stop with `VERIFY_BLOCKED`.

Manual verification steps from QA must be copied exactly into the done summary and final report.

### 7.5 Final Preflight

Run preflight one last time before marking the task done. If definite critical findings remain, fix them or stop with `PREFLIGHT_BLOCKED`.

---

## 8. Mark Done

After gates pass:

```bash
git mv backlog/in-progress/<NNN-slug>.md backlog/done/<NNN-slug>.md
```

Replace the task body with a short summary:

```md
### [HIGH] Login API

**Completed:** YYYY-MM-DD (commit `<short-sha>` after commit if needed)

**Summary:**
Implemented ...

**Quality gates:**
- Code review: pass (rounds used: 1)
- Security review: pass | skipped (rounds used: 1)
- QA verify: pass (rounds used: 1)

**Manual verify steps:**
1. ...
2. ...
3. ...
```

Update `BACKLOG.md`:

- Remove the task from `IN PROGRESS`.
- If empty, restore `- (none)`.
- Do not list done tasks in `BACKLOG.md`; `backlog/done/` is the source of truth.

---

## 9. Commit + Push

Commit only after:

- Implementation is complete.
- Preflight passes.
- Code review is `pass` or `warn`.
- Security review is `pass`, `warn`, or skipped.
- QA is `pass` or `warn`.
- The task has moved to `done`.
- `BACKLOG.md` is updated.

Commands:

```bash
git add -A
git commit -m "<concise message>"
git push -u origin agent/dev
```

Do not create a PR unless the project explicitly wants a PR workflow. If PRs are desired, add a separate post-push workflow.

Final report to the user:

- Done task path.
- Files changed.
- Commit message + short SHA.
- Branch pushed.
- Gate summary.
- Manual verification steps.
- Merge instructions, if applicable.

---

## 10. Automation Loop

You can create a loop script that invokes one AI-agent CLI iteration at a time.

Loop behavior:

1. Read `BACKLOG.md`.
2. Count `TODO` and `IN PROGRESS`.
3. If both are empty, stop.
4. Run exactly one `run-backlog` iteration.
5. Write per-iteration logs.
6. Stop when:
   - CLI exits non-zero.
   - `PREFLIGHT_BLOCKED` appears.
   - `REVIEW_BLOCKED` appears.
   - `VERIFY_BLOCKED` appears.
   - `manual intervention required` appears.
   - Max iterations is reached.

Provider wrappers can be:

- `run-backlog-loop.bat`
- `run-backlog-loop-claude.bat`
- `run-backlog-loop-codex.bat`
- `run-backlog-loop-gemini.bat`
- `run-backlog-loop-claude.ps1`
- `run-backlog-loop-codex.ps1`
- `run-backlog-loop-gemini.ps1`
- `run-backlog-loop-core.ps1`

Core loop parameters:

```text
Provider
MaxIterations
LogDir
Model
NoSkipPermissions
```

If the CLI cannot spawn subagents, the adapter prompt must require it to perform code-review/security/QA in the same session by reading `.agents/agents/*.md`.

### 10.1 Required Loop Script Files

Create the following files under `.agents/scripts/`:

```text
.agents/scripts/
  run-backlog-loop.bat
  run-backlog-loop-core.ps1
  run-backlog-loop-claude.bat
  run-backlog-loop-claude.ps1
  run-backlog-loop-codex.bat
  run-backlog-loop-codex.ps1
  run-backlog-loop-gemini.bat
  run-backlog-loop-gemini.ps1
```

`run-backlog-loop-core.ps1` owns the shared loop behavior:

- Resolve repo root from the script location.
- Create the log directory.
- Check that the selected provider CLI exists on `PATH`.
- Inspect `BACKLOG.md` before each iteration.
- Stop if there is no `TODO` and no `IN PROGRESS`.
- Invoke exactly one agent iteration.
- Tee provider output into per-iteration logs.
- Stop on non-zero CLI exit or blocker sentinels.

The provider-specific `.ps1` files should be thin wrappers around the core script. The provider-specific `.bat` files should only bypass PowerShell execution policy and call the matching `.ps1`.

### 10.2 Default Batch Entrypoint

`run-backlog-loop.bat` is the default human-friendly entrypoint. It can point to the provider you want as the default, usually Claude:

```bat
@echo off
REM Default backlog loop entrypoint.
REM Change this target if your default provider is Codex or Gemini.

setlocal
set "SCRIPT_DIR=%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%run-backlog-loop-claude.ps1" %*
endlocal
```

### 10.3 Provider Batch Wrappers

Create one `.bat` wrapper per provider.

`run-backlog-loop-claude.bat`:

```bat
@echo off
REM Wrapper to run the Claude backlog loop with execution policy bypassed.

setlocal
set "SCRIPT_DIR=%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%run-backlog-loop-claude.ps1" %*
endlocal
```

`run-backlog-loop-codex.bat`:

```bat
@echo off
REM Wrapper to run the Codex backlog loop with execution policy bypassed.

setlocal
set "SCRIPT_DIR=%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%run-backlog-loop-codex.ps1" %*
endlocal
```

`run-backlog-loop-gemini.bat`:

```bat
@echo off
REM Wrapper to run the Gemini backlog loop with execution policy bypassed.

setlocal
set "SCRIPT_DIR=%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%run-backlog-loop-gemini.ps1" %*
endlocal
```

### 10.4 Provider PowerShell Wrappers

Each provider `.ps1` forwards parameters to `run-backlog-loop-core.ps1`.

`run-backlog-loop-claude.ps1`:

```powershell
[CmdletBinding()]
param(
    [int]$MaxIterations = 100,
    [string]$LogDir = "logs/backlog-loop",
    [AllowEmptyString()]
    [string]$Model = "claude-sonnet-4-6",
    [switch]$NoSkipPermissions
)

$coreArgs = @{
    Provider = "claude"
    MaxIterations = $MaxIterations
    LogDir = $LogDir
    Model = $Model
}

if ($NoSkipPermissions) {
    $coreArgs.NoSkipPermissions = $true
}

& "$PSScriptRoot\run-backlog-loop-core.ps1" @coreArgs
exit $LASTEXITCODE
```

`run-backlog-loop-codex.ps1`:

```powershell
[CmdletBinding()]
param(
    [int]$MaxIterations = 100,
    [string]$LogDir = "logs/backlog-loop",
    [AllowEmptyString()]
    [string]$Model = "",
    [switch]$NoSkipPermissions
)

$coreArgs = @{
    Provider = "codex"
    MaxIterations = $MaxIterations
    LogDir = $LogDir
    Model = $Model
}

if ($NoSkipPermissions) {
    $coreArgs.NoSkipPermissions = $true
}

& "$PSScriptRoot\run-backlog-loop-core.ps1" @coreArgs
exit $LASTEXITCODE
```

`run-backlog-loop-gemini.ps1`:

```powershell
[CmdletBinding()]
param(
    [int]$MaxIterations = 100,
    [string]$LogDir = "logs/backlog-loop",
    [AllowEmptyString()]
    [string]$Model = "",
    [switch]$NoSkipPermissions
)

$coreArgs = @{
    Provider = "gemini"
    MaxIterations = $MaxIterations
    LogDir = $LogDir
    Model = $Model
}

if ($NoSkipPermissions) {
    $coreArgs.NoSkipPermissions = $true
}

& "$PSScriptRoot\run-backlog-loop-core.ps1" @coreArgs
exit $LASTEXITCODE
```

### 10.5 Provider Invocation Contract

The core script should construct the provider invocation like this:

- Claude:
  ```text
  claude -p /run-backlog --dangerously-skip-permissions --model <model>
  ```
  Use `< nul` or equivalent so the CLI runs headless.

- Codex:
  ```text
  codex exec -C <repo-root> --dangerously-bypass-approvals-and-sandbox -
  ```
  Pipe an adapter prompt on stdin that instructs Codex to execute exactly one backlog iteration by reading the run-backlog instructions.

- Gemini:
  ```text
  gemini --skip-trust -p "Run the backlog loop using the instructions provided on stdin." --yolo
  ```
  Pipe the same adapter prompt on stdin.

When `NoSkipPermissions` is set, remove provider-specific yolo/skip-permission flags and use the safest non-interactive mode supported by that CLI.

### 10.6 Adapter Prompt for Non-Native Providers

Use an adapter prompt for providers that do not support the native `/run-backlog` command:

```text
You are running the backlog workflow through a non-native CLI adapter.

Goal: execute exactly one backlog task iteration with behavior equivalent to /run-backlog.

Required contract:
1. Read .agents/skills/run-backlog/SKILL.md before changing files.
2. Follow that skill exactly for one iteration only.
3. Read the project guide, .agents/rules/*, the selected task file, and only relevant code.
4. If your CLI cannot spawn subagents, perform code-reviewer, security-auditor, and qa-verifier gates in this same session by reading .agents/agents/*.md and applying the same blocking criteria.
5. Preserve the same stop tokens and print them exactly when blocked: PREFLIGHT_BLOCKED, REVIEW_BLOCKED, VERIFY_BLOCKED, or "manual intervention required".
6. Commit and push only when the run-backlog skill says the task is DONE.
7. Do not ask for confirmation. Work autonomously inside this repository.

Start now.
```

---

## 11. Stop Conditions

Automation must not have a "ship anyway" mode.

Hard stops:

- `TODO is empty`.
- `NO_CHANGES`.
- `PREFLIGHT_BLOCKED`.
- `REVIEW_BLOCKED`.
- `VERIFY_BLOCKED`.
- Security audit remains blocked after max rounds.
- QA remains failed after max rounds.
- Git branch/push conflict cannot be resolved safely.
- Task spec is ambiguous or fails scope-control.

When stopped:

- Do not commit if gates have not passed.
- Report the reason clearly.
- Include git status and changed files when relevant.
- A later agent should be able to resume from `IN PROGRESS`.

---

## 12. Minimum Agent Roles

### Plan Agent

Use when creating `M/L` pending tasks.

Responsibilities:

- Read enough code to draft the task spec.
- Do not implement.
- Do not modify files.
- Locate likely files.
- Identify existing patterns.
- Produce acceptance criteria, verification steps, risk, and scope-control.
- Return structured JSON.

### Code Reviewer

Responsibilities:

- Review staged diff against the task spec.
- Find correctness bugs, regressions, convention violations, and missing verification.
- Return `pass`, `warn`, or `block`.

### Security Auditor

Responsibilities:

- Run only when a sensitive surface is touched.
- Check auth, tokens, secrets, backend writes, persistence, IAP, receipt validation, and data leakage.
- Return `pass`, `warn`, or `block`.

### QA Verifier

Responsibilities:

- Verify every acceptance criterion.
- Check that manual verification steps are sufficient.
- Return `pass`, `warn`, or `fail`.

---

## 13. Porting to Another Project

When installing this system in a new repo, customize:

1. Branch model:
   - automation branch, e.g. `agent/dev`
   - base branch, e.g. `main`, `develop`, `release`
2. Project rules:
   - code style
   - architecture
   - persistence
   - async
   - UI
   - backend/security
3. Sensitive file patterns.
4. Preflight script rules.
5. Build/test commands.
6. Task templates.
7. Agent definitions.
8. Whether final delivery creates a PR or only pushes a branch.

Do not remove:

- `pending/todo/in-progress/done` split.
- Clarification gate.
- Scope-control gate.
- Staged-diff quality gates.
- Max 2 auto-fix rounds.
- No commit before gates pass.
- Hard stop tokens.

---

## 14. Bootstrap Checklist for an AI Agent

If you are an AI agent asked to implement this system in a new project:

1. Create the directory layout in Section 1.
2. Create `BACKLOG.md` with `TODO`, `IN PROGRESS`, and `DONE`.
3. Create `.gitkeep` files in each backlog state folder.
4. Create task templates for `XS/S/M/L`.
5. Create skill/instruction files for:
   - pending task creation
   - add-to-backlog
   - run-backlog
   - execute-backlog loop
6. Create agent instructions for:
   - Plan
   - code-reviewer
   - security-auditor
   - qa-verifier
7. Create deterministic preflight script.
8. Create loop runner script.
9. Adapt branch names and build/test commands to the repo.
10. Add a small example pending task and dry-run the promote flow.
11. Do not run autonomous implementation until the user confirms the first queued task.

---

## 15. Mental Model

Think of the system as a controlled state machine:

```text
user idea
  -> pending task spec
  -> queued task
  -> in-progress implementation
  -> reviewed/verified diff
  -> done summary
  -> pushed agent branch
  -> user manual verification
  -> merge to release branch
```

Safety comes from separating concerns:

- Drafting a task does not queue it.
- Queueing a task does not implement it immediately unless automation is running.
- Implementing a task does not commit until gates pass.
- Passing static gates does not replace manual verification.
- Broad changes are allowed only when scope, impact, migration, tests, checkpoints, and rollback/fallback are explicit.
