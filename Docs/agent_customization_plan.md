# BF-STT Subagent Customization Plan

This document evaluates `D:\AppDevelop\template\awesome-claude-code-subagents-main` as a source of reusable agent patterns, role definitions, and checklists. The goal is **not** to copy generic agents as-is. The goal is to extract the situations they were designed for and customize them for BF-STT.

BF-STT is a Windows desktop WPF/.NET 8 speech-to-text application. Its main risk areas are audio capture, real-time streaming, STT provider integration, global hotkeys, clipboard/input injection, secure settings, and Windows release automation.

## Selection Principles

- Bring in an agent only when its usage scenario maps to a real BF-STT risk: real-time audio, WebSocket streaming, cancellation, provider APIs, API keys, WPF UI, or Windows release flow.
- Remove web, cloud, Kubernetes, database, and microservice guidance unless it directly applies to this application.
- Rename and rewrite agents around BF-STT-specific triggers. Prefer names such as `bf-audio-performance-engineer` over generic names such as `performance-engineer`.
- Agents should review the modules that actually exist in this codebase: `Services/Audio`, `Services/STT`, `Services/Workflow`, `Services/Platform`, `Services/Infrastructure`, `ViewModels`, and WPF XAML.
- Existing project skills under `.agents/skills` should take priority over generic agents when the task is a repeatable workflow, especially adding a new STT provider.

## Agents To Bring In And Customize

### 1. BF .NET WPF Engineer

Template sources to adapt:
- `categories/02-language-specialists/csharp-developer.md`
- selected parts of `categories/02-language-specialists/dotnet-core-expert.md`

Customize as:
- `bf-dotnet-wpf-engineer.md`

Use when:
- Building or modifying WPF/MVVM features.
- Changing service lifecycle, dependency injection, settings, or state-machine behavior.
- Adding logic around providers, recording workflow, hotkeys, clipboard, or input injection.

Keep:
- Nullable reference type discipline.
- Async/await, cancellation tokens, and exception handling.
- Dependency injection and structured logging.
- Testability and interface boundaries.

Remove:
- ASP.NET Core, Minimal APIs, EF Core, Kubernetes, cloud-native, and microservice guidance.
- .NET 10/C# 14 assumptions because BF-STT currently targets `net8.0-windows`.

BF-STT checklist:
- Do not block the UI thread.
- Every long-running operation must have a cancellation path.
- Dispose WebSocket, stream, audio, and cancellation resources correctly.
- Do not add new provider-specific if/else chains when `SttProviderRegistry` is the right extension point.
- WPF bindings must raise property change notifications where the UI depends on them.

### 2. BF Code Reviewer

Template sources to adapt:
- `categories/04-quality-security/code-reviewer.md`
- `categories/04-quality-security/architect-reviewer.md`
- `categories/06-developer-experience/refactoring-specialist.md`

Customize as:
- `bf-code-reviewer.md`

Use when:
- Reviewing a change set before commit.
- Reviewing workflow, provider, audio, or settings refactors.
- Evaluating plugin extraction or contract changes.

Keep:
- Correctness, maintainability, error handling, and resource management.
- Regression risk and test review.
- Detection of duplication, hidden coupling, long methods, and broad-change smells.

BF-STT checklist:
- State transitions under `Services/Workflow/States` must not create stuck or contradictory states.
- `RecordingCoordinator`, `BatchProcessor`, and `StreamingManager` must avoid race conditions.
- New providers must not break Test Mode, Settings Window behavior, or registry updates.
- Logs must not expose API keys or overly sensitive audio/transcript data.

### 3. BF Test Engineer

Template sources to adapt:
- `categories/04-quality-security/test-automator.md`
- `categories/04-quality-security/qa-expert.md`
- selected parts of `categories/04-quality-security/ui-ux-tester.md`

Customize as:
- `bf-test-engineer.md`

