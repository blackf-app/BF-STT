<#
.SYNOPSIS
    Core backlog automation loop for BF-STT.

.DESCRIPTION
    Reads BACKLOG.md, counts TODO + IN PROGRESS tasks, and runs one iteration
    of the run-backlog skill per loop cycle via an AI CLI. Stops on hard-stop
    tokens, non-zero exit, or when the backlog is empty.

    Resolves repo root from the script location. Creates the log directory.
    Checks that the selected provider CLI exists on PATH before starting.

.PARAMETER Provider
    AI CLI to use: "claude" | "gemini" | "codex". Default: "claude".

.PARAMETER MaxIterations
    Maximum number of backlog tasks to run in this session. Default: 100.

.PARAMETER LogDir
    Directory for per-iteration log files, relative to repo root.
    Default: "logs/backlog-loop".

.PARAMETER Model
    Model identifier to pass to the AI CLI. Default: "" (use CLI default).

.PARAMETER NoSkipPermissions
    If set, remove provider-specific yolo/skip-permission flags and use the
    safest non-interactive mode supported by that CLI.
#>

[CmdletBinding()]
param(
    [string]$Provider        = "claude",
    [int]   $MaxIterations   = 100,
    [string]$LogDir          = "logs/backlog-loop",
    [AllowEmptyString()]
    [string]$Model           = "",
    [switch]$NoSkipPermissions
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Resolve repo root ─────────────────────────────────────────────────────────
# Script lives at <repo>/.agents/scripts/run-backlog-loop-core.ps1
# So repo root is two directories up.

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

# ── Helpers ────────────────────────────────────────────────────────────────────

function Write-Log {
    param([string]$Message)
    $ts = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    Write-Host "[$ts] $Message"
}

function Test-ProviderCli {
    param([string]$Cli)
    $found = Get-Command $Cli -ErrorAction SilentlyContinue
    if (-not $found) {
        Write-Log "ERROR: '$Cli' is not on PATH. Install or add it before running the loop."
        exit 1
    }
}

function Count-BacklogTasks {
    $backlogFile = Join-Path $repoRoot "BACKLOG.md"
    $content = Get-Content $backlogFile -Raw -ErrorAction SilentlyContinue
    if (-not $content) { return 0 }

    $inSection = $false
    $count = 0

    foreach ($line in ($content -split "`n")) {
        if ($line -match '^## TODO|^## IN PROGRESS') { $inSection = $true; continue }
        if ($line -match '^## (?!TODO|IN PROGRESS)') { $inSection = $false; continue }
        if ($inSection -and $line -match '^\s*-\s+\[(?!none\])') { $count++ }
    }
    return $count
}

# ── Adapter prompt (§10.6) ────────────────────────────────────────────────────
# Used for providers that do not support a native /run-backlog command.

$adapterPrompt = @"
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
"@

$HARD_STOP_TOKENS = @(
    "PREFLIGHT_BLOCKED",
    "REVIEW_BLOCKED",
    "VERIFY_BLOCKED",
    "manual intervention required",
    "TODO is empty"
)

# ── Setup ──────────────────────────────────────────────────────────────────────

$logDirAbs = Join-Path $repoRoot $LogDir
New-Item -ItemType Directory -Force -Path $logDirAbs | Out-Null

# Check CLI exists
$providerLower = $Provider.ToLower()
if ($providerLower -notin @("claude", "gemini", "codex")) {
    Write-Log "ERROR: Unknown provider '$Provider'. Use 'claude', 'gemini', or 'codex'."
    exit 1
}

$cliName = $providerLower
$cliFound = Get-Command $cliName -ErrorAction SilentlyContinue
if (-not $cliFound) {
    Write-Log "ERROR: '$cliName' CLI is not installed or not on PATH."
    switch ($providerLower) {
        "claude" { Write-Log "Install: npm install -g @anthropic-ai/claude-code" }
        "gemini" { Write-Log "Install: npm install -g @google/gemini-cli" }
        "codex"  { Write-Log "Install: npm install -g @openai/codex" }
    }
    exit 1
}

# ── Loop ───────────────────────────────────────────────────────────────────────

Write-Log "Repo root   : $repoRoot"
Write-Log "Provider    : $Provider"
Write-Log "MaxIterations: $MaxIterations"
Write-Log "LogDir      : $logDirAbs"
Write-Log "Model       : $(if ($Model) { $Model } else { '(CLI default)' })"
Write-Log "SkipPerms   : $(-not $NoSkipPermissions)"
Write-Log "Starting backlog loop..."

for ($i = 1; $i -le $MaxIterations; $i++) {
    $taskCount = Count-BacklogTasks
    if ($taskCount -eq 0) {
        Write-Log "TODO is empty. Loop complete after $($i - 1) iteration(s)."
        break
    }

    Write-Log "--- Iteration $i/$MaxIterations ($taskCount task(s) remaining) ---"

    $logFile = Join-Path $logDirAbs ("iteration-{0:D3}-{1}.log" -f $i, (Get-Date -Format "yyyyMMddTHHmmss"))

    # ── Build CLI invocation (§10.5) ──────────────────────────────────────────

    switch ($providerLower) {

        "claude" {
            # Validated: claude 2.1.139
            # claude --print "<prompt>" [--dangerously-skip-permissions] [--model <model>]
            # -p/--print = non-interactive headless mode.
            $cliArgs = @("--print", $adapterPrompt)
            if (-not $NoSkipPermissions) {
                $cliArgs += "--dangerously-skip-permissions"
            }
            if ($Model) { $cliArgs += @("--model", $Model) }

            $output = & claude @cliArgs 2>&1
            $exitCode = $LASTEXITCODE
        }

        "gemini" {
            # Validated: gemini 0.41.2
            # gemini --skip-trust --prompt "<prompt>" [--yolo] [--model <model>]
            # -p/--prompt = non-interactive headless mode.
            # Adapter prompt passed via --prompt; stdin appended if needed.
            $cliArgs = @("--skip-trust", "--prompt", $adapterPrompt)
            if ($Model) { $cliArgs += @("--model", $Model) }
            if (-not $NoSkipPermissions) { $cliArgs += "--yolo" }

            $output = & gemini @cliArgs 2>&1
            $exitCode = $LASTEXITCODE
        }

        "codex" {
            # codex CLI — install: npm install -g @openai/codex
            # codex exec [--model <model>] [--dangerously-bypass-approvals-and-sandbox] -
            $cliArgs = @("exec")
            if ($Model) { $cliArgs += @("--model", $Model) }
            if (-not $NoSkipPermissions) { $cliArgs += "--dangerously-bypass-approvals-and-sandbox" }
            $cliArgs += "-"

            $oldErrorActionPreference = $ErrorActionPreference
            try {
                $ErrorActionPreference = "Continue"
                $output = $adapterPrompt | & codex @cliArgs 2>&1
                $exitCode = $LASTEXITCODE
            }
            finally {
                $ErrorActionPreference = $oldErrorActionPreference
            }
        }
    }

    # ── Tee output to log ─────────────────────────────────────────────────────

    $output | Out-File -FilePath $logFile -Encoding utf8
    Write-Log "Log: $logFile"

    # ── Check exit code ───────────────────────────────────────────────────────

    if ($exitCode -ne 0) {
        Write-Log "CLI exited with code $exitCode. Stopping loop."
        exit $exitCode
    }

    # ── Check hard-stop tokens ────────────────────────────────────────────────

    $outputText = $output -join "`n"
    foreach ($token in $HARD_STOP_TOKENS) {
        if ($outputText -match [regex]::Escape($token)) {
            Write-Log "Hard stop detected: $token"
            exit 1
        }
    }

    Write-Log "Iteration $i complete."
}

Write-Log "Backlog loop finished."
