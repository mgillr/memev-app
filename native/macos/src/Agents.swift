// SPDX-License-Identifier: Apache-2.0
// The three screens of native/REV2-SCREENS.md section 5 on macOS: Bots and agents, Connect (5.1), Background agents (5.3), and
// the Import sheet (5.2). Every action runs a command of native/CLI-CONTRACT.md, only after its sheet, off the main thread; the
// core's own answer is shown in the sheet, and the screen is read again afterwards. The ids passed come only from the core.
import AppKit
import UniformTypeIdentifiers

/// A tick box whose change is a closure.
final class TickBox: NSButton {
  var onChange: ((Bool) -> Void)?
  init(on: Bool, enabled: Bool, label: String) {
    super.init(frame: .zero)
    setButtonType(.switch)
    title = ""
    state = on ? .on : .off
    isEnabled = enabled
    setAccessibilityLabel(label)
    translatesAutoresizingMaskIntoConstraints = false
    target = self
    action = #selector(changed)
  }
  required init?(coder: NSCoder) { fatalError() }
  @objc func changed() { onChange?(state == .on) }
}

final class AgentScreens {
  weak var owner: AppDelegate?
  /// The apps ticked on the Connect tab, by id. A found, connectable app starts ticked (5.1).
  var ticked = Set<String>()
  var seen = Set<String>()
  var apps: [AppItem] = []
  var runners: [RunnerRow] = []
  var busy = false
  let connectAction = MButton("Connect apps", .secondary, rank: 1)
  let addAgentsAction = MButton("Add background agents", .secondary, rank: 1)
  let importAction = MButton("Import", .secondary, rank: 1)

  init(owner: AppDelegate) {
    self.owner = owner
    connectAction.onPress = { [weak self] in self?.connectSheet() }
    addAgentsAction.onPress = { [weak self] in self?.addAgentsSheet() }
    importAction.onPress = { [weak self] in self?.importSheet() }
  }

  /// The page actions after Refresh for a screen and tab: the last one shown is the amber one (SH-20).
  func actions(_ id: String, _ tab: String) -> [MButton] {
    if id == "bots" && tab == "connect" { return [connectAction] }
    if id == "bots" && tab == "agents" { return [addAgentsAction] }
    if id == "memory" && tab == "search" { return [importAction] }
    return []
  }

  // MARK: Connect (5.1)

  func fillConnect(_ v: NSStackView, _ d: PageData) {
    guard let o = owner else { return }
    apps = d.apps
    for a in apps where a.connectable && !seen.contains(a.id) { ticked.insert(a.id) }
    seen.formUnion(apps.map { $0.id })
    ticked = ticked.filter { id in apps.contains { $0.id == id && $0.connectable } }
    let list = ListBox()
    if !d.appsRead { list.add(RowView(glyph: nil, title: "The list of AI apps did not answer.")) }
    else if apps.isEmpty { list.add(RowView(glyph: nil, title: "No AI apps were found on this computer.")) }
    for a in apps {
      let tick = TickBox(on: a.connectable ? ticked.contains(a.id) : a.connected, enabled: a.connectable, label: a.name)
      tick.onChange = { [weak self] on in
        if on { self?.ticked.insert(a.id) } else { self?.ticked.remove(a.id) }
        self?.updateConnectAction()
      }
      var button: MButton? = nil
      if a.connected { button = MButton("Disconnect", .secondary) { [weak self] in self?.disconnectSheet(a) } }
      let row = RowView(glyph: nil, title: a.name, sub: a.state, code: a.connectable ? "memev connect \(a.id)" : "", button: button, tech: o.technical, wrap: 520)
      if !a.connectable && !a.connected { tick.isHidden = true }
      list.add(hstack([tick, row], spacing: 8))
    }
    o.put(o.block("AI APPS ON THIS COMPUTER", source: "memev onboarding --json", [list]), in: v)
    if let n = d.counts["notFound"], n > 0 {
      o.put(o.note(n == 1 ? "1 app Memev knows is not found on this computer." : "\(n) apps Memev knows are not found on this computer."), in: v, labelled: false)
    }
    updateConnectAction()
  }

