import AppKit
import Foundation

struct LimitSnapshot {
    let usedPercentage: Double
    let resetDescription: String?
}

struct UsageSnapshot {
    let session: LimitSnapshot?
    let weekAllModels: LimitSnapshot?
    let weekSonnet: LimitSnapshot?
    let rawText: String
    let updatedAt: Date
    let claudePath: String
}

struct ClaudeResult: Decodable {
    let type: String?
    let subtype: String?
    let isError: Bool?
    let result: String?

    enum CodingKeys: String, CodingKey {
        case type
        case subtype
        case isError = "is_error"
        case result
    }
}

enum UsageError: LocalizedError {
    case claudeBinaryNotFound
    case commandFailed(Int32, String)
    case missingResult
    case invalidOutput(String)

    var errorDescription: String? {
        switch self {
        case .claudeBinaryNotFound:
            return "Could not find Claude Code binary. Set CLAUDE_BINARY to its path."
        case let .commandFailed(status, output):
            return "Claude usage command failed (\(status)): \(output)"
        case .missingResult:
            return "Claude usage command did not return a result field."
        case let .invalidOutput(output):
            return "Could not parse Claude usage output: \(output)"
        }
    }
}

final class ClaudeUsageFetcher {
    private let fileManager = FileManager.default

    func fetch() throws -> UsageSnapshot {
        let claudePath = try findClaudeBinary()
        let output = try runClaudeUsage(claudePath: claudePath)
        let usageText = try extractResultText(from: output)

        return UsageSnapshot(
            session: parseLimit(label: "Current session", text: usageText),
            weekAllModels: parseLimit(label: "Current week (all models)", text: usageText),
            weekSonnet: parseLimit(label: "Current week (Sonnet only)", text: usageText),
            rawText: usageText,
            updatedAt: Date(),
            claudePath: claudePath
        )
    }

    private func findClaudeBinary() throws -> String {
        if let envPath = ProcessInfo.processInfo.environment["CLAUDE_BINARY"],
           isExecutable(envPath) {
            return envPath
        }

        let directCandidates = [
            "/opt/homebrew/bin/claude",
            "/usr/local/bin/claude",
            "/usr/bin/claude"
        ]

        if let candidate = directCandidates.first(where: isExecutable) {
            return candidate
        }

        let home = fileManager.homeDirectoryForCurrentUser.path
        let extensionRoots = [
            "\(home)/.antigravity-ide/extensions",
            "\(home)/.vscode/extensions",
            "\(home)/.vscode-insiders/extensions",
            "\(home)/.cursor/extensions",
            "\(home)/.windsurf/extensions"
        ]

        var extensionCandidates: [(path: String, modifiedAt: Date)] = []
        for root in extensionRoots {
            guard let items = try? fileManager.contentsOfDirectory(atPath: root) else {
                continue
            }

            for item in items where item.hasPrefix("anthropic.claude-code-") {
                let path = "\(root)/\(item)/resources/native-binary/claude"
                guard isExecutable(path) else {
                    continue
                }

                let attrs = try? fileManager.attributesOfItem(atPath: path)
                let modifiedAt = attrs?[.modificationDate] as? Date ?? .distantPast
                extensionCandidates.append((path, modifiedAt))
            }
        }

        if let newest = extensionCandidates.sorted(by: { $0.modifiedAt > $1.modifiedAt }).first {
            return newest.path
        }

        return try findViaShellPath()
    }

    private func findViaShellPath() throws -> String {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: "/usr/bin/env")
        process.arguments = ["zsh", "-lc", "command -v claude"]

        let outputPipe = Pipe()
        process.standardOutput = outputPipe
        process.standardError = Pipe()
        try process.run()
        process.waitUntilExit()

        guard process.terminationStatus == 0 else {
            throw UsageError.claudeBinaryNotFound
        }

        let data = outputPipe.fileHandleForReading.readDataToEndOfFile()
        let path = String(data: data, encoding: .utf8)?
            .trimmingCharacters(in: .whitespacesAndNewlines) ?? ""

