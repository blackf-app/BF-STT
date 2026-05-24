<#
.SYNOPSIS
    BF-STT Backlog Preflight — deterministic scan of staged diff before LLM review.

.DESCRIPTION
    Scans the staged diff for rule violations that are cheap and deterministic to detect.
    Also runs dotnet build and dotnet test.
    Outputs JSON to stdout.

.PARAMETER Pretty
    Pretty-print the JSON output.

.OUTPUTS
    JSON:
    {
      "summary": {
        "has_blocking_definite": bool,
        "definite_critical_count": int
      },
      "sensitive": {
        "value": bool,
        "reasons": []
      },
      "findings": [
        {
          "rule": "RULE_ID",
          "severity": "critical | warning",
          "definite": bool,
          "file": "path/to/file",
          "line": int,
          "match": "matched text",
          "description": "human-readable explanation"
        }
      ]
    }
#>

param(
    [switch]$Pretty
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Helpers ──────────────────────────────────────────────────────────────────

function Add-Finding {
    param($Rule, $Severity, $Definite, $File, $Line, $Match, $Description)
    [PSCustomObject]@{
        rule        = $Rule
        severity    = $Severity
        definite    = $Definite
        file        = $File
        line        = $Line
        match       = $Match
        description = $Description
    }
}

# ── Get staged diff ───────────────────────────────────────────────────────────

$diff = git diff --cached --unified=0 2>&1
if ($LASTEXITCODE -ne 0) {
    $diff = ""
}

$findings = [System.Collections.Generic.List[PSCustomObject]]::new()
$sensitiveReasons = [System.Collections.Generic.List[string]]::new()

# Parse diff into (file, linenum, content) tuples for added lines only
$currentFile = ""
$currentLine = 0

foreach ($rawLine in ($diff -split "`n")) {
    if ($rawLine -match '^\+\+\+ b/(.+)') {
        $currentFile = $Matches[1].Trim()
        $currentLine = 0
        continue
    }
    if ($rawLine -match '^@@ -\d+(?:,\d+)? \+(\d+)') {
        $currentLine = [int]$Matches[1]
        continue
    }
    if ($rawLine -match '^\+(.*)') {
        $addedContent = $Matches[1]
        $currentLine++

        # ── RULE: Hardcoded API key / secret patterns ──────────────────────
        if ($addedContent -match '(?i)(api[_-]?key|apikey|secret|password|token|auth[_-]?header)\s*[:=]\s*"[A-Za-z0-9_\-\.]{8,}"') {
            $findings.Add((Add-Finding `
                -Rule "HARDCODED_SECRET" `
                -Severity "critical" `
                -Definite $true `
                -File $currentFile `
                -Line $currentLine `
                -Match $addedContent.Trim() `
                -Description "Possible hardcoded credential or API key value."
            ))
            $sensitiveReasons.Add("Hardcoded credential pattern in $currentFile`:$currentLine")
        }

        # ── RULE: Logging API key or auth header ───────────────────────────
        if ($addedContent -match '(?i)(Log\.|logger\.|_logger\.|Console\.Write).*(?:ApiKey|api_key|apikey|Authorization|auth_header|Bearer\s)') {
            $findings.Add((Add-Finding `
                -Rule "LOG_SENSITIVE_DATA" `
                -Severity "critical" `
                -Definite $true `
                -File $currentFile `
                -Line $currentLine `
                -Match $addedContent.Trim() `
                -Description "Potential logging of API key or authorization header."
            ))
            $sensitiveReasons.Add("Possible credential logging in $currentFile`:$currentLine")
        }

        # ── RULE: Thread.Sleep on likely UI or async context ──────────────
        if ($addedContent -match '\bThread\.Sleep\b') {
            $findings.Add((Add-Finding `
                -Rule "THREAD_SLEEP" `
                -Severity "warning" `
                -Definite $false `
                -File $currentFile `
                -Line $currentLine `
                -Match $addedContent.Trim() `
                -Description "Thread.Sleep can block UI thread or starve the thread pool. Use Task.Delay with CancellationToken instead."
            ))
        }

        # ── RULE: Missing CancellationToken on async method ────────────────
        if ($addedContent -match '\basync\b.*\bTask\b' -and $addedContent -notmatch 'CancellationToken' -and $addedContent -notmatch '//') {
            $findings.Add((Add-Finding `
                -Rule "MISSING_CANCELLATION_TOKEN" `
                -Severity "warning" `
                -Definite $false `
                -File $currentFile `
                -Line $currentLine `
                -Match $addedContent.Trim() `
                -Description "Async method signature may be missing a CancellationToken parameter."
            ))
        }

        # ── RULE: Console.WriteLine in non-script C# ───────────────────────
        if ($currentFile -match '\.cs$' -and $addedContent -match '\bConsole\.WriteLine\b') {
            $findings.Add((Add-Finding `
                -Rule "CONSOLE_WRITELINE" `
                -Severity "warning" `
                -Definite $false `
                -File $currentFile `
                -Line $currentLine `
                -Match $addedContent.Trim() `
                -Description "Console.WriteLine in production C# code. Use ILogger instead."
            ))
        }

        # ── RULE: Sensitive surface touched (for security audit trigger) ───
        if ($addedContent -match '(?i)(ApiKey|api_key|appsettings|SettingsService|clipboard|hotkey|inputsimulator|websocket|HttpClient|registry\.Register|SttProviderRegistry)') {
            if ($sensitiveReasons.Count -eq 0 -or $sensitiveReasons[-1] -notmatch [regex]::Escape($currentFile)) {
                $sensitiveReasons.Add("Sensitive surface in $currentFile`:$currentLine")
            }
        }
    }
}

# ── dotnet build ──────────────────────────────────────────────────────────────

$buildOutput = dotnet build --no-restore -v quiet 2>&1
if ($LASTEXITCODE -ne 0) {
    $findings.Add((Add-Finding `
        -Rule "BUILD_FAILED" `
        -Severity "critical" `
        -Definite $true `
        -File "" `
        -Line 0 `
        -Match "" `
        -Description "dotnet build failed. Fix compilation errors before proceeding."
    ))
}

# ── dotnet test ───────────────────────────────────────────────────────────────

$testOutput = dotnet test --no-build --verbosity quiet 2>&1
if ($LASTEXITCODE -ne 0) {
    $findings.Add((Add-Finding `
        -Rule "TESTS_FAILED" `
        -Severity "critical" `
        -Definite $true `
        -File "" `
        -Line 0 `
        -Match "" `
        -Description "dotnet test failed. Fix failing tests before committing."
    ))
}

# ── Assemble output ────────────────────────────────────────────────────────────

$blockingCount = @($findings | Where-Object { $_.severity -eq "critical" -and $_.definite -eq $true }).Count

$result = [PSCustomObject]@{
    summary = [PSCustomObject]@{
        has_blocking_definite    = ($blockingCount -gt 0)
        definite_critical_count  = $blockingCount
    }
    sensitive = [PSCustomObject]@{
        value   = ($sensitiveReasons.Count -gt 0)
        reasons = $sensitiveReasons.ToArray()
    }
    findings = $findings.ToArray()
}

if ($Pretty) {
    $result | ConvertTo-Json -Depth 10
} else {
    $result | ConvertTo-Json -Depth 10 -Compress
}
