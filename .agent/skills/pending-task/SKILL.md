---
name: Pending Task
description: Create a new pending task file in backlog/pending/. Use when the user asks to draft, capture, or create a task. Do NOT queue or implement — only draft the spec file.
---

# Pending Task (Workflow A)

Create **one new file** in `backlog/pending/`. Do not edit `BACKLOG.md`. Do not create anything in `backlog/todo/`. Do not commit.

## Pipeline

```
[0] TRIAGE
[1] EXTRACT + CLARIFY
[2] SCOPE-CONTROL GATE
[3] DRAFT
[4] FILENAME
[5] WRITE
[6] CHECK
[7] REPORT
```

---

## Step 0: Triage

Choose tier using concrete signals:

| Tier | Use When |
|---|---|
| `XS` | 1-file tweak, constant/rename/dead-code, no new logic |
| `S` | 1-2 file bug fix or logic tweak, no new UI/save/event |
| `M` | Multi-file feature, new UI/popup/controller/settings/event, 3-8 files |
| `L` | 9+ files, cross-cutting, save migration, system integration |

**Auto-bump rules (apply before choosing tier):**
- Touches API key / auth / settings serialization → at least `M`
- Adds a settings field or save field → at least `M`
- Adds a cross-system event → at least `M`
- Touches provider registration or `SttProviderRegistry` → at least `M`
- Touches more than 2 feature modules or 8+ files → `L`

---

## Step 1: Extract + Clarify

Parse user request into:
- What
- Why
- Scope
- Priority (default: `MEDIUM`)
- Constraints
- Acceptance criteria (if provided)
- Do-not-touch / out-of-scope (if provided)
- Manual verification expectations (if provided)

**Clarification rules:**
- Ask max 3 questions per turn. Ask as many rounds as needed.
- Do NOT guess: product behavior, economy values, save migration, backend/auth behavior, API behavior, acceptance criteria.
- Do NOT ask questions answerable by reading the codebase.
- Only assume low-risk implementation details, and document them as assumptions.

**Must clarify before writing the task file if intent is ambiguous about:**
- Behavior change
- Scope boundary
- Acceptance criteria
- Verification steps
- Security/save/API/UI flow

---

## Step 2: Scope-Control Gate

Before drafting, verify the task does not silently expand:
- No new abstractions unless required.
- No pattern/dependency/schema changes unless explicitly allowed.
- No unrelated refactors.
- If broad changes are necessary, the task MUST document: why, affected areas, migration plan, regression plan, checkpoints, rollback.

For `XS/S`: if the task requires touching modules outside the stated scope, ask the user or split the task. Do NOT expand silently.

---

## Step 3: Draft by Tier

**XS:** Draft directly. No planning pass. Read code only to confirm path/name if uncertain.

**S:** Read 1-3 relevant files to confirm paths and patterns. No planning pass.

**M/L:** Run a planning pass before drafting:
- Read: `.agent/rules/*`, related skill docs, relevant source files.
- Do NOT implement. Do NOT modify files.
- Produce planning JSON:

```json
{
  "summary": "one-sentence restatement",
  "files_to_touch": [{ "path": "path/to/file", "why": "reason" }],
  "pattern_to_follow": "existing pattern name",
  "scope_control": {
    "is_broad_change": false,
    "why_broad_change_is_needed": "none | reason",
    "affected_areas": ["module"],
    "migration_plan": "none | steps",
    "test_regression_plan": ["checkpoint"],
    "checkpoints": ["observable state"],
    "rollback_or_fallback": "none | path",
    "out_of_scope": ["things not to touch"]
  },
  "completion_criteria": ["observable criterion"],
  "verify_steps": ["happy path", "edge case", "regression check"],
  "risks": ["risk + mitigation"],
  "applicable_guardrails": ["pattern", "security", "save"],
  "open_questions": []
}
```

If `open_questions` is non-empty and affects the implementation contract → ask the user before writing the task file.

---

## Step 4: Filename

```
backlog/pending/<YYYYMMDDTHHmmssSSS>-<TIER>-<slug>.md
```

- Timestamp: UTC, millisecond precision.
- TIER: `XS`, `S`, `M`, or `L`.
- Slug: 2-5 kebab-case words.

Example: `backlog/pending/20260524T141233456-M-add-deepgram-provider.md`

Do NOT assign `NNN` here. NNN is assigned only in `add-to-backlog`.

---

## Step 5: Write

Use the matching template from `backlog/_TEMPLATE_<TIER>.md` as the base.

Every task file must include:
- Title + priority
- Description
- Context & constraints
- Related files (with real paths for S/M/L)
- Acceptance criteria (verifiable)
- Scope-control summary
- Applicable guardrails
- Manual verification steps
- Assumptions (if any)
- Out-of-scope items

`M/L` must also include: Risks. `L` must also include: Phases/checkpoints, migration plan, rollback.

---

## Step 6: Quality Check

Before reporting, verify:
- [ ] Title is specific, not vague.
- [ ] No ambiguity remains that affects behavior/completion/verification.
- [ ] File paths are real (for S/M/L).
- [ ] Scope has not expanded without explanation.
- [ ] Acceptance criteria are verifiable (not "works correctly").
- [ ] At least one regression check exists.
- [ ] Broad changes include why/impact/migration/tests/checkpoints/rollback.

---

## Step 7: Report to User

- Selected tier + reason for tier choice.
- Created file path.
- Scope-control summary (1-2 sentences).
- Key assumptions.
- Top 3 acceptance criteria.
- Reminder: use `add-to-backlog` skill when ready to queue this task.
