// SPDX-License-Identifier: Apache-2.0
// The first native slice on macOS: the same window as native/linux, in AppKit. It reads the core through the shared reader
// (native/linux/src/read.c), writes nothing and sends nothing.
import AppKit

struct NavItem {
  let id: String
  let label: String
  let kind: Int // 0 overview, 2 later
  let later: String
}

let NAV: [NavItem] = [
  NavItem(id: "overview", label: "Overview", kind: 0, later: ""),
  NavItem(id: "operations", label: "Operations", kind: 2, later: "The live agent panes need the core's bus filters (SO-N-15), which are not on main yet. Bots and agents shows the team today."),
  NavItem(id: "projects", label: "Projects", kind: 2, later: "Not in this slice. No project list is loaded yet."),
  NavItem(id: "bots", label: "Bots and agents", kind: 2, later: "Not in this slice. The roster is not loaded yet."),
  NavItem(id: "memory", label: "Memory", kind: 2, later: "Not in this slice. Search stays on the memev command until a later slice."),
  NavItem(id: "checked", label: "Checked work", kind: 2, later: "Not in this slice. Receipts are not listed yet."),
  NavItem(id: "settings", label: "Settings", kind: 2, later: "Not in this slice. The theme follows this computer."),
]

func hex(_ v: UInt32) -> NSColor {
  NSColor(srgbRed: CGFloat((v >> 16) & 0xFF) / 255, green: CGFloat((v >> 8) & 0xFF) / 255, blue: CGFloat(v & 0xFF) / 255, alpha: 1)
}

// The visual system's values, the same as the Linux window's style sheet.
struct Palette {
  let background, text, dim, sub, navText, selected, rule, bad, warn: NSColor
  static let dark = Palette(background: hex(0x0A0D12), text: hex(0xE8ECF2), dim: hex(0x7F8997), sub: hex(0xA4ADBA), navText: hex(0xA4ADBA),
                            selected: hex(0x181E27), rule: hex(0x374252), bad: hex(0xFF8A7A), warn: hex(0xF2B544))
  static let light = Palette(background: hex(0xF6F7F9), text: hex(0x10151C), dim: hex(0x5F6978), sub: hex(0x46505E), navText: hex(0x46505E),
                             selected: hex(0xEDEFF3), rule: hex(0xC3CAD4), bad: hex(0xB3261E), warn: hex(0x8A5600))
  static let amber = hex(0xF2B544)
  static let onAmber = hex(0x0A0D12)
}

final class FlippedView: NSView {
  override var isFlipped: Bool { true }
}

// A borderless button whose title is drawn in the palette's colour, with an optional selected background and amber edge.
final class NavButton: NSButton {
  let edge = NSView()
  var selectedState = false

  convenience init(title: String, target: AnyObject, action: Selector) {
    self.init(frame: .zero)
    self.title = title
    self.target = target
    self.action = action
    isBordered = false
    alignment = .left
    wantsLayer = true
    translatesAutoresizingMaskIntoConstraints = false
    heightAnchor.constraint(greaterThanOrEqualToConstant: 32).isActive = true
    edge.wantsLayer = true
    edge.layer?.backgroundColor = Palette.amber.cgColor
    edge.translatesAutoresizingMaskIntoConstraints = false
    edge.isHidden = true
    addSubview(edge)
    NSLayoutConstraint.activate([
      edge.leadingAnchor.constraint(equalTo: leadingAnchor), edge.topAnchor.constraint(equalTo: topAnchor),
      edge.bottomAnchor.constraint(equalTo: bottomAnchor), edge.widthAnchor.constraint(equalToConstant: 2),
    ])
  }

  func paint(_ p: Palette) {
    let style = NSMutableParagraphStyle()
    style.alignment = .left
    style.firstLineHeadIndent = 12
    style.headIndent = 12
    style.lineBreakMode = .byWordWrapping
    attributedTitle = NSAttributedString(string: title, attributes: [
      .foregroundColor: selectedState ? p.text : p.navText, .font: NSFont.systemFont(ofSize: 13), .paragraphStyle: style,
    ])
    layer?.backgroundColor = selectedState ? p.selected.cgColor : NSColor.clear.cgColor
    edge.isHidden = !selectedState
  }
}

