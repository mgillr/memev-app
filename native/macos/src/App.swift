// SPDX-License-Identifier: Apache-2.0
// The rev2 shell on macOS (native/REV2-SCREENS.md section 1): the sidebar with the health button and its pips, the menu, Help and
// support and the foot line; the top bar; the stopped banner; each screen's header, tabs and content; the detail panel; sheets.
// It reads the core through the shared reader (native/linux/src/read.c) and the shim's command runner. It writes nothing to the
// notes and sends nothing off this computer.
import AppKit

/// The health button (SH-2): a glyph and the core's health sentence, then the pip strip. Its title is that sentence.
final class HealthButton: ClickArea, Themed {
  let glyph = Glyph(.ok)
  let sentence = Label("Reading Memev.", .body, .text, wrap: 160)
  let pips = hstack([], spacing: 4)
  var open = false { didSet { applyTheme() } }

  override init(frame: NSRect) {
    super.init(frame: frame)
    let line = hstack([glyph, sentence], spacing: 8)
    line.alignment = .firstBaseline
    hold(vstack([line, pips], spacing: 8), NSEdgeInsets(top: 8, left: 12, bottom: 8, right: 12))
    applyTheme()
  }

  required init?(coder: NSCoder) { fatalError() }

  func applyTheme() {
    layer?.cornerRadius = 8
    layer?.backgroundColor = open ? Role.raise.color.cgColor : NSColor.clear.cgColor
  }
}

/// The window's root view; it repaints when the computer's theme changes.
final class RootView: NSView {
  var onAppearance: (() -> Void)?
  override func viewDidChangeEffectiveAppearance() {
    super.viewDidChangeEffectiveAppearance()
    onAppearance?()
  }
}

/// One screen's view: its header and its content, kept while the window is open.
final class ScreenView {
  let root = vstack([], spacing: 40)
  let header = PageHeader()
  let content = vstack([], spacing: 0)
  let refresh: MButton
  var tabViews: [String: NSView] = [:]

  init(refresh: MButton) {
    self.refresh = refresh
    addFull(header, to: root)
    addFull(content, to: root)
  }
}

struct Pip {
  let name: String
  let kind: GlyphKind
  let state: String // fine, needs, fault, off, unread
  let tip: String
}

final class AppDelegate: NSObject, NSApplicationDelegate {
  var window: NSWindow!
  var glance = memev_shim_new()!
  var loading = false
  // Each of the first reads says when its answer is in; until then its part of the window says it is reading.
  var glanceRead = false
  let firstReads = DispatchGroup()
  var readsDone = 0
  let launched = Date()
  var shownMs = -1
  var starting = false
  /// Told the core's answer to a start, once the window has read the core again (used by the self-check).
  var onStartDone: ((Int) -> Void)?
  var selfCheck = ProcessInfo.processInfo.environment["MEMEV_SELF_CHECK"] != nil
  var attentionRows = 0
  var checkRows = 0
  var palette: Theme { Theme.current }

  // What the core said, beyond the reader's glance.
  var doctor: [[String: Any]] = []
  var roster: [String: Any]?
  var version = ""
  var planLabel = ""
  var projectName = ""
  var pageData: [String: PageData] = [:]
  var pageLoad = 0

  // Navigation.
  var current = "overview"
  var currentPage = -1
  var tabFor: [String: String] = [:]
  var inGuide = false
  var technical = UserDefaults.standard.bool(forKey: "technicalDetail")
  var appearance = UserDefaults.standard.string(forKey: "appearance") ?? "system"

  // The shell.
  let root = RootView()
  let sidebar = Box(.panel)
  var sidebarWidth: NSLayoutConstraint!
  let healthButton = HealthButton()
  var navButtons: [NavButton] = []
  var projectButton: NavButton!
  var helpButton: NavButton!
  let foot = Label("", .small, .dim)
  let crumbLink = MButton("Projects", .link)
  let crumbSep = Label("›", .body, .sub)
  let crumb = Label("Overview", .body, .text)
  let techChip = Box(.raise, radius: 4)
  let statusLine = Label("", .body, .sub)
  let command = NSTextField(string: "")
  let banner = Box(.raise)
  var bannerStart: MButton!
  let pageStack = vstack([], spacing: 0, insets: NSEdgeInsets(top: 32, left: 36, bottom: 64, right: 36))
  let page = FlippedView()
  let scroll = NSScrollView()
  let detail = DetailPanel()
  let detailEdge = hairline(.rule, vertical: true)
  let sheet = SheetHost()
  var selectedRow: RowView?
  var screens: [String: ScreenView] = [:]
  /// Everything but the sheet: the amber rule for the window runs here, and a sheet keeps its own.
  let shellRow = hstack([], spacing: 0)

  // Kept for the guided first launch (Guide.swift) and the self-check.
  var pageTitle: Label { screen("guide").header.title }
  let primary = MButton("Continue", .secondary, rank: 1)
  var guide: GuideController!
  let search = NSSearchField()
  var refreshItem: NSMenuItem!
  var overviewSentence: Label { screen("overview").header.sentence }

  func applicationDidFinishLaunching(_ note: Notification) {
    applyAppearance()
    buildMenu()
    buildWindow()
    // The window shows at once, each part saying it is reading; the core is read off the main thread and each part fills when
    // its answer is in. A slow or stuck core no longer leaves a blank window.
    paint()
    fill()
    if memev_shim_want_guide() { guide.show(0) } else { showPage(0) }
    window.makeKeyAndOrderFront(nil)
    NSApp.activate(ignoringOtherApps: true)
    shownMs = Int(Date().timeIntervalSince(launched) * 1000)
    readFirst()
    if selfCheck {
      if let want = ProcessInfo.processInfo.environment["MEMEV_SELF_CHECK_PAGE"], let i = NAV.firstIndex(where: { $0.id == want }) {
        search.stringValue = ProcessInfo.processInfo.environment["MEMEV_SELF_CHECK_QUERY"] ?? ""
        showPage(i)
        return
      }
      // The self-check waits for every first read, so what it prints does not depend on which answer came first.
      firstReads.notify(queue: .main) {
        if ProcessInfo.processInfo.environment["MEMEV_SELF_CHECK_START"] != nil { self.checkStart() } else { self.reportAndQuit() }
      }
      return
    }
    firstReads.notify(queue: .main) {
      var ms = Int(ProcessInfo.processInfo.environment["MEMEV_POLL_MS"] ?? "") ?? 5000
      if ms < 200 { ms = 200 }
      Timer.scheduledTimer(withTimeInterval: Double(ms) / 1000, repeats: true) { [weak self] _ in self?.reload() }
    }
  }

  /// One read of the core off the main thread; done runs on the main thread with its answer.
  func readOff<T>(_ work: @escaping () -> T, _ done: @escaping (T) -> Void) {
    firstReads.enter()
    DispatchQueue.global(qos: .userInitiated).async {
      let v = work()
      DispatchQueue.main.async {
        done(v)
        self.readsDone += 1
        self.firstReads.leave()
      }
    }
  }