  func updateConnectAction() {
    let n = ticked.count
    connectAction.text = n == 1 ? "Connect 1 app" : "Connect \(n) apps"
    connectAction.isEnabled = n > 0 && !busy
  }

  func connectSheet(onDone: (() -> Void)? = nil) {
    let chosen = apps.filter { ticked.contains($0.id) && $0.connectable && plainId($0.id) }
    guard !chosen.isEmpty, let o = owner else { return }
    let title = chosen.count == 1 ? "Connect 1 AI app?" : "Connect \(chosen.count) AI apps?"
    o.sheet.present(SheetModel(
      title: title,
      note: "Memev adds itself to the settings of each app below and keeps a copy of the old settings first. Each app gets its own seat on the message bus. You can disconnect any of them later.",
      rows: chosen.map { DetailRow(glyph: nil, title: $0.name, sub: "memev connect \($0.id)") },
      source: "memev connect " + chosen.map { $0.id }.joined(separator: " "), main: "Connect",
      onMain: { [weak self] host in
        self?.run(host, doing: "Connecting \(chosen.count == 1 ? chosen[0].name : "\(chosen.count) apps").", args: ["connect"] + chosen.map { $0.id }, timeout: 120000,
                  done: "Connected", failed: "Not every app was connected", onDone: onDone)
      }))
  }

  func disconnectSheet(_ a: AppItem) {
    guard plainId(a.id), let o = owner else { return }
    o.sheet.present(ConfirmKind.disconnect.sheet(a.name) { [weak self] host in
      self?.run(host, doing: "Disconnecting \(a.name).", args: ["disconnect", a.id], timeout: 60000, done: "Disconnected", failed: "\(a.name) was not disconnected")
    })
  }

  // MARK: Background agents (5.3)

  func fillAgents(_ v: NSStackView, _ d: PageData) {
    guard let o = owner else { return }
    runners = d.runners
    let list = ListBox()
    if !d.runnersRead { list.add(RowView(glyph: nil, title: "The background agents did not answer.")) }
    else if runners.isEmpty { list.add(RowView(glyph: nil, title: "No background agents yet.")) }
    for r in runners {
      let sub = r.enabled ? "on \u{00B7} \(r.today) of \(r.maxPerDay) runs today" : "off"
      let stop = r.enabled ? MButton("Stop", .secondary) { [weak self] in self?.stopSheet(r.seat) } : nil
      list.add(RowView(glyph: r.enabled ? .ok : .off, title: r.seat, sub: sub, button: stop))
    }
    o.put(o.block("BACKGROUND AGENTS", source: "memev runner status --json", [list]), in: v)
    o.put(o.note("An agent takes its turns while memev runner watch runs for its app. Nothing keeps that running after a restart yet."), in: v, labelled: false)
  }

