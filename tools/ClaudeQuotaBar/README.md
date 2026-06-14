# ClaudeQuotaBar

Small macOS menu bar app that shows the Claude Code 5-hour session quota from
the Claude credentials already available on this Mac.

It does not store or print OAuth tokens. It shells out to Claude Code:

```sh
claude -p "/usage" --output-format json --tools "" --no-session-persistence
```

Claude Code handles Keychain/OAuth refresh. The command returns usage metadata
with `total_cost_usd: 0` and no model turn.

## Run

```sh
cd tools/ClaudeQuotaBar
swift run ClaudeQuotaBar
```

To verify from the terminal without opening the menu bar app:

```sh
swift run ClaudeQuotaBar --once
```

If `claude` is not on `PATH`, the app auto-detects common editor extension
installations. You can also set an explicit binary path:

```sh
CLAUDE_BINARY=/path/to/claude swift run ClaudeQuotaBar
```

## Build an app bundle

```sh
cd tools/ClaudeQuotaBar
chmod +x scripts/build-app.sh
./scripts/build-app.sh
open dist/ClaudeQuotaBar.app
```