  /// The six first reads: the glance (status and doctor through the shared reader), status, licence and plan, card, doctor
  /// and the roster. Each fills its own part of the window when it returns.
  func readFirst() {
    loading = true
    firstReads.notify(queue: .main) { self.loading = false }
    readOff({ () -> UnsafeMutablePointer<MemevGlance> in let g = memev_shim_new()!; memev_shim_load(g); return g }) { g in
      let old = self.glance
      self.glance = g
      memev_shim_free(old)
      self.glanceRead = true
      self.fill()
    }
    readOff({ memevJSON(["status", "--json"]) as? [String: Any] }) { s in
      self.version = str(s?["version"])
      self.fill()
    }
    readOff({ (memevJSON(["license", "status", "--json"]) as? [String: Any], memevJSON(["plan", "--json"]) as? [String: Any]) }) { lp in
      self.planLabel = str(lp.0?["tier_label"]).isEmpty ? str(lp.1?["plan"]) : str(lp.0?["tier_label"])
      self.fill()
    }
    readOff({ memevJSON(["card", "--json"]) as? [String: Any] }) { c in
      let p = str(c?["project"])
      self.projectName = p.contains("/") ? "" : p
      self.fill()
    }
    readOff({ AppDelegate.readDoctor() }) { d in
      self.doctor = d
      self.fill()
    }
    readOff({ memevJSON(["bus", "roster", "--json"]) as? [String: Any] }) { r in
      self.roster = r
      self.fill()
    }
  }

