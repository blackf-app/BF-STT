---
name: Add to Backlog
description: Promote one or more pending tasks from backlog/pending/ into backlog/todo/ and update BACKLOG.md. Use when the user says "add to backlog", "queue task", or "promote task". Do NOT implement anything.
---

# Add to Backlog (Workflow B)

Move tasks from `backlog/pending/` to `backlog/todo/`, assign sequential NNN, and update `BACKLOG.md`.

Do NOT commit. Do NOT implement. Let the user review the queue before running automation.

## Pipeline

```
[0] CONFIRM_INTENT
[1] LIST
[2] DISPLAY
[3] PICK
[4] OVERRIDE_PRIORITY
[5] ASSIGN_NNN
[6] MOVE
[7] UPDATE_BACKLOG
[8] REPORT
```

---

## Step 0: Confirm Intent

Confirm the user wants to queue (promote) tasks, not implement them.

---

## Step 1: List Pending

Glob: `backlog/pending/*.md` (ignore `.gitkeep`).

Parse filename: `<timestamp>-<TIER>-<slug>.md`

Read first heading of each file to extract priority and title:
```md
### [HIGH] Task title
```

Sort newest first (by timestamp).

---

## Step 2: Display

Show the list:

```
[1] [M]  [HIGH]   Add Deepgram provider        - 2026-05-24 14:23
[2] [S]  [MEDIUM] Fix notification badge        - 2026-05-24 14:25
[3] [XS] [LOW]    Remove unused constant        - 2026-05-24 14:30
```

---

## Step 3: Pick

Accept any of:
- `1`
- `1,3`
- `1 3 5`
- `1-3`
- `1-2,4`
- `all`

---

## Step 4: Override Priority (Optional)

Allow the user to override priority during pick:
```
2:HIGH, 4:LOW
```

Tier does NOT change at this step. If tier is wrong, edit the pending file or create a replacement task.

---

## Step 5: Assign NNN

Scan filenames in:
- `backlog/todo/`
- `backlog/in-progress/`
- `backlog/done/`

Find max NNN prefix. Assign the next sequential numbers to selected tasks.

Example: if max existing is `003`, assign `004`, `005`, ...

---

## Step 6: Move

Use `git mv`:

```powershell
git mv "backlog/pending/<timestamp>-<TIER>-<slug>.md" "backlog/todo/<NNN>-<slug>.md"
```

Preserve the slug from the pending filename.

---

## Step 7: Update BACKLOG.md

Open `BACKLOG.md`. Insert into the correct priority bucket under `## TODO`:

```md
- [HIGH] [M] [Add Deepgram provider](backlog/todo/004-add-deepgram-provider.md)
```

Ordering rules:
- `HIGH` tasks first.
- `MEDIUM` tasks next.
- `LOW` tasks last.
- Within same priority, preserve insertion order.

Do NOT commit yet.

---

## Step 8: Report

- List of tasks promoted (pending path → todo path).
- NNN assigned to each.
- Current BACKLOG.md TODO section.
- Reminder: run `run-backlog` skill or the automation loop to execute tasks.