Use when:
- Adding tests for services, providers, filters, and audio logic.
- Creating characterization tests before refactoring.
- Planning manual validation for Settings and Test Mode.

Keep:
- Test strategy, edge cases, regression coverage, and CI/build integration.
- Defect reports with reproduction steps.

Customize to the current stack:
- `xUnit`
- `NSubstitute`
- `Microsoft.NET.Test.Sdk`
- No default Playwright/web UI test flow because BF-STT is a WPF desktop app.

BF-STT checklist:
- Test missing API keys, empty audio, invalid language, and cancellation.
- Test provider response parsing with representative JSON samples.
- Test `HallucinationFilter`, VAD, AGC, and silence detection with boundary inputs.
- Test `SttProviderRegistry`: batch-only providers, streaming providers, and unknown provider fallback.

### 4. BF Security Auditor

Template sources to adapt:
- `categories/04-quality-security/security-auditor.md`
- `categories/06-developer-experience/dependency-manager.md`
- selected parts of `categories/04-quality-security/powershell-security-hardening.md`

Customize as:
- `bf-security-auditor.md`

Use when:
- Changing settings, API key handling, logging, update scripts, or release scripts.
- Adding a new provider API integration.
- Changing clipboard, input injection, or hotkey behavior.

Keep:
- Secret handling, dependency vulnerability review, and configuration security.
- Third-party API/auth header review.
- Evidence-based findings and remediation steps.

BF-STT checklist:
- API keys must not be committed through `appsettings.json`.
- API keys must not appear in logs, exception messages, debug output, or release artifacts.
- Registry/file settings must be serialized and protected safely.
- Clipboard restoration must not destroy user data.
- Input injection and hotkeys must only execute user-intended actions.
- Release artifacts must not contain local secret files.

### 5. BF Audio Performance Engineer

Template sources to adapt:
- `categories/04-quality-security/performance-engineer.md`
- selected parts of `categories/05-data-ai/nlp-engineer.md`
- selected parts of `categories/05-data-ai/ai-engineer.md`

Customize as:
- `bf-audio-performance-engineer.md`

Use when:
- Improving real-time streaming latency.
- Changing audio capture, resampling, RNNoise, AGC, or silence detection.
- Optimizing allocation behavior, buffer sizes, and UI responsiveness.

Keep:
- Profiling, bottleneck analysis, memory/CPU/I/O review.
- Baselines and regression measurements.

Remove:
- Database tuning, CDN guidance, web load testing, and cloud autoscaling.

BF-STT checklist:
- Streaming must send audio frames evenly, without abnormal bursts.
- The audio pipeline must not allocate large buffers per frame.
- Stop/cancel must not leave WebSocket receive loops hanging.
- The UI must remain responsive when Test Mode runs multiple providers.
- Sound feedback must not fight the active audio capture path.

### 6. BF Build Release Engineer

Template sources to adapt:
- `categories/06-developer-experience/build-engineer.md`
- `categories/02-language-specialists/powershell-5.1-expert.md`
- `categories/06-developer-experience/powershell-module-architect.md`

Customize as:
- `bf-build-release-engineer.md`

Use when:
- Changing `scripts/release.ps1`, `pre_build.ps1`, `post_build.ps1`, or `increment_version.ps1`.
- Changing Windows single-file publish behavior.
- Adding release preflight checks, tag validation, or artifact validation.

Keep:
- Reproducible builds, clear errors, versioning, and release automation.
- PowerShell parameter validation, dry-run behavior, and logging.

BF-STT checklist:
- Debug builds must not publish automatically.
- Release builds must not recurse through `AutoPublishOnRelease`.
- Version bump behavior must be deliberate and observable.
- `publish/` must not be committed.
- A running app must be stopped and restarted safely.

### 7. BF Technical Writer

Template sources to adapt:
- `categories/08-business-product/technical-writer.md`
- `categories/06-developer-experience/documentation-engineer.md`
- `categories/07-specialized-domains/api-documenter.md`