  func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }

  // MARK: Theme

  /// MEMEV_THEME forces a theme; otherwise Settings, General, Appearance; otherwise the computer's theme (SH-24).
  func applyAppearance() {
    switch ProcessInfo.processInfo.environment["MEMEV_THEME"] ?? appearance {
    case "dark": NSApp.appearance = NSAppearance(named: .darkAqua)
    case "light": NSApp.appearance = NSAppearance(named: .aqua)
    default: NSApp.appearance = nil
    }
  }

  func paint() {
    let dark = (window?.effectiveAppearance ?? NSApp.effectiveAppearance).bestMatch(from: [.darkAqua, .aqua]) == .darkAqua
    Theme.current = dark ? .darkTokens : .lightTokens
    window?.backgroundColor = Theme.current.page
    root.layer?.backgroundColor = Theme.current.page.cgColor
    command.textColor = Role.text.color
    repaintTree(root)
  }

  // MARK: Menu and window

  func buildMenu() {
    let main = NSMenu()
    let appItem = NSMenuItem()
    main.addItem(appItem)
    let appMenu = NSMenu()
    appMenu.addItem(NSMenuItem(title: "Quit Memev", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q"))
    appItem.submenu = appMenu
    let viewItem = NSMenuItem()
    main.addItem(viewItem)
    let viewMenu = NSMenu(title: "View")
    refreshItem = NSMenuItem(title: "Refresh", action: #selector(refreshClicked(_:)), keyEquivalent: "r")
    refreshItem.keyEquivalentModifierMask = [.command]
    refreshItem.target = self
    viewMenu.addItem(refreshItem)
    let go = NSMenuItem(title: "Search or run a command", action: #selector(focusCommand(_:)), keyEquivalent: "k")
    go.keyEquivalentModifierMask = [.command]
    go.target = self
    viewMenu.addItem(go)
    viewItem.submenu = viewMenu
    NSApp.mainMenu = main
  }

  /// Kept for the guide: a label in the design's font at a size.
  func label(_ f: NSTextField, size: CGFloat, weight: NSFont.Weight = .regular) {
    f.font = Fonts.sans(size, weight)
    f.isSelectable = false
    f.translatesAutoresizingMaskIntoConstraints = false
  }

  /// Kept for the guide: a plain wrapping line in the body style.
  func plain(_ text: String) -> NSTextField { Label(text, .body, .text, wrap: 600) }

  func buildWindow() {
    window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 1280, height: 800), styleMask: [.titled, .closable, .miniaturizable, .resizable],
                      backing: .buffered, defer: false)
    window.title = "Memev"
    window.contentMinSize = NSSize(width: 1024, height: 700)
    window.center()
    root.wantsLayer = true
    root.onAppearance = { [weak self] in self?.paint() }
    window.contentView = root

    buildSidebar()
    let main = buildMain()
    detail.isHidden = true
    detailEdge.isHidden = true
    detail.onClose = { [weak self] in self?.closeDetail() }

    let row = shellRow
    for v in [sidebar, hairline(.rule, vertical: true), main, detailEdge, detail] as [NSView] { row.addArrangedSubview(v) }
    row.alignment = .top
    row.distribution = .fill
    root.addSubview(row)
    root.addSubview(sheet)
    for v in [sidebar, main, detail] as [NSView] { v.heightAnchor.constraint(equalTo: row.heightAnchor).isActive = true }
    for v in row.arrangedSubviews where v !== sidebar && v !== main && v !== detail { v.heightAnchor.constraint(equalTo: row.heightAnchor).isActive = true }
    main.setContentHuggingPriority(.init(1), for: .horizontal)
    NSLayoutConstraint.activate([
      row.leadingAnchor.constraint(equalTo: root.leadingAnchor), row.trailingAnchor.constraint(equalTo: root.trailingAnchor),
      row.topAnchor.constraint(equalTo: root.topAnchor), row.bottomAnchor.constraint(equalTo: root.bottomAnchor),
      sheet.leadingAnchor.constraint(equalTo: root.leadingAnchor), sheet.trailingAnchor.constraint(equalTo: root.trailingAnchor),
      sheet.topAnchor.constraint(equalTo: root.topAnchor), sheet.bottomAnchor.constraint(equalTo: root.bottomAnchor),
    ])

    guide = GuideController(owner: self)
    primary.onPress = { [weak self] in self?.refreshClicked(nil) }
    _ = screen("overview")
    _ = screen("health")
    _ = screen("guide")

    // Esc closes a sheet, then the detail panel (SH-21, 1.16).
    NSEvent.addLocalMonitorForEvents(matching: .keyDown) { [weak self] e in
      guard let me = self, e.keyCode == 53 else { return e }
      if me.sheet.isOpen { me.sheet.dismiss(); return nil }
      if !me.detail.isHidden { me.closeDetail(); return nil }
      return e
    }
  }

  func buildSidebar() {
    let stack = vstack([], spacing: 4, insets: NSEdgeInsets(top: 20, left: 16, bottom: 16, right: 16))
    sidebar.addSubview(stack)
    sidebarWidth = sidebar.widthAnchor.constraint(equalToConstant: 240)
    NSLayoutConstraint.activate([
      sidebarWidth,
      stack.leadingAnchor.constraint(equalTo: sidebar.leadingAnchor), stack.trailingAnchor.constraint(equalTo: sidebar.trailingAnchor),
      stack.topAnchor.constraint(equalTo: sidebar.topAnchor), stack.bottomAnchor.constraint(equalTo: sidebar.bottomAnchor),
    ])
    let wordmark = hstack([Mark(), Label("Memev", .wordmark, .text)], spacing: 8)
    stack.addArrangedSubview(wordmark)
    stack.setCustomSpacing(16, after: wordmark)
    healthButton.onPress = { [weak self] in self?.open("health") }
    addFull(healthButton, to: stack)
    stack.setCustomSpacing(16, after: healthButton)
    for (i, item) in NAV.enumerated() {
      if item.id == "settings" {
        let d = hairline(.rule)
        addFull(d, to: stack)
        stack.setCustomSpacing(8, after: stack.arrangedSubviews[stack.arrangedSubviews.count - 2])
        stack.setCustomSpacing(8, after: d)
      }
      let b = NavButton(title: item.menu, target: self, action: #selector(navClicked(_:)))
      b.tag = i
      navButtons.append(b)
      addFull(b, to: stack)
      if item.id == "projects" {
        projectButton = NavButton(title: "", target: self, action: #selector(projectClicked(_:)))
        projectButton.indent = 28
        projectButton.isHidden = true
        addFull(projectButton, to: stack)
      }
    }
    let gap = spacer()
    stack.addArrangedSubview(gap)
    helpButton = NavButton(title: "Help and support", target: self, action: #selector(helpClicked(_:)))
    addFull(helpButton, to: stack)
    stack.setCustomSpacing(12, after: helpButton)
    stack.addArrangedSubview(foot)
  }

  func buildMain() -> NSView {
    let top = Box(.panel)
    top.heightAnchor.constraint(equalToConstant: 48).isActive = true
    crumbLink.onPress = { [weak self] in self?.open("projects") }
    let chipText = Label("Technical detail on", .smallMedium, .sub)
    techChip.addSubview(chipText)
    NSLayoutConstraint.activate([
      techChip.heightAnchor.constraint(equalToConstant: 20),
      chipText.leadingAnchor.constraint(equalTo: techChip.leadingAnchor, constant: 8), chipText.trailingAnchor.constraint(equalTo: techChip.trailingAnchor, constant: -8),
      chipText.centerYAnchor.constraint(equalTo: techChip.centerYAnchor),
    ])
    statusLine.setAccessibilityRole(.staticText)
    statusLine.setAccessibilityLabel("Status")

    // SH-9: the command field, 280 by 32, with ⌘K at its right.
    let field = Box(.page, border: .ctl, radius: 8)
    command.isBordered = false
    command.drawsBackground = false
    command.focusRingType = .none
    command.font = TypeStyle.body.font
    command.placeholderString = "Search or run a command"
    command.target = self
    command.action = #selector(commandEntered(_:))
    command.translatesAutoresizingMaskIntoConstraints = false
    command.setAccessibilityLabel("Search or run a command")
    let key = Label("⌘K", .small, .dim)
    field.addSubview(command)
    field.addSubview(key)
    NSLayoutConstraint.activate([
      field.widthAnchor.constraint(equalToConstant: 280), field.heightAnchor.constraint(equalToConstant: 32),
      command.leadingAnchor.constraint(equalTo: field.leadingAnchor, constant: 10), command.centerYAnchor.constraint(equalTo: field.centerYAnchor),
      command.trailingAnchor.constraint(equalTo: key.leadingAnchor, constant: -8),
      key.trailingAnchor.constraint(equalTo: field.trailingAnchor, constant: -10), key.centerYAnchor.constraint(equalTo: field.centerYAnchor),
    ])
    let bar = hstack([crumbLink, crumbSep, crumb, techChip, statusLine, spacer(), field], spacing: 8)
    bar.setCustomSpacing(16, after: crumb)
    bar.setCustomSpacing(16, after: techChip)
    top.addSubview(bar)
    NSLayoutConstraint.activate([
      bar.leadingAnchor.constraint(equalTo: top.leadingAnchor, constant: 36), bar.trailingAnchor.constraint(equalTo: top.trailingAnchor, constant: -36),
      bar.centerYAnchor.constraint(equalTo: top.centerYAnchor),
    ])

    // SH-10: the stopped banner.
    let seeWhy = MButton("See why it stopped", .secondary) { [weak self] in self?.open("health") }
    bannerStart = MButton("Start Memev", .secondary, rank: 3) { [weak self] in self?.startSheet() }
    let words = vstack([Label("Memev is stopped.", .bodyMedium, .text),
                        Label("Your AI apps cannot reach their shared memory. Nothing is lost.", .body, .sub)], spacing: 2)
    let bannerRow = hstack([Glyph(.bad), words, spacer(), seeWhy, bannerStart], spacing: 12)
    banner.addSubview(bannerRow)
    NSLayoutConstraint.activate([
      bannerRow.leadingAnchor.constraint(equalTo: banner.leadingAnchor, constant: 36), bannerRow.trailingAnchor.constraint(equalTo: banner.trailingAnchor, constant: -36),
      bannerRow.topAnchor.constraint(equalTo: banner.topAnchor, constant: 12), bannerRow.bottomAnchor.constraint(equalTo: banner.bottomAnchor, constant: -12),
    ])
    banner.isHidden = true

    page.translatesAutoresizingMaskIntoConstraints = false
    page.addSubview(pageStack)
    scroll.hasVerticalScroller = true
    scroll.hasHorizontalScroller = false
    scroll.drawsBackground = false
    scroll.documentView = page
    scroll.translatesAutoresizingMaskIntoConstraints = false
    scroll.setContentHuggingPriority(.init(1), for: .vertical)
    NSLayoutConstraint.activate([
      page.leadingAnchor.constraint(equalTo: scroll.contentView.leadingAnchor), page.trailingAnchor.constraint(equalTo: scroll.contentView.trailingAnchor),
      page.topAnchor.constraint(equalTo: scroll.contentView.topAnchor),
      pageStack.leadingAnchor.constraint(equalTo: page.leadingAnchor), pageStack.trailingAnchor.constraint(equalTo: page.trailingAnchor),
      pageStack.topAnchor.constraint(equalTo: page.topAnchor), pageStack.bottomAnchor.constraint(equalTo: page.bottomAnchor),
    ])

    search.placeholderString = "Search your notes"
    search.font = TypeStyle.body.font
    search.target = self
    search.action = #selector(searchChanged(_:))
    search.sendsWholeSearchString = true
    search.translatesAutoresizingMaskIntoConstraints = false
    search.widthAnchor.constraint(equalToConstant: 420).isActive = true

    let col = vstack([], spacing: 0)
    for v in [top, hairline(.rule), banner, scroll] as [NSView] { addFull(v, to: col) }
    return col
  }

  /// A screen's view, made once and kept. The guide's screen holds the guide's steps and its one button.
  func screen(_ id: String) -> ScreenView {
    if let s = screens[id] { return s }
    let refresh = id == "guide" ? primary : MButton("Refresh", .secondary, rank: 1) { [weak self] in self?.refreshClicked(nil) }
    let s = ScreenView(refresh: refresh)
    s.header.what.onPress = { [weak self] in self?.openWhat() }
    s.header.tabs.onSelect = { [weak self] t in self?.selectTab(t) }
    s.root.isHidden = true
    screens[id] = s
    addFull(s.root, to: pageStack)
    return s
  }

  func def(_ id: String) -> ScreenDef {
    if id == "health" { return HEALTH }
    if id == "project" { return projectDef(projectName) }
    if id == "guide" { return ScreenDef(id: "guide", menu: "", title: "", tabs: [], sentences: [:], words: []) }
    return NAV.first { $0.id == id } ?? NAV[0]
  }

  // MARK: The core's answers

  var stopped: Bool { memev_shim_have_binary(glance) && !memev_shim_daemon_up(glance) }

  func sentences() -> (String, String) {
    if !glanceRead { return ("Reading Memev.", "Reading Memev.") }
    var h = [CChar](repeating: 0, count: 256)
    var o = [CChar](repeating: 0, count: 512)
    memev_shim_health(glance, &h, Int32(h.count))
    memev_shim_overview(glance, &o, Int32(o.count))
    return (String(cString: h), String(cString: o))
  }

  /// The doctor's checks with their ids, detail and fix, for the pips and the detail panel. The doctor prints an array, or an
  /// object with a checks array.
  static func readDoctor() -> [[String: Any]] {
    let j = memevJSON(["doctor", "--json"])
    if let a = j as? [[String: Any]] { return a }
    if let o = j as? [String: Any], let a = o["checks"] as? [[String: Any]] { return a }
    return []
  }

  func doctorEntry(_ title: String) -> [String: Any]? {
    doctor.first { str($0["title"]) == title } ?? doctor.first { str($0["id"]) == title }
  }

  static func kind(_ status: String) -> GlyphKind {
    switch status {
    case "ok": return .ok
    case "warn": return .warn
    case "fail": return .bad
    default: return .off
    }
  }

  /// The 14 pips of 1.2, in order. A pip whose source the window does not read is hollow and says so in its title.
  func pips() -> [Pip] {
    func byID(_ id: String) -> [String: Any]? { doctor.first { str($0["id"]) == id } }
    func pip(_ name: String, _ id: String) -> Pip {
      guard let c = byID(id) else { return Pip(name: name, kind: .off, state: "unread", tip: "\(name) · not read yet") }
      let st = str(c["status"])
      let k = AppDelegate.kind(st)
      return Pip(name: name, kind: k, state: k == .ok ? "fine" : k == .warn ? "needs" : k == .bad ? "fault" : "unread", tip: "\(name) · \(safe(str(c["detail"])))")
    }
    var out: [Pip] = []
    if let seats = roster?["seats"] as? [[String: Any]] {
      let named = seats.filter { !str($0["seat"]).isEmpty }
      let live = named.filter { str($0["state"]) == "live" }.count
      let quiet = named.filter { str($0["state"]) == "stale" }.map { str($0["seat"]) }
      if let q = quiet.first {
        out.append(Pip(name: "Bots", kind: .warn, state: "needs", tip: "\(q) quiet · \(live) of \(named.count) live"))
      } else {
        out.append(Pip(name: "Bots", kind: .ok, state: "fine", tip: "Bots · \(live) of \(named.count) live"))
      }
    } else {
      out.append(Pip(name: "Bots", kind: .off, state: "unread", tip: "Bots · not read yet"))
    }
    out.append(pip("Memory on", "vault"))
    if byID("daemon") != nil {
      out.append(pip("Running", "daemon"))
    } else if memev_shim_have_binary(glance) {
      // The doctor did not return its daemon check: the core's status answer says whether Memev is running.
      let up = memev_shim_daemon_up(glance)
      out.append(Pip(name: "Running", kind: up ? .ok : .bad, state: up ? "fine" : "fault", tip: up ? "Running" : "Not running"))
    } else {
      out.append(Pip(name: "Running", kind: .off, state: "unread", tip: "Running · not read yet"))
    }
    out.append(pip("History", "oplog"))
    out.append(pip("Search", "index"))
    out.append(pip("Messages", "bus"))
    out.append(pip("Lead set", "bus-lead"))
    out.append(pip("Backup", "backup"))
    out.append(pip("Plan", "license"))
    out.append(pip("Notes safe", "secrets"))
    out.append(Pip(name: "Shared", kind: .off, state: "unread", tip: "Shared · not read yet: the core's ready check is not in this window"))
    out.append(Pip(name: "Checks on", kind: .off, state: "unread", tip: "Checks on · not read yet: the core's contracts status is not in this window"))
    out.append(pip("Update", "update"))
    if let r = byID("remote") {
      let off = str(r["status"]) == "ok" && str(r["detail"]).lowercased().contains("off")
      out.append(off ? Pip(name: "Web off", kind: .off, state: "off", tip: "Web off") : pip("Web access", "remote"))
    } else {
      out.append(Pip(name: "Web access", kind: .off, state: "unread", tip: "Web access · not read yet"))
    }
    return out
  }

  var quietBots: Int {
    ((roster?["seats"] as? [[String: Any]]) ?? []).filter { !str($0["seat"]).isEmpty && str($0["state"]) == "stale" }.count
  }

  /// Fills everything that comes from the glance and the doctor: the health button, the Overview and Health screens.
  func fill() {
    let (health, overview) = sentences()
    healthButton.title = health
    healthButton.sentence.stringValue = health
    let ps = pips()
    clear(healthButton.pips)
    for p in ps {
      let g = Glyph(p.kind, size: 6, pip: true)
      g.toolTip = p.tip
      healthButton.pips.addArrangedSubview(g)
    }
    let fine = ps.filter { $0.state == "fine" }.count
    let off = ps.filter { $0.state == "off" }.count
    let look = Int(memev_shim_look(glance))
    healthButton.glyph.kind = !glanceRead ? .off : stopped ? .bad : look > 0 ? .warn : .ok
    healthButton.setAccessibilityLabel("\(health) \(fine) fine, \(off) off by choice. Open Health.")
    navButtons[3].needsYou = quietBots > 0 && !stopped
    let foots = [planLabel, version.isEmpty ? "" : "v\(version)"].filter { !$0.isEmpty }
    foot.stringValue = foots.joined(separator: " · ")
    projectButton.text = projectName
    projectButton.isHidden = projectName.isEmpty

    let ov = screen("overview")
    ov.header.sentence.stringValue = overview
    buildOverview(ov)
    let he = screen("health")
    he.header.sentence.stringValue = health
    buildHealth(he)
    render()
  }

  // MARK: Building blocks

  /// A labelled block: the section label, the command when technical detail is on, then the body.
  func block(_ label: String, sample: Bool = false, source: String = "", link: MButton? = nil, _ body: [NSView]) -> NSStackView {
    let b = vstack([], spacing: 12)
    addFull(SectionLabel(label, sample: sample, link: link), to: b)
    if technical && !source.isEmpty { b.addArrangedSubview(Label(source, .code, .dim)) }
    for v in body { addFull(v, to: b) }
    return b
  }

  /// Adds a block to a screen's content with the spacing of 1.1: 48 px before a labelled section, 24 before an unlabelled one.
  func put(_ v: NSView, in s: NSStackView, labelled: Bool = true) {
    if let last = s.arrangedSubviews.last { s.setCustomSpacing(labelled ? 48 : 24, after: last) }
    addFull(v, to: s)
  }

  func note(_ text: String) -> Label { Label(text, .body, .sub, wrap: 640) }

  func laterBlock(_ key: String) -> NSView? {
    guard let items = LATER[key], !items.isEmpty else { return nil }
    return comingLater(items) { [weak self] it, row in
      self?.openDetail(DetailModel(key: "later:\(it.title)", title: it.title, sample: true, why: it.why, plan: it.plan, source: it.source), from: row)
    }
  }

  func checkRow(_ title: String, _ status: String, button: MButton? = nil) -> RowView {
    let e = doctorEntry(title)
    let detailText = safe(str(e?["detail"]))
    let r = RowView(glyph: AppDelegate.kind(status), title: title, sub: detailText, code: technical ? "memev doctor --json · \(str(e?["id"]))" : "",
                    button: button, tech: technical)
    r.onPress = { [weak self, weak r] in
      guard let me = self, let r = r else { return }
      var facts = [DetailFact(name: "Check", value: title)]
      let fix = safe(str(e?["fix"]))
      if !fix.isEmpty { facts.append(DetailFact(name: "To fix", value: fix)) }
      let word = status == "ok" ? "Fine" : status == "fail" ? "Failed" : "Needs a look"
      me.openDetail(DetailModel(key: "check:\(title)", title: title, state: (AppDelegate.kind(status), word),
                                why: detailText.isEmpty ? "The core gave no more detail for this check." : detailText,
                                facts: facts, source: "memev doctor --json"), from: r)
    }
    return r
  }

  func buildOverview(_ s: ScreenView) {
    clear(s.content)
    attentionRows = 0
    let star = String(cString: memev_shim_north_star(glance))
    if !star.isEmpty { put(Label(star, .bodyMedium, .text, wrap: 640), in: s.content, labelled: false) }
    let up = memev_shim_daemon_up(glance)
    let shown = Int(memev_shim_check_shown(glance))
    let list = ListBox()
    var rows: [RowView] = []
    if !glanceRead {
      list.add(RowView(glyph: nil, title: "Reading Memev."))
    } else if !up {
      list.add(RowView(glyph: stopped ? .bad : .off, title: "Nothing to list while Memev is not running."))
    } else if memev_shim_look(glance) == 0 {
      list.add(RowView(glyph: .ok, title: "All clear.", sub: "Nothing needs you. Every check the core returned is fine."))
    } else {
      for i in 0..<shown {
        let status = String(cString: memev_shim_check_status(glance, Int32(i)))
        if status == "ok" { continue }
        let title = String(cString: memev_shim_check_title(glance, Int32(i)))
        // SH-20: a row's "See why" may be the screen's one amber button; the rule picks the first.
        let why = MButton("See why", .secondary, rank: 2)
        let r = checkRow(title, status, button: why)
        why.onPress = { [weak r] in r?.onPress?() }
        rows.append(r)
        list.add(r)
        attentionRows += 1
      }
    }
    var link: MButton?
    if rows.count > 2 {
      let l = MButton("Show all (\(rows.count))", .link)
      l.onPress = { [weak l, weak list] in
        guard let l = l, let list = list else { return }
        let showing = l.text.hasPrefix("Show fewer")
        l.text = showing ? "Show all (\(rows.count))" : "Show fewer"
        self.collapse(list, keep: showing ? 2 : rows.count)
      }
      link = l
    }
    put(block("NEEDS YOU", source: "memev doctor --json", link: link, [list]), in: s.content)
    if rows.count > 2 { collapse(list, keep: 2) }
    put(block("TODAY", [note("Progress by project, bots now and runs today need the core's work status and runner status, which this window does not read yet. No sample figures are shown.")]), in: s.content)
    if let later = laterBlock("overview") { put(later, in: s.content, labelled: false) }
  }

  /// Shows the first keep rows of a list and hides the rest with their dividers.
  func collapse(_ list: ListBox, keep: Int) {
    var n = 0
    for v in list.stack.arrangedSubviews {
      if v is RowView { n += 1 }
      v.isHidden = n > keep
    }
    enforceAmber()
  }

  func buildHealth(_ s: ScreenView) {
    clear(s.content)
    checkRows = 0
    let up = memev_shim_daemon_up(glance)
    let shown = Int(memev_shim_check_shown(glance))
    if !glanceRead {
      let l = ListBox()
      l.add(RowView(glyph: nil, title: "Reading Memev."))
      put(block("CHECKS", source: "memev doctor --json", [l]), in: s.content)
      let svc = ListBox()
      svc.add(RowView(glyph: nil, title: "Reading Memev.", code: "memev status --json", tech: technical))
      put(block("MEMEV SERVICE", source: "memev status --json", [svc]), in: s.content)
      return
    }
    if stopped {
      let start = MButton("Start Memev", .secondary, rank: 2) { [weak self] in self?.startSheet() }
      let l = ListBox()
      l.add(RowView(glyph: .bad, title: "Memev is not running", sub: "start it and your apps share their memory again", button: start))
      put(block("TO LOOK AT", [l]), in: s.content)
    }
    if !up || shown == 0 {
      let l = ListBox()
      l.add(RowView(glyph: .off, title: "No checks were returned."))
      checkRows = 1
      put(block("CHECKS", source: "memev doctor --json", [l]), in: s.content)
    } else {
      var look: [RowView] = []
      var fine: [RowView] = []
      var fineNames: [String] = []
      for i in 0..<shown {
        let status = String(cString: memev_shim_check_status(glance, Int32(i)))
        let title = String(cString: memev_shim_check_title(glance, Int32(i)))
        if status == "ok" { fine.append(checkRow(title, status)); fineNames.append(title.lowercased()) } else { look.append(checkRow(title, status)) }
      }
      checkRows = look.count + fine.count
      if !look.isEmpty {
        let l = ListBox()
        for r in look { l.add(r) }
        put(block("TO LOOK AT", source: "memev doctor --json", [l]), in: s.content)
      }
      if !fine.isEmpty {
        let e = ExpanderRow(title: fine.count == 1 ? "1 check is fine" : "\(fine.count) checks are fine", summary: fineNames.prefix(7).joined(separator: " · "))
        let l = ListBox()
        for r in fine { l.add(r) }
        addFull(l, to: e.body)
        put(e, in: s.content, labelled: look.isEmpty == false ? false : true)
      }
      if pips().contains(where: { $0.state == "off" }) {
        let e = ExpanderRow(title: "Off by your choice (1)", summary: "web access for cloud AI")
        let l = ListBox()
        let manage = MButton("Manage", .secondary) { [weak self] in self?.open("bots", tab: "connect") }
        l.add(RowView(glyph: .off, title: "Web access for cloud AI", sub: "off until you switch it on", button: manage))
        addFull(l, to: e.body)
        put(e, in: s.content, labelled: false)
      }
    }
    let svc = ListBox()
    svc.add(RowView(glyph: up ? .ok : .bad, title: up ? "Running" : "Stopped", sub: up ? "the core's status answers" : "the core's status does not answer",
                    code: "memev status --json", tech: technical))
    put(block("MEMEV SERVICE", source: "memev status --json", [svc]), in: s.content)
    let help = ExpanderRow(title: "Get help", summary: "Help and support")
    let hl = ListBox()
    let openHelp = MButton("Open", .secondary) { [weak self] in self?.open("settings", tab: "help") }
    hl.add(RowView(glyph: nil, title: "Help and support", sub: "email, report a problem, ask for a feature", button: openHelp))
    addFull(hl, to: help.body)
    put(help, in: s.content, labelled: false)
    if let later = laterBlock("health") { put(later, in: s.content, labelled: false) }
  }

  /// A data screen's content from what its read returned, each section under its tab.
  func buildData(_ id: String, _ s: ScreenView, _ d: PageData?) {
    let tabs = def(id).tabs
    let keys = tabs.isEmpty ? [""] : tabs.map { $0.id }
    var views: [String: NSStackView] = [:]
    for k in keys { views[k] = vstack([], spacing: 0) }
    if id == "memory", let v = views["search"] { search.removeFromSuperview(); v.addArrangedSubview(search) }
    if let d = d {
      for sec in d.sections {
        guard let v = views[sec.tab] ?? views[""] else { continue }
        let list = ListBox()
        if sec.rows.isEmpty && !sec.empty.isEmpty { list.add(RowView(glyph: nil, title: sec.empty)) }
        for r in sec.rows {
          let row = RowView(glyph: r.tone == 2 ? .bad : r.tone == 1 ? .warn : nil, title: r.title, sub: r.sub)
          row.onPress = { [weak self, weak row] in
            guard let me = self, let row = row else { return }
            me.openDetail(DetailModel(key: "row:\(sec.head):\(r.title)", title: r.title, state: r.tone == 0 ? nil : (r.tone == 2 ? .bad : .warn, r.tone == 2 ? "Removed" : "Needs a look"),
                                      why: r.sub, source: sec.source), from: row)
          }
          list.add(row)
        }
        if !sec.rows.isEmpty || !sec.empty.isEmpty { put(block(sec.head, source: sec.source, [list]), in: v) }
      }
    } else {
      for k in keys where NOT_BUILT["\(id)/\(k)"] == nil { put(note("Reading Memev."), in: views[k]!, labelled: false) }
    }
    if id == "memory", d?.sections.contains(where: { $0.head == "SEARCH" }) != true, let v = views["search"] {
      put(note("Type words and press Return to search everything your AI apps have saved."), in: v, labelled: false)
    }
    if id == "settings", let v = views["general"] { put(settingsControls(), in: v) }
    for k in keys {
      if let why = NOT_BUILT["\(id)/\(k)"] { put(note(why), in: views[k]!, labelled: false) }
      if let later = laterBlock(k.isEmpty ? id : "\(id)/\(k)") { put(later, in: views[k]!, labelled: false) }
    }
    if let later = laterBlock(id), !keys.contains(""), let v = views[keys.first!] { put(later, in: v, labelled: false) }
    install(views, in: s)
  }

  /// Settings, General: Appearance and Technical detail, both settings of this app (SH-23, SH-24).
  func settingsControls() -> NSView {
    let seg = NSSegmentedControl(labels: ["This computer", "Dark", "Light"], trackingMode: .selectOne, target: self, action: #selector(appearanceChanged(_:)))
    seg.selectedSegment = appearance == "dark" ? 1 : appearance == "light" ? 2 : 0
    seg.isEnabled = ProcessInfo.processInfo.environment["MEMEV_THEME"] == nil
    seg.setAccessibilityLabel("Appearance")
    let sw = NSSwitch()
    sw.state = technical ? .on : .off
    sw.target = self
    sw.action = #selector(technicalChanged(_:))
    sw.setAccessibilityLabel("Technical detail")
    let l = ListBox()
    l.add(RowView(glyph: nil, title: "Appearance", sub: "dark, light, or as this computer is set", button: nil))
    l.add(hstack([seg], spacing: 0))
    l.add(RowView(glyph: nil, title: "Technical detail", sub: "show the commands and the core's own words behind each screen", button: nil))
    l.add(hstack([sw], spacing: 0))
    return block("THIS APP", [l])
  }

  func buildPlaceholder(_ id: String, _ s: ScreenView) {
    let tabs = def(id).tabs
    let keys = tabs.isEmpty ? [""] : tabs.map { $0.id }
    var views: [String: NSStackView] = [:]
    for k in keys {
      let v = vstack([], spacing: 0)
      if id == "project" && k == "overview" {
        let star = String(cString: memev_shim_north_star(glance))
        let l = ListBox()
        l.add(RowView(glyph: nil, title: "North star", sub: star.isEmpty ? "No north star yet." : star))
        put(block("THE PROJECT", source: "memev card --json", [l]), in: v)
      }
      if let why = NOT_BUILT["\(id)/\(k)"] { put(note(why), in: v, labelled: false) }
      if let later = laterBlock(k.isEmpty ? id : "\(id)/\(k)") { put(later, in: v, labelled: false) }
      views[k] = v
    }
    if let later = laterBlock(id), let first = keys.first, let v = views[first] { put(later, in: v, labelled: false) }
    install(views, in: s)
  }

  func install(_ views: [String: NSStackView], in s: ScreenView) {
    clear(s.content)
    s.tabViews = views
    let order = def(screenID(s)).tabs.map { $0.id }
    for k in (order.isEmpty ? [""] : order) { if let v = views[k] { addFull(v, to: s.content) } }
  }

  func screenID(_ s: ScreenView) -> String { screens.first { $0.value === s }?.key ?? "" }

  // MARK: Navigation

  func select(_ index: Int) {
    for (i, b) in navButtons.enumerated() { b.selectedState = i == index }
    projectButton?.selectedState = false
    helpButton?.selectedState = false
    healthButton.open = false
  }

  func showGuide() {
    current = "guide"
    currentPage = -1
    closeDetail()
    render()
  }

  func setPrimary(_ title: String) { primary.text = title }

  // Leaving the guide for a page ends it for this run; it shows again at the next start until it is finished.
  func leaveGuide() {
    if inGuide { inGuide = false }
  }

  func showPage(_ index: Int) {
    guard index >= 0 && index < NAV.count else { return }
    open(NAV[index].id)
  }

  static let dataScreens = ["projects", "bots", "memory", "checked", "settings"]

  func open(_ id: String, tab: String? = nil) {
    leaveGuide()
    if sheet.isOpen { sheet.dismiss() }
    current = id
    if let t = tab { tabFor[id] = t }
    currentPage = NAV.firstIndex { $0.id == id } ?? -1
    closeDetail()
    if AppDelegate.dataScreens.contains(id) {
      buildData(id, screen(id), pageData[id])
      loadPage(id)
    } else if !["overview", "health"].contains(id) {
      buildPlaceholder(id, screen(id))
    }
    render()
  }

  func selectTab(_ t: String) {
    tabFor[current] = t
    closeDetail()
    render()
  }

  /// The tab list of a screen, with live counts and needs-you diamonds where the core's answers give them.
  func tabs(_ id: String) -> [TabItem] {
    var list = def(id).tabs
    for i in list.indices {
      if id == "memory" && list[i].id == "review", let n = pageData["memory"]?.counts["review"] { list[i].label = "Review (\(n))" }
      if id == "bots" && list[i].id == "team" { list[i].needsYou = quietBots > 0 && !stopped }
    }
    return list
  }

  func sentence(_ id: String, _ tab: String) -> String {
    switch id {
    case "overview": return sentences().1
    case "health": return sentences().0
    case "guide": return ""
    case "project":
      if tab == "overview" { let s = String(cString: memev_shim_north_star(glance)); return s.isEmpty ? "No north star yet." : "North star: \(s)" }
    case "settings" where tab == "general":
      let theme = Theme.current.dark ? "Dark theme." : "Light theme."
      return version.isEmpty ? theme : "\(theme) Version \(version)."
    default: break
    }
    if let s = pageData[id]?.sentences[tab], !s.isEmpty { return s }
    return def(id).sentences[tab] ?? ""
  }

  /// Draws the current screen: header, tabs, the selected tab's content, the top bar and the banner, then the amber rule.
  func render() {
    let id = current
    let d = def(id)
    let s = screen(id)
    let tabList = tabs(id)
    let tab = tabFor[id] ?? tabList.first?.id ?? ""
    if id != "guide" {
      s.header.configure(title: d.title, sentence: sentence(id, tab), tabs: tabList, selected: tab, showWhat: !d.words.isEmpty, actions: [s.refresh])
    } else {
      s.header.configure(title: s.header.title.stringValue, sentence: "", tabs: [], selected: "", showWhat: false, actions: [primary])
      clear(s.content)
      addFull(guide.view, to: s.content)
    }
    for (k, v) in s.tabViews { v.isHidden = !(k == tab || (k == "" && tabList.isEmpty)) }
    for (k, v) in screens { v.root.isHidden = k != id }

    // The menu: the selected entry, or none for Health, a project's Help and support and the guide.
    select(currentPage)
    if id == "health" { healthButton.open = true }
    if id == "project" { projectButton.selectedState = true }
    if id == "settings" && tab == "help" { select(-1); helpButton.selectedState = true }

    // SH-6: the breadcrumb.
    let inProject = id == "project"
    crumbLink.isHidden = !inProject
    crumbSep.isHidden = !inProject
    crumb.stringValue = id == "guide" ? "Setting up" : inProject ? projectName : d.id == "health" ? "Health" : d.menu
    techChip.isHidden = !technical
    banner.isHidden = !stopped || id == "guide"
    pageStack.edgeInsets = NSEdgeInsets(top: 32, left: 36, bottom: 64, right: 36)
    enforceAmber()
    repaintTree(root)
  }

  func enforceAmber() {
    AmberRule.enforce(shellRow, stopped: stopped && current != "guide", noPrimary: def(current).noPrimary)
  }

  @objc func navClicked(_ sender: NSButton) { showPage(sender.tag) }

  @objc func projectClicked(_ sender: NSButton) { if !projectName.isEmpty { open("project") } }

  @objc func helpClicked(_ sender: NSButton) { open("settings", tab: "help") }

  @objc func searchChanged(_ sender: Any?) { if current == "memory" { loadPage("memory") } }

  @objc func focusCommand(_ sender: Any?) { window.makeFirstResponder(command) }

  /// SH-9: goes to a screen, a tab or the project by name; anything else searches the notes. It never sends anything to a bot.
  @objc func commandEntered(_ sender: Any?) {
    let q = command.stringValue.trimmingCharacters(in: .whitespaces)
    let low = q.lowercased()
    if low.isEmpty { return }
    command.stringValue = ""
    func say(_ s: String) { statusLine.stringValue = s }
    for d in NAV + [HEALTH] where d.menu.lowercased() == low || d.title.lowercased() == low {
      open(d.id); say("Opened \(d.menu)."); return
    }
    if low == "help" || low == "help and support" { open("settings", tab: "help"); say("Opened Help and support."); return }
    for d in NAV {
      for t in d.tabs where t.label.lowercased() == low || "\(d.menu) \(t.label)".lowercased() == low {
        open(d.id, tab: t.id); say("Opened \(d.menu), \(t.label)."); return
      }
    }
    if !projectName.isEmpty && projectName.lowercased() == low { open("project"); say("Opened \(projectName)."); return }
    for d in NAV + [HEALTH] where low.count >= 3 && d.menu.lowercased().hasPrefix(low) {
      open(d.id); say("Opened \(d.menu)."); return
    }
    search.stringValue = q
    open("memory", tab: "search")
    say("Searching your notes for \u{201C}\(q)\u{201D}.")
  }

  // MARK: The detail panel and sheets

  func openDetail(_ m: DetailModel, from row: RowView?) {
    if !detail.isHidden, detail.model?.key == m.key { closeDetail(); return }
    selectedRow?.selectedState = false
    selectedRow = row
    row?.selectedState = true
    detail.show(m, technical: technical)
    detail.isHidden = false
    detailEdge.isHidden = false
    window.makeFirstResponder(detail.close)
  }

  func closeDetail() {
    guard !detail.isHidden else { return }
    detail.isHidden = true
    detailEdge.isHidden = true
    if let r = selectedRow {
      r.selectedState = false
      if !r.isHiddenOrHasHiddenAncestor { window.makeFirstResponder(r) }
    }
    selectedRow = nil
  }

  func openWhat() {
    let d = def(current)
    openDetail(DetailModel(key: "what:\(current)", title: "What is this?", why: "The words on this screen, in plain terms.",
                           groups: [DetailGroup(label: "WORDS ON THIS SCREEN", rows: d.words.map { DetailRow(title: $0.0, sub: $0.1) })]), from: nil)
  }

  static let startTitle = "Start Memev?"

  /// SH-10 under the owner's ruling D-1: the window starts Memev itself, after a sheet that names what changes. The main button
  /// runs the core's own start (native/CLI-CONTRACT.md), which opens the background service for these notes and returns once
  /// it answers or gives up.
  func startSheet() {
    sheet.present(SheetModel(title: AppDelegate.startTitle,
                             note: "This starts Memev's background service on this Mac, so your AI apps can reach their shared memory again. It does not change your notes, and it does not make Memev start when you sign in.",
                             source: "memev start", main: "Start Memev", onMain: { [weak self] host in self?.runStart(host) }))
  }

  /// Runs start off the main thread, shows that it is starting, then the result, and reads the core again for the health state.
  func runStart(_ host: SheetHost) {
    if starting { return }
    starting = true
    host.status.stringValue = "Starting Memev. This can take a few seconds."
    host.mainButton?.isEnabled = false
    statusLine.stringValue = "Starting Memev."
    DispatchQueue.global(qos: .userInitiated).async {
      // The core waits for its service to answer before it returns; a first open of a large vault can take longer.
      let (code, _) = memevRun(["start"], timeout: 60000)
      DispatchQueue.main.async {
        self.starting = false
        let title: String
        let said: String
        switch code {
        case 0:
          title = "Memev is running"
          said = "Memev's background service is running on this Mac. Your AI apps can reach their shared memory."
        case -1 where !memev_shim_have_binary(self.glance):
          title = "Memev could not be run"
          said = "This window could not find the memev command it runs. Nothing was started."
        case -1:
          title = "Memev has not answered yet"
          said = "Memev did not answer within a minute. It may still be opening your notes; Health shows when it is running."
        default:
          title = "Memev did not start"
          said = "Memev did not start (the core stopped with code \(code)). Its log on this Mac says why, and Health shows its checks."
        }
        self.statusLine.stringValue = title + "."
        if host.isOpen && host.model?.title == AppDelegate.startTitle {
          host.present(SheetModel(title: title, note: said, source: "memev start", main: "Close"))
        }
        self.refreshNow { self.onStartDone?(code) }
      }
    }
  }

  /// Reads the core again now, after a poll already under way has finished.
  func refreshNow(_ then: @escaping () -> Void) {
    if loading { DispatchQueue.main.asyncAfter(deadline: .now() + 0.2) { self.refreshNow(then) }; return }
    reload(then)
  }

  // MARK: Settings of this app

  @objc func appearanceChanged(_ sender: NSSegmentedControl) {
    appearance = ["system", "dark", "light"][max(0, min(2, sender.selectedSegment))]
    UserDefaults.standard.set(appearance, forKey: "appearance")
    applyAppearance()
    paint()
    render()
  }

  @objc func technicalChanged(_ sender: NSSwitch) {
    technical = sender.state == .on
    UserDefaults.standard.set(technical, forKey: "technicalDetail")
    fill()
    open(current, tab: tabFor[current])
    statusLine.stringValue = technical ? "Technical detail is on." : "Technical detail is off."
  }

  // MARK: Reading the core

  // A page reads the core off the main thread; a newer load replaces an older one that ends later.
  func loadPage(_ id: String) {
    pageLoad += 1
    let ticket = pageLoad
    let query = search.stringValue
    let theme = ProcessInfo.processInfo.environment["MEMEV_THEME"] ?? (appearance == "system" ? "follows this computer" : appearance)
    DispatchQueue.global(qos: .userInitiated).async {
      let d: PageData
      switch id {
      case "memory": d = Pages.memory(query: query)
      case "checked": d = Pages.checked()
      case "bots": d = Pages.bots()
      case "projects": d = Pages.projects()
      default: d = Pages.settings(theme: theme)
      }
      DispatchQueue.main.async {
        guard ticket == self.pageLoad else { return }
        self.pageData[id] = d
        let s = self.screen(id)
        self.buildData(id, s, d)
        if self.current == id { self.render() }
        if self.selfCheck, let want = ProcessInfo.processInfo.environment["MEMEV_SELF_CHECK_PAGE"], want == id {
          self.firstReads.notify(queue: .main) { self.reportPage(id, d.sections) }
        }
      }
    }
  }

  func reportPage(_ id: String, _ sections: [PageSection]) {
    print("page=\(id)")
    for s in sections {
      print("section=\(s.head) rows=\(s.rows.count)")
      if s.rows.isEmpty { print("empty=\(s.empty)") }
      for r in s.rows.prefix(5) { print("row=\(r.title)") }
    }
    var texts: [String] = []
    walk(window.contentView!) { v in if let f = v as? NSTextField { texts.append(f.stringValue) } }
    let leak = texts.contains { $0.contains("/Users/") || $0.contains("/var/folders/") || $0.contains("should-not-appear") }
    print("leak=\(leak ? "present" : "absent")")
    fflush(stdout)
    exit(leak ? 1 : 0)
  }

  @objc func refreshClicked(_ sender: Any?) {
    if inGuide { guide.next(); return }
    reload()
    if AppDelegate.dataScreens.contains(current) { loadPage(current) }
  }

  // The core is read off the main thread; the window is filled on it.
  func reload(_ then: (() -> Void)? = nil) {
    if loading { return }
    loading = true
    DispatchQueue.global(qos: .userInitiated).async {
      let fresh = memev_shim_new()!
      memev_shim_load(fresh)
      let doc = AppDelegate.readDoctor()
      let ros = memevJSON(["bus", "roster", "--json"]) as? [String: Any]
      DispatchQueue.main.async {
        let old = self.glance
        self.glance = fresh
        memev_shim_free(old)
        self.doctor = doc
        self.roster = ros
        self.loading = false
        self.paint()
        self.fill()
        if self.inGuide { self.guide.paint(self.palette) }
        then?()
      }
    }
  }

  func walk(_ v: NSView, _ f: (NSView) -> Void) {
    f(v)
    for c in v.subviews { walk(c, f) }
  }

  // MARK: The self-check

  /// MEMEV_SELF_CHECK_START: opens Start Memev's sheet, presses its main button, and prints what the core answered and what the
  /// window shows once it has read the core again.
  func checkStart() {
    startSheet()
    print("start_sheet=\(sheet.model?.title ?? "")|\(sheet.mainButton?.text ?? "")")
    print("start_before=\(memev_shim_daemon_up(glance) ? "running" : "stopped")")
    onStartDone = { code in
      print("start_rc=\(code)")
      print("start_result=\(self.sheet.model?.title ?? "")")
      print("start_status=\(self.statusLine.stringValue)")
      print("start_after=\(memev_shim_daemon_up(self.glance) ? "running" : "stopped")")
      print("start_banner=\(self.banner.isHidden ? "hidden" : "shown")")
      fflush(stdout)
      exit(0)
    }
    sheet.mainButton?.performClick(nil)
  }

  func reportAndQuit() {
    window.layoutIfNeeded()
    let root = window.contentView!
    var focusable = 0
    var texts: [String] = []
    walk(root) { v in
      if let b = v as? NSButton {
        if b.isEnabled && !b.refusesFirstResponder { focusable += 1 }
        texts.append(b.title)
      } else if let f = v as? NSTextField {
        texts.append(f.stringValue)
      }
    }
    // Only the amber buttons a person can see count: hidden screens keep their own page action.
    let primaryCount = AmberRule.visibleAmber(root)
    let secret = texts.contains { $0.contains("should-not-appear") || $0.contains("vault-marker") }
    let accel = refreshItem.keyEquivalent == "r" && refreshItem.keyEquivalentModifierMask == [.command] && NSApp.mainMenu?.indexOfItem(withSubmenu: refreshItem.menu) ?? -1 >= 0
    let (health, overview) = sentences()
    let match = healthButton.title == health && overviewSentence.stringValue == overview
    let w = Int(window.contentMinSize.width), h = Int(window.contentMinSize.height)
    let sw = Int(sidebarWidth.constant)
    print("health=\(healthButton.title)")
    print("overview=\(overviewSentence.stringValue)")
    print("star=\(String(cString: memev_shim_north_star(glance)))")
    print("attention=\(attentionRows)")
    print("checks=\(checkRows)")
    print("primary=\(primaryCount)")
    print("minw=\(w)")
    print("minh=\(h)")
    print("sidebar=\(sw)")
    print("focusable=\(focusable)")
    print("accel=\(accel ? 1 : 0)")
    print("match=\(match ? 1 : 0)")
    print("secret=\(secret ? "present" : "absent")")
    print("theme=\(Theme.current.dark ? "dark" : "light")")
    print("visible=\(window.isVisible ? 1 : 0)")
    let ring = AmberRule.ringed(root)
    print("amber_ring=\(ring ? 1 : 0)")
    print("font_sans=\(TypeStyle.body.font.familyName ?? "")")
    print("font_mono=\(TypeStyle.label.font.familyName ?? "")")
    print("font_weights=\(Fonts.weightOf(TypeStyle.body.font)),\(Fonts.weightOf(TypeStyle.bodyMedium.font)),\(Fonts.weightOf(TypeStyle.wordmark.font))")
    print("font_mono_faces=\(TypeStyle.code.font.fontName),\(TypeStyle.label.font.fontName)")
    print("reads=\(readsDone)")
    print("shown_ms=\(shownMs)")
    // A picture of the window for a reviewer, printed and never written to a file, only when asked for.
    if ProcessInfo.processInfo.environment["MEMEV_SELF_CHECK_PNG"] != nil, let rep = root.bitmapImageRepForCachingDisplay(in: root.bounds) {
      root.cacheDisplay(in: root.bounds, to: rep)
      if let png = rep.representation(using: .png, properties: [:]) { print("png=\(png.base64EncodedString())") }
    }
    var shellOK = true
    if inGuide {
      print("guide=\(pageTitle.stringValue)")
      print("primary_label=\(primary.title)")
    } else {
      let ps = pips()
      print("pips=\(healthButton.pips.arrangedSubviews.count)")
      for p in ps { print("pip=\(p.name):\(p.state)") }
      // Every screen and tab, with the most amber buttons a person can see on any one of them (SH-20).
      var most = 0
      for d in NAV + [HEALTH] {
        open(d.id)
        let tabList = tabs(d.id)
        print("screen=\(d.id)|\(screen(d.id).header.title.stringValue)|\(tabList.map { $0.label }.joined(separator: ","))")
        for t in (tabList.isEmpty ? [TabItem(id: "", label: "")] : tabList) {
          if !t.id.isEmpty { selectTab(t.id) }
          window.layoutIfNeeded()
          most = max(most, AmberRule.visibleAmber(root))
        }
      }
      print("amber_max=\(most)")
      // A confirmation sheet has no amber button and a red main button; a form sheet has one amber button.
      sheet.present(ConfirmKind.stop.sheet("") { $0.dismiss() })
      let confirmAmber = AmberRule.visibleAmber(sheet)
      let red = sheet.mainButton?.base == .danger
      sheet.dismiss()
      startSheet()
      let sheetAmber = AmberRule.visibleAmber(sheet)
      let sheetRing = AmberRule.ringed(sheet)
      print("start_sheet=\(sheet.model?.title ?? "")|\(sheet.mainButton?.text ?? "")")
      sheet.dismiss()
      print("confirm_amber=\(confirmAmber)")
      print("confirm_main=\(red ? "red" : "plain")")
      print("sheet_amber=\(sheetAmber)")
      print("sheet_ring=\(sheetRing ? 1 : 0)")
      shellOK = most <= 1 && ps.count == 14 && healthButton.pips.arrangedSubviews.count == 14 && confirmAmber == 0 && red && sheetAmber == 1 && sheetRing
    }
    let ok = match && primaryCount == 1 && ring && readsDone == 6 && w >= 1024 && h >= 700 && sw == 240 && focusable >= 9 && accel && !secret && window.isVisible && shellOK
    fflush(stdout)
    exit(ok ? 0 : 1)
  }
}