final class AppDelegate: NSObject, NSApplicationDelegate {
  var window: NSWindow!
  var palette = Palette.dark
  var glance = memev_shim_new()!
  var loading = false
  var selfCheck = ProcessInfo.processInfo.environment["MEMEV_SELF_CHECK"] != nil
  var attentionRows = 0

  let sidebar = NSStackView()
  var sidebarWidth: NSLayoutConstraint!
  let edge = NSView()
  let page = FlippedView()
  let pageStack = NSStackView()
  var healthButton: NavButton!
  var helpButton: NavButton!
  var navButtons: [NavButton] = []
  let wordmark = NSTextField(labelWithString: "Memev")
  let pageTitle = NSTextField(labelWithString: "Executive overview")
  let primary = NSButton(frame: .zero)
  let overviewSentence = NSTextField(wrappingLabelWithString: "")
  let northStar = NSTextField(wrappingLabelWithString: "")
  let attention = NSStackView()
  let overviewNote = NSTextField(wrappingLabelWithString: "Charts are a later slice. The lines above are the core's answers, read just now. No sample figures are shown.")
  let healthSentence = NSTextField(wrappingLabelWithString: "")
  let checks = NSStackView()
  let laterBody = NSTextField(wrappingLabelWithString: "")
  var overviewView: NSStackView!
  var healthView: NSStackView!
  var laterView: NSStackView!
  var sectionLabels: [NSTextField] = []
  var rules: [NSView] = []
  var refreshItem: NSMenuItem!
  var guide: GuideController!
  let pagesView = NSStackView()
  let search = NSSearchField()
  var currentPage = -1
  var pageLoad = 0
  var inGuide = false

  func applicationDidFinishLaunching(_ note: Notification) {
    let env = ProcessInfo.processInfo.environment
    switch env["MEMEV_THEME"] {
    case "dark": NSApp.appearance = NSAppearance(named: .darkAqua)
    case "light": NSApp.appearance = NSAppearance(named: .aqua)
    default: NSApp.appearance = nil
    }
    buildMenu()
    buildWindow()
    memev_shim_load(glance)
    paint()
    fill()
    if memev_shim_want_guide() { guide.show(0) } else { showPage(0) }
    window.makeKeyAndOrderFront(nil)
    NSApp.activate(ignoringOtherApps: true)
    if selfCheck {
      if let want = ProcessInfo.processInfo.environment["MEMEV_SELF_CHECK_PAGE"], let i = NAV.firstIndex(where: { $0.id == want }) {
        search.stringValue = ProcessInfo.processInfo.environment["MEMEV_SELF_CHECK_QUERY"] ?? ""
        showPage(i)
        return
      }
      DispatchQueue.main.async { self.reportAndQuit() }
      return
    }
    var ms = Int(env["MEMEV_POLL_MS"] ?? "") ?? 5000
    if ms < 200 { ms = 200 }
    Timer.scheduledTimer(withTimeInterval: Double(ms) / 1000, repeats: true) { [weak self] _ in self?.reload() }
  }