  func addAgentsSheet() {
    guard let o = owner else { return }
    let connected = apps.filter { $0.connected && plainId($0.id) }
    if connected.isEmpty {
      o.sheet.present(SheetModel(title: "Add background agents", note: "Connect an AI app first, on the Connect tab. Background agents run as one of your connected apps.", main: "Close"))
      return
    }
    let app = NSPopUpButton(frame: .zero, pullsDown: false)
    app.addItems(withTitles: connected.map { $0.name })
    app.setAccessibilityLabel("App")
    let count = NSTextField(string: "1"); count.setAccessibilityLabel("How many agents"); count.placeholderString = "1 to 20"
    let limit = NSTextField(string: ""); limit.setAccessibilityLabel("Most runs a day"); limit.placeholderString = "required, for example 10"
    let fields: [NSView] = [
      field("App", app), field("How many agents", count), field("Most runs a day", limit),
    ]
    o.sheet.present(SheetModel(
      title: "Add background agents",
      note: "Each agent takes one unattended turn when the lead posts it a task, until the day's limit. Each runs as the app you choose, with its own seat.",
      fields: fields, source: "memev runner on <app> --agents N --max-runs-per-day M", main: "Start",
      onMain: { [weak self] host in
        guard let n = Int(count.stringValue.trimmingCharacters(in: .whitespaces)), (1...20).contains(n) else { host.status.stringValue = "How many agents: a whole number from 1 to 20."; return }
        guard let m = Int(limit.stringValue.trimmingCharacters(in: .whitespaces)), m >= 1 else { host.status.stringValue = "Most runs a day is required: a whole number of 1 or more. Nothing was started."; return }
        let i = app.indexOfSelectedItem
        guard i >= 0 && i < connected.count else { return }
        self?.run(host, doing: "Starting \(n == 1 ? "1 agent" : "\(n) agents") for \(connected[i].name).",
                  args: ["runner", "on", connected[i].id, "--agents", String(n), "--max-runs-per-day", String(m)], timeout: 60000,
                  done: "Background agents on", failed: "The background agents did not start")
      }))
  }

  func field(_ label: String, _ control: NSView) -> NSView {
    control.translatesAutoresizingMaskIntoConstraints = false
    control.widthAnchor.constraint(equalToConstant: 280).isActive = true
    return vstack([Label(label, .smallMedium, .sub), control], spacing: 6)
  }

  func stopSheet(_ seat: String) {
    guard let o = owner else { return }
    o.sheet.present(ConfirmKind.stopagents.sheet(seat) { [weak self] host in
      self?.run(host, doing: "Stopping \(seat).", args: ["runner", "off", seat], timeout: 30000, done: "Stopped", failed: "\(seat) was not stopped")
    })
  }

  // MARK: Import (5.2)

  func importSheet() {
    guard let o = owner else { return }
    let ai = RowView(glyph: nil, title: "AI app history on this computer", sub: "previewed before anything is kept",
                     button: MButton("Preview", .secondary) { [weak self] in self?.preview(["--ai-history"], "the AI app history on this computer") })
    let export = RowView(glyph: nil, title: "A chat export (.zip)", sub: "from ChatGPT or Claude: their export file",
                         button: MButton("Choose", .secondary) { [weak self] in self?.chooseExport() })
    let earlier = RowView(glyph: nil, title: "Earlier imports", sub: "each can be undone",
                          button: MButton("List", .secondary) { [weak self] in self?.listImports() })
    o.sheet.present(SheetModel(
      title: "Import",
      note: "Everything is previewed before anything is kept, and each import can be undone. Memev stops for a moment while it looks, then starts again.",
      fields: [ai, export, earlier], source: "memev import --ai-history --preview \u{00B7} memev import --export FILE.zip --preview \u{00B7} memev import list", main: "Close"))
  }

  func chooseExport() {
    let p = NSOpenPanel()
    p.allowedContentTypes = [UTType.zip]
    p.allowsMultipleSelection = false
    p.canChooseDirectories = false
    p.title = "Choose a ChatGPT or Claude export"
    guard p.runModal() == .OK, let url = p.url else { return }
    let f = url.path
    // the core takes the path as one argument with no shell; a control character or a leading dash is still refused
    if f.hasPrefix("-") || f.unicodeScalars.contains(where: { $0.value < 0x20 || $0.value == 0x7f }) {
      owner?.sheet.status.stringValue = "That file's name cannot be passed to the core. Copy it to a plain name, such as export.zip, and choose that."
      return
    }
    preview(["--export", f], "that export")
  }

