### [PRIORITY] Task title (L)

**Tier:** L — cross-cutting work: 9+ files, backend surface, save migration, system integration

---

## Description

What and why, in full detail. Include business motivation.

## Context & Constraints

- Existing patterns to follow: ...
- Must not change: ...
- Related systems: ...
- Dependencies: ...

## Related Files

- `path/to/file1.cs` — reason
- `path/to/file2.cs` — reason
- *(list all expected files)*

## Acceptance Criteria

- [ ] Criterion 1 (observable, verifiable).
- [ ] Criterion 2.
- [ ] Criterion 3.
- [ ] Regression: ...
- [ ] Regression: Settings window still correct.
- [ ] Regression: All existing providers still work.

## Phases & Checkpoints

### Phase 1: [Name]
- [ ] Step 1.1
- [ ] Step 1.2
- **Observable checkpoint:** What is true after phase 1.

### Phase 2: [Name]
- [ ] Step 2.1
- [ ] Step 2.2
- **Observable checkpoint:** What is true after phase 2.

### Phase 3: [Name]
- [ ] Step 3.1
- **Observable checkpoint:** What is true after phase 3.

## Scope-Control Summary

- **Broad change?** Yes — explain why.
- **Affected areas:** list all modules/systems.
- **Migration plan:** Detail if settings/save/data/schema touched.
- **Test/regression plan:** Full regression checklist.
- **Checkpoints:** See phases above.
- **Rollback/fallback:** Describe rollback path.
- **Out-of-scope:** Do not touch: ...

## Applicable Guardrails

- No API key in logs.
- CancellationToken on all async methods.
- Dispose HttpClient, WebSocket, audio buffers.
- No UI thread blocking.
- XAML bindings use INotifyPropertyChanged.
- SttProviderRegistry for provider registration.
- Settings backward compatibility.

## Risks

- Risk 1 + mitigation.
- Risk 2 + mitigation.
- Risk 3 + mitigation.

## Manual Verification

1. Step 1.
2. Step 2.
3. Step 3.
4. Step 4.

## Assumptions

- None / List any assumptions made.