        guard isExecutable(path) else {
            throw UsageError.claudeBinaryNotFound
        }
        return path
    }

    private func isExecutable(_ path: String) -> Bool {
        fileManager.isExecutableFile(atPath: path)
    }

    private func runClaudeUsage(claudePath: String) throws -> String {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: claudePath)
        process.arguments = [
            "-p",
            "/usage",
            "--output-format",
            "json",
            "--tools",
            "",
            "--no-session-persistence"
        ]

        let outputPipe = Pipe()
        let errorPipe = Pipe()
        process.standardOutput = outputPipe
        process.standardError = errorPipe

        try process.run()
        process.waitUntilExit()

        let outputData = outputPipe.fileHandleForReading.readDataToEndOfFile()
        let errorData = errorPipe.fileHandleForReading.readDataToEndOfFile()
        let output = String(data: outputData, encoding: .utf8) ?? ""
        let error = String(data: errorData, encoding: .utf8) ?? ""

        guard process.terminationStatus == 0 else {
            throw UsageError.commandFailed(
                process.terminationStatus,
                truncate(error.isEmpty ? output : error)
            )
        }

        return output
    }

    private func extractResultText(from output: String) throws -> String {
        let trimmed = output.trimmingCharacters(in: .whitespacesAndNewlines)
        guard let data = trimmed.data(using: .utf8), !data.isEmpty else {
            throw UsageError.invalidOutput("empty output")
        }

        let decoder = JSONDecoder()
        if let result = try? decoder.decode(ClaudeResult.self, from: data),
           let text = result.result {
            return text
        }

        if let results = try? decoder.decode([ClaudeResult].self, from: data),
           let text = results.last(where: { $0.type == "result" || $0.result != nil })?.result {
            return text
        }

        throw UsageError.missingResult
    }

    private func parseLimit(label: String, text: String) -> LimitSnapshot? {
        guard let line = text
            .components(separatedBy: .newlines)
            .first(where: { $0.hasPrefix("\(label):") }) else {
            return nil
        }

        guard let used = firstMatch(#"([0-9]+(?:\.[0-9]+)?)% used"#, in: line),
              let usedPercentage = Double(used) else {
            return nil
        }

        let resetDescription = firstMatch(#"resets\s+(.+)$"#, in: line)
        return LimitSnapshot(usedPercentage: usedPercentage, resetDescription: resetDescription)
    }

    private func firstMatch(_ pattern: String, in text: String) -> String? {
        guard let regex = try? NSRegularExpression(pattern: pattern) else {
            return nil
        }

        let range = NSRange(text.startIndex..<text.endIndex, in: text)
        guard let match = regex.firstMatch(in: text, range: range),
              match.numberOfRanges >= 2,
              let matchRange = Range(match.range(at: 1), in: text) else {
            return nil
        }

        return String(text[matchRange])
    }

    private func truncate(_ text: String, limit: Int = 300) -> String {
        let clean = text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard clean.count > limit else {
            return clean
        }

        let index = clean.index(clean.startIndex, offsetBy: limit)
        return String(clean[..<index]) + "..."
    }
}

final class AppDelegate: NSObject, NSApplicationDelegate {
    private let statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
    private let fetcher = ClaudeUsageFetcher()
    private var timer: Timer?
    private var isRefreshing = false
    private var lastSnapshot: UsageSnapshot?
    private var lastError: Error?

    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)
        statusItem.button?.title = "Claude --"
        rebuildMenu()
        refresh()

        timer = Timer.scheduledTimer(withTimeInterval: 60, repeats: true) { [weak self] _ in
            self?.refresh()
        }
    }

    private func refresh() {
        guard !isRefreshing else {
            return
        }

        isRefreshing = true
        updateTitleLoading()

        DispatchQueue.global(qos: .utility).async { [weak self] in
            guard let self else {
                return
            }

            let result = Result { try self.fetcher.fetch() }
            DispatchQueue.main.async {
                self.isRefreshing = false

                switch result {
                case let .success(snapshot):
                    self.lastSnapshot = snapshot
                    self.lastError = nil
                case let .failure(error):
                    self.lastError = error
                }

                self.rebuildMenu()
            }
        }
    }

    private func updateTitleLoading() {
        if lastSnapshot == nil {
            statusItem.button?.title = "Claude ..."
        }
    }

    private func rebuildMenu() {
        if let snapshot = lastSnapshot {
            statusItem.button?.title = formatMenuBarTitle(snapshot)
        } else if lastError != nil {
            statusItem.button?.title = "Claude !"
        } else {
            statusItem.button?.title = "Claude --"
        }

        let menu = NSMenu()

        if let snapshot = lastSnapshot {
            addLimitItems(snapshot, to: menu)
            menu.addItem(.separator())
            menu.addItem(disabledItem("Updated: \(formatTime(snapshot.updatedAt))"))
            menu.addItem(disabledItem("Binary: \(snapshot.claudePath)"))
        } else if let lastError {
            menu.addItem(disabledItem("No usage data"))
            menu.addItem(disabledItem(errorText(lastError)))
        } else {
            menu.addItem(disabledItem("Loading usage data..."))
        }

        menu.addItem(.separator())
        let refreshItem = NSMenuItem(title: isRefreshing ? "Refreshing..." : "Refresh", action: #selector(refreshFromMenu), keyEquivalent: "r")
        refreshItem.target = self
        refreshItem.isEnabled = !isRefreshing
        menu.addItem(refreshItem)

        let openItem = NSMenuItem(title: "Open Claude Usage", action: #selector(openClaudeUsage), keyEquivalent: "")
        openItem.target = self
        menu.addItem(openItem)

        menu.addItem(.separator())
        let quitItem = NSMenuItem(title: "Quit", action: #selector(quit), keyEquivalent: "q")
        quitItem.target = self
        menu.addItem(quitItem)

        statusItem.menu = menu
    }

    private func addLimitItems(_ snapshot: UsageSnapshot, to menu: NSMenu) {
        if let session = snapshot.session {
            menu.addItem(disabledItem("Current session: \(formatPercent(session.usedPercentage)) used"))
            if let reset = session.resetDescription {
                menu.addItem(disabledItem("Resets: \(reset)"))
            }
        } else {
            menu.addItem(disabledItem("Current session: unavailable"))
        }

        if let week = snapshot.weekAllModels {
            menu.addItem(.separator())
            menu.addItem(disabledItem("Week all models: \(formatPercent(week.usedPercentage)) used"))
            if let reset = week.resetDescription {
                menu.addItem(disabledItem("Week resets: \(reset)"))
            }
        }

        if let sonnet = snapshot.weekSonnet {
            menu.addItem(disabledItem("Week Sonnet: \(formatPercent(sonnet.usedPercentage)) used"))
        }
    }

    private func formatMenuBarTitle(_ snapshot: UsageSnapshot) -> String {
        guard let session = snapshot.session else {
            return "Claude --"
        }

        return "Claude \(formatPercent(session.usedPercentage))"
    }

    private func formatPercent(_ value: Double) -> String {
        "\(Int(value.rounded()))%"
    }

    private func formatTime(_ date: Date) -> String {
        let formatter = DateFormatter()
        formatter.timeStyle = .medium
        formatter.dateStyle = .none
        return formatter.string(from: date)
    }

    private func disabledItem(_ title: String) -> NSMenuItem {
        let item = NSMenuItem(title: title, action: nil, keyEquivalent: "")
        item.isEnabled = false
        return item
    }

    private func errorText(_ error: Error) -> String {
        let text = (error as? LocalizedError)?.errorDescription ?? error.localizedDescription
        guard text.count > 120 else {
            return text
        }

        let index = text.index(text.startIndex, offsetBy: 120)
        return String(text[..<index]) + "..."
    }

    @objc private func refreshFromMenu() {
        refresh()
    }

    @objc private func openClaudeUsage() {
        if let url = URL(string: "https://claude.ai/settings/usage") {
            NSWorkspace.shared.open(url)
        }
    }

    @objc private func quit() {
        NSApp.terminate(nil)
    }
}

func runOnce() -> Int32 {
    do {
        let snapshot = try ClaudeUsageFetcher().fetch()
        let session = snapshot.session.map { "\(Int($0.usedPercentage.rounded()))%" } ?? "unavailable"
        let reset = snapshot.session?.resetDescription ?? "unknown reset"
        print("Claude session: \(session) used, resets \(reset)")
        return 0
    } catch {
        fputs("ClaudeQuotaBar: \(error.localizedDescription)\n", stderr)
        return 1
    }
}

if CommandLine.arguments.contains("--once") {
    exit(runOnce())
}

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.run()
