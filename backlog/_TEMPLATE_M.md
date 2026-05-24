### [PRIORITY] Task title (M)

**Tier:** M — multi-file feature, new UI/popup/controller/settings field/event, 3-8 files

---

## Description

What and why, in 3-6 sentences.

## Context & Constraints

- Existing pattern to follow: ...
- Must not change: ...
- Related system: ...

## Related Files

- `path/to/file1.cs` — reason
- `path/to/file2.cs` — reason
- `path/to/file3.xaml` — reason

## Acceptance Criteria

- [ ] Criterion 1 (observable, verifiable).
- [ ] Criterion 2.
- [ ] Criterion 3.
- [ ] Regression: existing behavior X still works.
- [ ] Regression: Settings window still saves/loads correctly.

## Scope-Control Summary

- **Broad change?** No / Yes — explain why.
- **Affected areas:** list modules.
- **Migration plan:** None / describe if settings/data touched.
- **Test/regression plan:** Describe regression checks.
- **Checkpoints:** Observable state after each phase.
- **Rollback/fallback:** None / describe.
- **Out-of-scope:** Do not touch: ...

## Applicable Guardrails

- No API key in logs.
- CancellationToken on all async methods.
- Dispose HttpClient, WebSocket, audio buffers.
- No UI thread blocking.
- XAML bindings use INotifyPropertyChanged.
- SttProviderRegistry for provider registration.

## Risks

- Risk 1 + mitigation.
- Risk 2 + mitigation.

## Manual Verification

1. Step 1.
2. Step 2.
3. Step 3.

## Assumptions

- None / List any assumptions made.