  func preview(_ what: [String], _ label: String) {
    guard let host = owner?.sheet else { return }
    runThen(host, doing: "Looking at \(label). Memev stops for a moment and starts again.", args: ["import"] + what + ["--preview"], timeout: 180000) { [weak self] code, answer in
      guard let self = self, let o = self.owner else { return }
      let nothing = code != 0 || answer.range(of: #"^0 item"#, options: .regularExpression) != nil || answer.contains("\n0 item")
      o.sheet.present(SheetModel(
        title: code != 0 ? "The preview did not finish" : nothing ? "Nothing to bring in" : "Keep these items?",
        note: self.cut(answer), source: "memev import ... --preview", main: nothing ? "Close" : "Import",
        onMain: { [weak self] host in
          if nothing { host.dismiss(); return }
          self?.run(host, doing: "Importing. Memev stops for a moment and starts again.", args: ["import"] + what + ["--yes"], timeout: 600000,
                    done: "Imported", failed: "The import did not finish")
        }))
    }
  }

  func listImports() {
    guard let host = owner?.sheet else { return }
    runThen(host, doing: "Reading the earlier imports. Memev stops for a moment and starts again.", args: ["import", "list"], timeout: 60000) { [weak self] code, answer in
      guard let self = self, let o = self.owner else { return }
      let ids = answer.split(separator: "\n").compactMap { line -> (String, String)? in
        let t = line.trimmingCharacters(in: .whitespaces)
        guard let first = t.split(separator: " ").first.map(String.init), plainId(first), first.hasPrefix("import-") else { return nil }
        return (first, showPath(String(t.dropFirst(first.count)).trimmingCharacters(in: .whitespaces)))
      }
      let rows: [NSView] = ids.map { (id, rest) in
        RowView(glyph: nil, title: id, sub: rest, button: MButton("Undo", .secondary) { [weak self] in self?.undoSheet(id) })
      }
      o.sheet.present(SheetModel(
        title: "Earlier imports",
        note: code != 0 ? "The list did not answer." : ids.isEmpty ? "No imports yet." : "Undo takes everything that import brought in out of your memory.",
        fields: rows, source: "memev import list", main: "Close"))
    }
  }

  func undoSheet(_ id: String) {
    guard plainId(id), let o = owner else { return }
    o.sheet.present(ConfirmKind.undoimport.sheet(id) { [weak self] host in
      self?.run(host, doing: "Undoing the import.", args: ["import", "undo", id], timeout: 120000, done: "Import undone", failed: "The import was not undone")
    })
  }

  // MARK: Running

  /// Runs one command off the main thread for a sheet, then shows the core's answer and reads the screen again.
  func run(_ host: SheetHost, doing: String, args: [String], timeout: Int, done: String, failed: String, onDone: (() -> Void)? = nil) {
    runThen(host, doing: doing, args: args, timeout: timeout) { [weak self] code, answer in
      guard let self = self, let o = self.owner else { return }
      let title = code == 0 ? done : code == -1 ? "Memev did not answer in time" : failed
      o.sheet.present(SheetModel(title: title, note: self.cut(answer).isEmpty ? (code == 0 ? "Done." : "The core gave no reason.") : self.cut(answer),
                                 source: "memev " + args.joined(separator: " "), main: onDone == nil ? "Close" : "Continue",
                                 onMain: { host in host.dismiss(); onDone?() }))
      if ["bots", "memory"].contains(o.current) { o.loadPage(o.current) }
    }
  }

  func runThen(_ host: SheetHost, doing: String, args: [String], timeout: Int, then: @escaping (Int, String) -> Void) {
    if busy { return }
    busy = true
    host.status.stringValue = doing
    host.mainButton?.isEnabled = false
    updateConnectAction()
    DispatchQueue.global(qos: .userInitiated).async {
      let (code, out) = memevRun(args, timeout: timeout)
      DispatchQueue.main.async {
        self.busy = false
        self.updateConnectAction()
        then(code, out)
      }
    }
  }

  /// The core's answer for a sheet: the person's paths shortened, at most 30 lines.
  func cut(_ s: String) -> String {
    let lines = showPath(s).split(separator: "\n", omittingEmptySubsequences: false).map(String.init)
    let kept = lines.prefix(30).joined(separator: "\n").trimmingCharacters(in: .whitespacesAndNewlines)
    return lines.count > 30 ? kept + "\n\u{2026}" : kept
  }
}