  func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }

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
    viewItem.submenu = viewMenu
    NSApp.mainMenu = main
  }

  func label(_ f: NSTextField, size: CGFloat, weight: NSFont.Weight = .regular) {
    f.font = NSFont.systemFont(ofSize: size, weight: weight)
    f.isSelectable = false
    f.translatesAutoresizingMaskIntoConstraints = false
  }

  func sectionHead(_ text: String) -> NSView {
    let row = NSStackView()
    row.orientation = .horizontal
    row.spacing = 8
    row.alignment = .centerY
    let mark = NSView()
    mark.wantsLayer = true
    mark.layer?.backgroundColor = Palette.amber.cgColor
    mark.translatesAutoresizingMaskIntoConstraints = false
    mark.widthAnchor.constraint(equalToConstant: 16).isActive = true
    mark.heightAnchor.constraint(equalToConstant: 2).isActive = true
    let l = NSTextField(labelWithString: text)
    label(l, size: 12)
    sectionLabels.append(l)
    let rule = NSView()
    rule.wantsLayer = true
    rule.translatesAutoresizingMaskIntoConstraints = false
    rule.heightAnchor.constraint(equalToConstant: 1).isActive = true
    rule.setContentHuggingPriority(.defaultLow, for: .horizontal)
    rules.append(rule)
    row.addArrangedSubview(mark)
    row.addArrangedSubview(l)
    row.addArrangedSubview(rule)
    return row
  }

  func column(_ views: [NSView], spacing: CGFloat) -> NSStackView {
    let s = NSStackView(views: views)
    s.orientation = .vertical
    s.alignment = .leading
    s.spacing = spacing
    s.translatesAutoresizingMaskIntoConstraints = false
    for v in views where v is NSStackView && (v as! NSStackView).orientation == .horizontal {
      v.widthAnchor.constraint(equalTo: s.widthAnchor).isActive = true
    }
    return s
  }

  func buildWindow() {
    window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 1280, height: 800), styleMask: [.titled, .closable, .miniaturizable, .resizable],
                      backing: .buffered, defer: false)
    window.title = "Memev"
    window.contentMinSize = NSSize(width: 1024, height: 700)
    window.center()
    let root = NSView()
    root.wantsLayer = true
    window.contentView = root

    sidebar.orientation = .vertical
    sidebar.alignment = .leading
    sidebar.spacing = 8
    sidebar.edgeInsets = NSEdgeInsets(top: 16, left: 12, bottom: 16, right: 12)
    sidebar.translatesAutoresizingMaskIntoConstraints = false
    label(wordmark, size: 20, weight: .medium)
    sidebar.addArrangedSubview(wordmark)
    healthButton = NavButton(title: "Reading Memev.", target: self, action: #selector(healthClicked(_:)))
    sidebar.addArrangedSubview(healthButton)
    for (i, item) in NAV.enumerated() {
      let b = NavButton(title: item.label, target: self, action: #selector(navClicked(_:)))
      b.tag = i
      navButtons.append(b)
      sidebar.addArrangedSubview(b)
    }
    let spacer = NSView()
    spacer.translatesAutoresizingMaskIntoConstraints = false
    spacer.setContentHuggingPriority(.init(1), for: .vertical)
    sidebar.addArrangedSubview(spacer)
    helpButton = NavButton(title: "Help and support", target: self, action: #selector(helpClicked(_:)))
    sidebar.addArrangedSubview(helpButton)
    for b in [healthButton!, helpButton!] + navButtons {
      b.widthAnchor.constraint(equalTo: sidebar.widthAnchor, constant: -24).isActive = true
    }

    edge.wantsLayer = true
    edge.translatesAutoresizingMaskIntoConstraints = false

    label(pageTitle, size: 28, weight: .medium)
    primary.title = "Refresh"
    primary.isBordered = false
    primary.wantsLayer = true
    primary.layer?.backgroundColor = Palette.amber.cgColor
    primary.layer?.cornerRadius = 6
    primary.identifier = NSUserInterfaceItemIdentifier("primary")
    primary.target = self
    primary.action = #selector(refreshClicked(_:))
    primary.translatesAutoresizingMaskIntoConstraints = false
    primary.heightAnchor.constraint(equalToConstant: 32).isActive = true
    primary.widthAnchor.constraint(greaterThanOrEqualToConstant: 88).isActive = true
    primary.attributedTitle = NSAttributedString(string: "Refresh", attributes: [.foregroundColor: Palette.onAmber, .font: NSFont.systemFont(ofSize: 13, weight: .medium)])
    let header = NSStackView(views: [pageTitle, primary])
    header.orientation = .horizontal
    header.spacing = 16
    header.alignment = .centerY
    header.distribution = .fill
    pageTitle.setContentHuggingPriority(.defaultLow, for: .horizontal)
    primary.setContentHuggingPriority(.required, for: .horizontal)

    for f in [overviewSentence, healthSentence, laterBody] { label(f, size: 16) }
    label(northStar, size: 13)
    label(overviewNote, size: 12)
    for f in [overviewSentence, laterBody, overviewNote, northStar, healthSentence] { f.preferredMaxLayoutWidth = 600 }
    for s in [attention, checks] {
      s.orientation = .vertical
      s.alignment = .leading
      s.spacing = 6
      s.translatesAutoresizingMaskIntoConstraints = false
    }
    overviewView = column([overviewSentence, northStar, sectionHead("STATUS"), attention, overviewNote], spacing: 24)
    healthView = column([healthSentence, sectionHead("CHECKS"), checks], spacing: 24)
    laterView = column([laterBody], spacing: 16)
    guide = GuideController(owner: self)
    pagesView.orientation = .vertical
    pagesView.alignment = .leading
    pagesView.spacing = 12
    pagesView.translatesAutoresizingMaskIntoConstraints = false
    search.placeholderString = "Search your notes"
    search.target = self
    search.action = #selector(searchChanged(_:))
    search.sendsSearchStringImmediately = false
    search.translatesAutoresizingMaskIntoConstraints = false
    search.widthAnchor.constraint(equalToConstant: 420).isActive = true

    pageStack.orientation = .vertical
    pageStack.alignment = .leading
    pageStack.spacing = 24
    pageStack.edgeInsets = NSEdgeInsets(top: 24, left: 36, bottom: 24, right: 36)
    pageStack.translatesAutoresizingMaskIntoConstraints = false
    for v in [header, overviewView!, healthView!, laterView!, guide.view, pagesView] as [NSView] {
      pageStack.addArrangedSubview(v)
      v.widthAnchor.constraint(equalTo: pageStack.widthAnchor, constant: -72).isActive = true
    }
    page.translatesAutoresizingMaskIntoConstraints = false
    page.addSubview(pageStack)
    let scroll = NSScrollView()
    scroll.hasVerticalScroller = true
    scroll.hasHorizontalScroller = false
    scroll.drawsBackground = false
    scroll.documentView = page
    scroll.translatesAutoresizingMaskIntoConstraints = false

    root.addSubview(sidebar)
    root.addSubview(edge)
    root.addSubview(scroll)
    sidebarWidth = sidebar.widthAnchor.constraint(equalToConstant: 240)
    NSLayoutConstraint.activate([
      sidebarWidth,
      sidebar.leadingAnchor.constraint(equalTo: root.leadingAnchor), sidebar.topAnchor.constraint(equalTo: root.topAnchor),
      sidebar.bottomAnchor.constraint(equalTo: root.bottomAnchor),
      edge.leadingAnchor.constraint(equalTo: sidebar.trailingAnchor), edge.widthAnchor.constraint(equalToConstant: 1),
      edge.topAnchor.constraint(equalTo: root.topAnchor), edge.bottomAnchor.constraint(equalTo: root.bottomAnchor),
      scroll.leadingAnchor.constraint(equalTo: edge.trailingAnchor), scroll.trailingAnchor.constraint(equalTo: root.trailingAnchor),
      scroll.topAnchor.constraint(equalTo: root.topAnchor), scroll.bottomAnchor.constraint(equalTo: root.bottomAnchor),
      page.leadingAnchor.constraint(equalTo: scroll.contentView.leadingAnchor), page.trailingAnchor.constraint(equalTo: scroll.contentView.trailingAnchor),
      page.topAnchor.constraint(equalTo: scroll.contentView.topAnchor),
      pageStack.leadingAnchor.constraint(equalTo: page.leadingAnchor), pageStack.trailingAnchor.constraint(equalTo: page.trailingAnchor),
      pageStack.topAnchor.constraint(equalTo: page.topAnchor), pageStack.bottomAnchor.constraint(equalTo: page.bottomAnchor),
    ])
  }

  // The theme follows the computer unless MEMEV_THEME forces one.
  func paint() {
    let dark = window.effectiveAppearance.bestMatch(from: [.darkAqua, .aqua]) == .darkAqua
    palette = dark ? .dark : .light
    let p = palette
    window.backgroundColor = p.background
    window.contentView?.layer?.backgroundColor = p.background.cgColor
    edge.layer?.backgroundColor = p.rule.cgColor
    for r in rules { r.layer?.backgroundColor = p.rule.cgColor }
    for f in [wordmark, pageTitle, northStar] { f.textColor = p.text }
    for f in [overviewSentence, healthSentence, laterBody] { f.textColor = p.text }
    overviewNote.textColor = p.dim
    for f in sectionLabels { f.attributedStringValue = NSAttributedString(string: f.stringValue, attributes: [.foregroundColor: p.dim, .kern: 0.72, .font: NSFont.systemFont(ofSize: 12)]) }
    for b in [healthButton!, helpButton!] + navButtons { b.paint(p) }
  }

  func row(_ title: String, _ status: String) -> NSTextField {
    let f = NSTextField(wrappingLabelWithString: "\(title): \(status)")
    label(f, size: 13)
    f.preferredMaxLayoutWidth = 600
    f.textColor = status == "fail" ? palette.bad : status == "ok" ? palette.text : palette.warn
    return f
  }

  func plain(_ text: String) -> NSTextField {
    let f = NSTextField(wrappingLabelWithString: text)
    label(f, size: 13)
    f.textColor = palette.text
    return f
  }

  func sentences() -> (String, String) {
    var h = [CChar](repeating: 0, count: 256)
    var o = [CChar](repeating: 0, count: 512)
    memev_shim_health(glance, &h, Int32(h.count))
    memev_shim_overview(glance, &o, Int32(o.count))
    return (String(cString: h), String(cString: o))
  }

  func fill() {
    let (health, overview) = sentences()
    healthButton.title = health
    healthButton.paint(palette)
    overviewSentence.stringValue = overview
    healthSentence.stringValue = health
    let star = String(cString: memev_shim_north_star(glance))
    northStar.stringValue = star
    northStar.isHidden = star.isEmpty

    for v in attention.arrangedSubviews { attention.removeArrangedSubview(v); v.removeFromSuperview() }
    attentionRows = 0
    let up = memev_shim_daemon_up(glance)
    let shown = Int(memev_shim_check_shown(glance))
    if !up {
      attention.addArrangedSubview(plain("Nothing to list while Memev is not running."))
    } else if memev_shim_look(glance) == 0 {
      attention.addArrangedSubview(plain("Nothing needs you."))
    } else {
      for i in 0..<shown {
        let status = String(cString: memev_shim_check_status(glance, Int32(i)))
        if status == "ok" { continue }
        attention.addArrangedSubview(row(String(cString: memev_shim_check_title(glance, Int32(i))), status))
        attentionRows += 1
      }
    }

    for v in checks.arrangedSubviews { checks.removeArrangedSubview(v); v.removeFromSuperview() }
    if !up || shown == 0 {
      checks.addArrangedSubview(plain("No checks were returned."))
    } else {
      for i in 0..<shown {
        checks.addArrangedSubview(row(String(cString: memev_shim_check_title(glance, Int32(i))), String(cString: memev_shim_check_status(glance, Int32(i)))))
      }
    }
  }

  func show(_ view: NSStackView, title: String) {
    pageTitle.stringValue = title
    for v in [overviewView!, healthView!, laterView!, guide.view, pagesView] { v.isHidden = v !== view }
  }

  func select(_ index: Int) {
    for (i, b) in navButtons.enumerated() { b.selectedState = i == index; b.paint(palette) }
  }

  func showGuide() { show(guide.view, title: pageTitle.stringValue) }

  func setPrimary(_ title: String) {
    primary.title = title
    primary.attributedTitle = NSAttributedString(string: title, attributes: [.foregroundColor: Palette.onAmber, .font: NSFont.systemFont(ofSize: 13, weight: .medium)])
  }

  // Leaving the guide for a page ends it for this run; it shows again at the next start until it is finished.
  func leaveGuide() {
    if inGuide { inGuide = false; setPrimary("Refresh") }
  }

  func showPage(_ index: Int) {
    guard index >= 0 && index < NAV.count else { return }
    leaveGuide()
    select(index)
    let item = NAV[index]
    currentPage = index
    if item.kind == 0 {
      show(overviewView, title: "Executive overview")
    } else if ["projects", "bots", "memory", "checked", "settings"].contains(item.id) {
      show(pagesView, title: item.label)
      loadPage(item.id)
    } else {
      laterBody.stringValue = item.later
      show(laterView, title: item.label)
    }
  }

  @objc func navClicked(_ sender: NSButton) { showPage(sender.tag) }

  @objc func searchChanged(_ sender: Any?) { if currentPage >= 0 && NAV[currentPage].id == "memory" { loadPage("memory") } }

  // A page reads the core off the main thread; a newer load replaces an older one that ends later.
  func loadPage(_ id: String) {
    pageLoad += 1
    let ticket = pageLoad
    let query = search.stringValue
    for v in pagesView.arrangedSubviews { pagesView.removeArrangedSubview(v); v.removeFromSuperview() }
    if id == "memory" { pagesView.addArrangedSubview(search) }
    pagesView.addArrangedSubview(plain("Reading Memev."))
    DispatchQueue.global(qos: .userInitiated).async {
      let sections: [PageSection]
      switch id {
      case "memory": sections = Pages.memory(query: query)
      case "checked": sections = Pages.checked()
      case "bots": sections = Pages.bots()
      case "projects": sections = Pages.projects()
      default: sections = Pages.settings()
      }
      DispatchQueue.main.async {
        guard ticket == self.pageLoad else { return }
        for v in self.pagesView.arrangedSubviews where v !== self.search { self.pagesView.removeArrangedSubview(v); v.removeFromSuperview() }
        for sec in sections {
          let head = self.sectionHead(sec.head)
          self.pagesView.addArrangedSubview(head)
          head.widthAnchor.constraint(equalTo: self.pagesView.widthAnchor).isActive = true
          if sec.rows.isEmpty && !sec.empty.isEmpty { self.pagesView.addArrangedSubview(self.plain(sec.empty)) }
          for r in sec.rows {
            let t = self.plain(r.title)
            t.textColor = r.tone == 2 ? self.palette.bad : r.tone == 1 ? self.palette.warn : self.palette.text
            self.pagesView.addArrangedSubview(t)
            if !r.sub.isEmpty {
              let d = self.plain(r.sub)
              d.font = NSFont.systemFont(ofSize: 12)
              d.textColor = self.palette.dim
              self.pagesView.addArrangedSubview(d)
              self.pagesView.setCustomSpacing(14, after: d)
            }
          }
        }
        self.paint()
        if self.selfCheck, let want = ProcessInfo.processInfo.environment["MEMEV_SELF_CHECK_PAGE"], want == id { self.reportPage(id, sections) }
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

  // Health is not a menu row. It opens the health page directly.
  @objc func healthClicked(_ sender: NSButton) {
    leaveGuide()
    select(-1)
    show(healthView, title: "Health")
  }

  @objc func helpClicked(_ sender: NSButton) {
    leaveGuide()
    select(-1)
    laterBody.stringValue = "Help and support is not in this slice. This app sends nothing."
    show(laterView, title: "Help and support")
  }

  @objc func refreshClicked(_ sender: Any?) {
    if inGuide { guide.next(); return }
    reload()
    if currentPage >= 0, ["projects", "bots", "memory", "checked", "settings"].contains(NAV[currentPage].id) { loadPage(NAV[currentPage].id) }
  }

  // The core is read off the main thread; the window is filled on it.
  func reload() {
    if loading { return }
    loading = true
    DispatchQueue.global(qos: .userInitiated).async {
      let fresh = memev_shim_new()!
      memev_shim_load(fresh)
      DispatchQueue.main.async {
        let old = self.glance
        self.glance = fresh
        memev_shim_free(old)
        self.loading = false
        self.paint()
        self.fill()
        if self.inGuide { self.guide.paint(self.palette) }
      }
    }
  }

  func walk(_ v: NSView, _ f: (NSView) -> Void) {
    f(v)
    for c in v.subviews { walk(c, f) }
  }

  func reportAndQuit() {
    window.layoutIfNeeded()
    let root = window.contentView!
    var primaryCount = 0, focusable = 0
    var texts: [String] = []
    walk(root) { v in
      if let b = v as? NSButton {
        if b.identifier?.rawValue == "primary" { primaryCount += 1 }
        if b.isEnabled && !b.refusesFirstResponder { focusable += 1 }
        texts.append(b.title)
      } else if let f = v as? NSTextField {
        texts.append(f.stringValue)
      }
    }
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
    print("checks=\(checks.arrangedSubviews.count)")
    print("primary=\(primaryCount)")
    print("minw=\(w)")
    print("minh=\(h)")
    print("sidebar=\(sw)")
    print("focusable=\(focusable)")
    print("accel=\(accel ? 1 : 0)")
    print("match=\(match ? 1 : 0)")
    print("secret=\(secret ? "present" : "absent")")
    print("theme=\(palette.background == Palette.dark.background ? "dark" : "light")")
    print("visible=\(window.isVisible ? 1 : 0)")
    if inGuide {
      print("guide=\(pageTitle.stringValue)")
      print("primary_label=\(primary.title)")
    }
    // A picture of the window for a reviewer, printed and never written to a file, only when asked for.
    if ProcessInfo.processInfo.environment["MEMEV_SELF_CHECK_PNG"] != nil, let rep = root.bitmapImageRepForCachingDisplay(in: root.bounds) {
      root.cacheDisplay(in: root.bounds, to: rep)
      if let png = rep.representation(using: .png, properties: [:]) { print("png=\(png.base64EncodedString())") }
    }
    let ok = match && primaryCount == 1 && w >= 1024 && h >= 700 && sw == 240 && focusable >= 9 && accel && !secret && window.isVisible
    fflush(stdout)
    exit(ok ? 0 : 1)
  }
}