Customize as:
- `bf-technical-writer.md`

Use when:
- Updating README, troubleshooting docs, provider guides, or release notes.
- Writing instructions for adding providers or configuring API keys.
- Keeping docs aligned with the Settings UI and actual workflow behavior.

Keep:
- Task-based documentation, troubleshooting, migration notes, and release notes.
- Provider/API integration guides.

BF-STT checklist:
- Docs must clearly distinguish batch and streaming mode.
- Each provider guide should identify endpoint, auth header, model, and language behavior.
- Setup instructions must not encourage committing secrets.
- Troubleshooting should cover audio devices, hotkey conflicts, clipboard behavior, and API errors.

## Use As Inspiration, Not As Raw Files

### Product, Business, And Research Agents

Template sources:
- `product-manager.md`
- `business-analyst.md`
- `research-analyst.md`
- `market-researcher.md`
- `competitive-analyst.md`

Use their ideas for a small workflow, not a full agent yet:
- Research a new STT provider.
- Compare pricing, latency, language support, and streaming support.
- Create backlog-ready tasks with acceptance criteria.

Recommended skill:
- `.agents/skills/research-stt-provider/SKILL.md`

Expected output:
- Provider comparison table.
- Integration risks.
- Endpoint/auth/model notes.
- Recommendation on whether BF-STT should add the provider.

### Workflow Orchestration Agents

Template sources:
- `workflow-orchestrator.md`
- `codebase-orchestrator.md`
- `context-manager.md`

Use their ideas for:
- Backlog automation.
- Coordinating large work through plan, implementation, review, test, and release phases.

Do not bring the full meta-orchestration set in yet:
- The full orchestration layer may make the project workflow heavier than needed.
- Apply it only after `BACKLOG_AUTOMATION_WORKFLOW.md` becomes an active process.

## Do Not Bring In

- Cloud/Kubernetes/Terraform/SRE agents: they do not match the current desktop application.
- React/Vue/Angular/Next.js/frontend web agents: BF-STT uses WPF.
- Database/Postgres/SQL agents: BF-STT does not currently have a database layer.
- Mobile/game/blockchain/fintech/WordPress agents: unrelated to the current product.
- `install-agents.sh`, `.claude-plugin/marketplace.json`, and `tools/subagent-catalog/*`: these are template infrastructure, not project knowledge.

## Implementation Structure

```text
.agents/
  agents/
    bf-dotnet-wpf-engineer.md
    bf-code-reviewer.md
    bf-test-engineer.md
    bf-security-auditor.md
    bf-audio-performance-engineer.md
    bf-build-release-engineer.md
    bf-technical-writer.md
  skills/
    add-stt-provider/
      SKILL.md
    research-stt-provider/
      SKILL.md
    release-preflight/
      SKILL.md
  rules/
    architecture.md
    security.md
    testing.md
    release.md
```

## Priority Order

1. Create `.agents/agents/bf-code-reviewer.md`, `bf-test-engineer.md`, and `bf-security-auditor.md`.
2. Upgrade `.agents/skills/add-stt-provider/SKILL.md` so it matches `SttProviderRegistry`, the test project, and secret-handling requirements.
3. Create `bf-audio-performance-engineer.md` before large audio or streaming changes.
4. Create `bf-build-release-engineer.md` before publish, versioning, or release automation changes.
5. Create `research-stt-provider` before adding a new provider based on external research.
6. Implement full backlog automation only when repeated queued tasks need clear acceptance criteria and automated execution.

## Acceptance Criteria For Bringing Anything In

An agent or skill should be added to BF-STT only when these four questions have concrete answers:

1. In what BF-STT situation should it be triggered?
2. Which files or modules should it inspect?
3. What concrete output should it produce: code, review, test plan, release checklist, or documentation?
4. Which project-specific risk does it reduce that a generic agent would miss?

If those questions cannot be answered, keep the item in the external template as reference material and do not add it to this repository.

