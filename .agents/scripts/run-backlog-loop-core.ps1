<#
.SYNOPSIS
    Core backlog automation loop for BF-STT.

.DESCRIPTION
    Reads BACKLOG.md, counts TODO + IN PROGRESS tasks, and runs one iteration
    of the run-backlog skill per loop cycle via an AI CLI. Stops on hard-stop
    tokens, non-zero exit, or when the backlog is empty.

.PARAMETER Provider
    AI CLI to use: "claude" | "gemini" | "codex". Default: "claude".

.PARAMETER MaxIterations
    Maximum number of backlog tasks to run in this session. Default: 10.

.PARAMETER LogDir
    Directory for per-iteration log files. Default: ".agents/logs/backlog".

.PARAMETER Model
    Model identifier to pass to the AI CLI. Default: "" (use CLI default).

.PARAMETER NoSkipPermissions
    If set, do NOT pass --dangerously-skip-permissions to claude CLI.
    By default, skip-permissions is enabled for unattended runs.
#>

param(
    [string]$Provider        = "claude",
    [int]   $MaxIterations   = 10,
    [string]$LogDir          = ".agents/logs/backlog",
    [string]$Model           = "",
    [switch]$NoSkipPermissions
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Helpers ────────────────────────────────────────────────────────────────────

function Write-Log {
    param([string]$Message)
    $ts = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    Write-Host "[$ts] $Message"
}

function Count-BacklogTasks {
    $content = Get-Content "BACKLOG.md" -Raw -ErrorAction SilentlyContinue
    if (-not $content) { return 0 }

    $inTodoSection = $false
    $inInProgressSection = $false
    $count = 0

    foreach ($line in ($content -split "`n")) {
        if ($line -match '^## TODO') { $inTodoSection = $true; $inInProgressSection = $false; continue }
        if ($line -match '^## IN PROGRESS') { $inInProgressSection = $true; $inTodoSection = $false; continue }
        if ($line -match '^## ') { $inTodoSection = $false; $inInProgressSection = $false; continue }

        if (($inTodoSection -or $inInProgressSection) -and $line -match '^\s*-\s+\[(?!none)') {
            $count++
        }
    }
    return $count
}

$HARD_STOP_TOKENS = @(
    "PREFLIGHT_BLOCKED",
    "REVIEW_BLOCKED",
    "VERIFY_BLOCKED",
    "manual intervention required",
    "TODO is empty"
)

# ── Setup ──────────────────────────────────────────────────────────────────────

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

$runPrompt = @"
Read the skill file at .agents/skills/run-backlog/SKILL.md and follow it exactly.
Execute one full backlog task cycle: pick, branch, implement, preflight, review, QA, mark done, commit, push.
Stop immediately and report if any hard-stop condition is encountered.
"@

# ── Loop ───────────────────────────────────────────────────────────────────────

Write-Log "Starting backlog loop. Provider=$Provider MaxIterations=$MaxIterations"

for ($i = 1; $i -le $MaxIterations; $i++) {
    $taskCount = Count-BacklogTasks
    if ($taskCount -eq 0) {
        Write-Log "TODO is empty. Loop complete after $($i - 1) iteration(s)."
        break
    }

    Write-Log "Iteration $i/$MaxIterations — $taskCount task(s) remaining."

    $logFile = Join-Path $LogDir ("iteration-{0:D3}-{1}.log" -f $i, (Get-Date -Format "yyyyMMddTHHmmss"))

    # ── Build CLI command ─────────────────────────────────────────────────────

    switch ($Provider.ToLower()) {
        "claude" {
            $cliArgs = @("--print")
            if (-not $NoSkipPermissions) {
                $cliArgs += "--dangerously-skip-permissions"
            }
            if ($Model) {
                $cliArgs += @("--model", $Model)
            }
            $cliArgs += $runPrompt

            $output = & claude @cliArgs 2>&1
            $exitCode = $LASTEXITCODE
        }
        "gemini" {
            $cliArgs = @()
            if ($Model) {
                $cliArgs += @("--model", $Model)
            }
            $cliArgs += $runPrompt

            $output = & gemini @cliArgs 2>&1
            $exitCode = $LASTEXITCODE
        }
        "codex" {
            # OpenAI Codex CLI: codex [--model <model>] --approval-mode full-auto "<prompt>"
            $cliArgs = @("--approval-mode", "full-auto")
            if ($Model) {
                $cliArgs += @("--model", $Model)
            }
            $cliArgs += $runPrompt

            $output = & codex @cliArgs 2>&1
            $exitCode = $LASTEXITCODE
        }
        default {
            Write-Log "ERROR: Unknown provider '$Provider'. Use 'claude', 'gemini', or 'codex'."
            exit 1
        }
    }

    # ── Write log ─────────────────────────────────────────────────────────────

    $output | Out-File -FilePath $logFile -Encoding utf8
    Write-Log "Log written: $logFile"

    # ── Check exit code ───────────────────────────────────────────────────────

    if ($exitCode -ne 0) {
        Write-Log "CLI exited with code $exitCode. Stopping loop."
        Write-Log "See log: $logFile"
        exit $exitCode
    }

    # ── Check hard-stop tokens ────────────────────────────────────────────────

    $outputText = $output -join "`n"
    foreach ($token in $HARD_STOP_TOKENS) {
        if ($outputText -match [regex]::Escape($token)) {
            Write-Log "Hard stop detected: $token"
            Write-Log "See log: $logFile"
            exit 1
        }
    }

    Write-Log "Iteration $i complete."
}

Write-Log "Backlog loop finished."
