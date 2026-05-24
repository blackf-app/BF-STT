git add .
$status = git status -s
$diff = git diff --cached --stat
if (-not $status) {
    Write-Output "NO_CHANGES"
} else {
    Write-Output "--- STATUS ---"
    Write-Output $status
    Write-Output "--- DIFF ---"
    Write-Output $diff
}
