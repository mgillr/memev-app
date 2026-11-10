// SPDX-License-Identifier: Apache-2.0
// The guided first launch on macOS: the same steps, words and rules as the Linux window. Every rule about what may run lives in the
// shared guide (native/linux/src/guide.c); this file only draws the steps and collects what the person typed and ticked.
import AppKit

final class GuideController: NSObject {
  unowned let owner: AppDelegate
  let view = NSStackView()
  let text = NSTextField(wrappingLabelWithString: "")
  let entry = NSTextField(string: "")
  let detail = NSTextField(wrappingLabelWithString: "")
  let choices = NSStackView()
  let apps = NSStackView()
  let backupTick = NSButton(checkboxWithTitle: "Record this remote, and push my notes to it", target: nil, action: nil)
  let back = NSButton(title: "Back", target: nil, action: nil)
  /// The three steps after the check (REV2-SCREENS.md section 5) each have Skip; skipping changes nothing.
  let skip = NSButton(title: "Skip", target: nil, action: nil)
  var leadSeats: [String] = []
  var thisButton: NavButton!
  var otherButton: NavButton!
  var step = 0
  var firstComputer = -1
  var notesDir = ""
  var joinCode = ""
  var backupURL = ""
  var confirmBackup = false
  var selected: [String] = []
  var appsLoaded = false
  var busy = false

  init(owner: AppDelegate) {
    self.owner = owner
    super.init()
    view.orientation = .vertical
    view.alignment = .leading
    view.spacing = 16
    view.translatesAutoresizingMaskIntoConstraints = false
    for f in [text, detail] { owner.label(f, size: 13); f.preferredMaxLayoutWidth = 600 }
    entry.translatesAutoresizingMaskIntoConstraints = false
    entry.widthAnchor.constraint(equalToConstant: 520).isActive = true
    choices.orientation = .horizontal
    choices.spacing = 12
    thisButton = NavButton(title: "This is my first computer", target: self, action: #selector(pick(_:)))
    otherButton = NavButton(title: "I already use Memev on another computer", target: self, action: #selector(pick(_:)))
    for b in [thisButton!, otherButton!] { b.widthAnchor.constraint(greaterThanOrEqualToConstant: 260).isActive = true; choices.addArrangedSubview(b) }
    apps.orientation = .vertical
    apps.alignment = .leading
    apps.spacing = 6
    back.target = self
    back.action = #selector(goBack(_:))
    skip.target = self
    skip.action = #selector(skipStep(_:))
    for v in [text, choices, entry, apps, backupTick, detail, back, skip] as [NSView] { view.addArrangedSubview(v) }
  }

  func paint(_ p: Theme) {
    text.textColor = p.text
    detail.textColor = p.warn
    thisButton.selectedState = firstComputer == 1
    otherButton.selectedState = firstComputer == 0
    thisButton.paint(p)
    otherButton.paint(p)
    for v in apps.arrangedSubviews { (v as? NSButton)?.contentTintColor = p.text }
  }

  @objc func pick(_ sender: NSButton) {
    firstComputer = sender === thisButton ? 1 : 0
    paint(owner.palette)
  }

  @objc func goBack(_ sender: Any?) {
    if step <= 0 || busy { return }
    show(step - 1)
  }

  func show(_ s: Int) {
    step = s
    owner.inGuide = true
    owner.select(-1)
    owner.showGuide()
    back.isHidden = s == 0 || s >= 5
    skip.isHidden = s < 5
    choices.isHidden = true
    entry.isHidden = true
    apps.isHidden = true
    backupTick.isHidden = true
    detail.stringValue = ""
    owner.setPrimary("Continue")
    switch s {
    case 0:
      owner.pageTitle.stringValue = "Is this your first computer?"
      text.stringValue = "Nothing is created, connected or sent on this step."
      choices.isHidden = false
    case 1 where firstComputer == 0:
      owner.pageTitle.stringValue = "Join another computer"
      text.stringValue = "The join runs only when you press the button. It contacts the other computer."
      entry.isHidden = false
      entry.stringValue = joinCode
      entry.placeholderString = "The code from the other computer"
      owner.setPrimary("Join the other computer")
    case 1:
      owner.pageTitle.stringValue = "Where should the notes live?"
      text.stringValue = "Type a full path. The folder is created only when you continue, and only on this computer."
      entry.isHidden = false
      entry.stringValue = notesDir.isEmpty ? (ProcessInfo.processInfo.environment["HOME"] ?? "") + "/Memev" : notesDir
      entry.placeholderString = "/Users/you/Memev"
    case 2:
      owner.pageTitle.stringValue = "Which apps should Memev know?"
      text.stringValue = "The list is the core's own client list. A tick is remembered here. Nothing is connected: that list has no id, and this window does not keep its own list."
      loadApps()
      apps.isHidden = false
    case 3:
      owner.pageTitle.stringValue = "Back up to your own git remote?"
      text.stringValue = "Leave this blank to skip it. Nothing is recorded unless you tick the line. If you record a remote, Memev pushes your notes to it: when it starts, every 5 minutes by default, and when it stops."
      entry.isHidden = false
      entry.stringValue = backupURL
      entry.placeholderString = "https://, ssh://, git:// or git@host:path"
      backupTick.isHidden = false
      backupTick.state = .off
    case 5:
      owner.pageTitle.stringValue = "Connect your AI apps?"
      text.stringValue = "These are the AI apps Memev found on this computer. Memev adds itself to the settings of each one you tick and keeps a copy of the old settings first. A sheet names them before anything changes."
      apps.isHidden = false
      loadFoundApps()
      owner.setPrimary("Connect the ticked apps")
    case 6:
      owner.pageTitle.stringValue = "Bring in your existing AI chat history?"
      text.stringValue = "Memev can read the chats your AI apps keep on this computer, or a ChatGPT or Claude export. You see a preview before anything is kept, and each import can be undone."
      owner.setPrimary("Look at what is there")
    case 7:
      owner.pageTitle.stringValue = "Which app should lead?"
      text.stringValue = "The lead hands out tasks to the others and is the one you talk to. You can change it later."
      apps.isHidden = false
      loadSeats()
      owner.setPrimary("Make it the lead")
    default:
      owner.pageTitle.stringValue = "Check that a note can be saved"
      text.stringValue = "This uses the core on this computer. It does not use sample figures."
      owner.setPrimary("Check the notes")
    }
    paint(owner.palette)
  }

  func loadApps() {
    if appsLoaded { return }
    appsLoaded = true
    for v in apps.arrangedSubviews { apps.removeArrangedSubview(v); v.removeFromSuperview() }
    apps.addArrangedSubview(owner.plain("Reading the client list."))
    // The core is read off the main thread, once (the shim keeps one list), and the ticks are made on it.
    DispatchQueue.global(qos: .userInitiated).async {
      let n = Int(memev_shim_clients_load())
      var rows: [(String, String, Bool)] = []
      for i in 0..<max(n, 0) {
        rows.append((String(cString: memev_shim_client_name(Int32(i))), String(cString: memev_shim_client_state(Int32(i))),
                     memev_shim_client_selectable(Int32(i))))
      }
      DispatchQueue.main.async { self.showApps(n, rows) }
    }
  }

  func showApps(_ n: Int, _ rows: [(String, String, Bool)]) {
    for v in apps.arrangedSubviews { apps.removeArrangedSubview(v); v.removeFromSuperview() }
    if n <= 0 {
      apps.addArrangedSubview(owner.plain(n < 0 ? "The client list could not be read." : "The client list did not parse."))
      return
    }
    for (name, state, selectable) in rows {
      let tick = NSButton(checkboxWithTitle: "\(name): \(state)", target: nil, action: nil)
      tick.font = TypeStyle.body.font
      tick.isEnabled = selectable
      tick.identifier = NSUserInterfaceItemIdentifier(name)
      apps.addArrangedSubview(tick)
    }
    paint(owner.palette)
  }

  func plan() -> UnsafeMutablePointer<GuidePlan> {
    let p = memev_shim_plan_new()!
    memev_shim_plan_set(p, Int32(firstComputer), notesDir, joinCode, firstComputer == 0 && step == 1, backupURL, confirmBackup)
    var cstrs = selected.map { strdup($0) }
    let ptrs = cstrs.map { UnsafePointer($0) }
    ptrs.withUnsafeBufferPointer { memev_shim_plan_select(p, $0.baseAddress, Int32(selected.count)) }
    for c in cstrs { free(c) }
    cstrs.removeAll()
    return p
  }

  func check() -> (Bool, String) {
    let p = plan()
    defer { memev_shim_plan_free(p) }
    var why = [CChar](repeating: 0, count: 256)
    let ok = memev_shim_plan_check(p, &why, Int32(why.count)) == 0
    return (ok, String(cString: why))
  }

  // Join and the notes check call the core and can take seconds, so they run off the main thread.
  func apply(failure: String) {
    let p = plan()
    busy = true
    owner.primary.isEnabled = false
    DispatchQueue.global(qos: .userInitiated).async {
      var out = [CChar](repeating: 0, count: 400)
      let rc = memev_shim_plan_apply(p, &out, Int32(out.count))
      memev_shim_plan_free(p)
      let said = String(cString: out)
      DispatchQueue.main.async {
        self.busy = false
        self.owner.primary.isEnabled = true
        if rc != 0 {
          self.detail.stringValue = said.isEmpty ? failure : said
          return
        }
        // a computer that joined another's memory skips the three steps after the check: its memory lives on the other computer
        if self.firstComputer == 1 { self.show(5) } else { self.finish() }
      }
    }
  }

  // MARK: The three steps after the check (REV2-SCREENS.md section 5)

  @objc func skipStep(_ sender: Any?) {
    if busy { return }
    if step == 5 { show(6) } else if step == 6 || step == 61 { show(7) } else { finish() }
  }

  func clearList(_ placeholder: String) {
    for v in apps.arrangedSubviews { apps.removeArrangedSubview(v); v.removeFromSuperview() }
    apps.addArrangedSubview(owner.plain(placeholder))
  }

  /// The apps onboarding --json found; a connectable one starts ticked, and its tick carries the core's id.
  func loadFoundApps() {
    clearList("Reading the apps Memev found.")
    DispatchQueue.global(qos: .userInitiated).async {
      let (found, read) = Pages.apps()
      DispatchQueue.main.async {
        guard self.step == 5 else { return }
        for v in self.apps.arrangedSubviews { self.apps.removeArrangedSubview(v); v.removeFromSuperview() }
        self.owner.agents.apps = found
        let can = found.filter { $0.connectable }
        if !read { self.apps.addArrangedSubview(self.owner.plain("The list of AI apps did not answer. Skip this, and connect apps later from Bots and agents, Connect.")) }
        else if can.isEmpty { self.apps.addArrangedSubview(self.owner.plain(found.isEmpty ? "No AI apps were found on this computer." : "Every app Memev found is connected already, or needs a step by hand.")) }
        for a in can {
          let tick = NSButton(checkboxWithTitle: a.name, target: nil, action: nil)
          tick.font = TypeStyle.body.font
          tick.state = .on
          tick.identifier = NSUserInterfaceItemIdentifier(a.id)
          self.apps.addArrangedSubview(tick)
        }
        self.paint(self.owner.palette)
      }
    }
  }

  /// The seats bus roster --json lists, one to pick.
  func loadSeats() {
    clearList("Reading the seats on the message bus.")
    DispatchQueue.global(qos: .userInitiated).async {
      let r = memevJSON(["bus", "roster", "--json"]) as? [String: Any]
      let seats = ((r?["seats"] as? [[String: Any]]) ?? []).map { str($0["seat"]) }.filter { !$0.isEmpty && !$0.contains(" ") }
      DispatchQueue.main.async {
        guard self.step == 7 else { return }
        for v in self.apps.arrangedSubviews { self.apps.removeArrangedSubview(v); v.removeFromSuperview() }
        self.leadSeats = seats
        if r == nil { self.apps.addArrangedSubview(self.owner.plain("The roster did not answer. Skip this, and choose the lead later.")) }
        else if seats.isEmpty { self.apps.addArrangedSubview(self.owner.plain("No app has a seat yet. Each connected app gets one on its first session. Skip this, and choose the lead later with memev bus operator.")) }
        for (i, seat) in seats.enumerated() {
          let b = NSButton(radioButtonWithTitle: seat, target: self, action: #selector(self.pickSeat(_:)))
          b.font = TypeStyle.body.font
          b.state = i == 0 ? .on : .off
          b.identifier = NSUserInterfaceItemIdentifier(seat)
          self.apps.addArrangedSubview(b)
        }
        self.paint(self.owner.palette)
      }
    }
  }

  @objc func pickSeat(_ sender: NSButton) {
    for v in apps.arrangedSubviews { if let b = v as? NSButton, b !== sender { b.state = .off } }
  }

  func ticks() -> [String] {
    apps.arrangedSubviews.compactMap { v in
      guard let b = v as? NSButton, b.state == .on, let id = b.identifier?.rawValue else { return nil }
      return id
    }
  }

  func next() {
    if busy { return }
    switch step {
    case 0:
      if firstComputer < 0 { detail.stringValue = "Choose this computer or another one."; return }
      show(1)
    case 1 where firstComputer == 0:
      joinCode = entry.stringValue
      apply(failure: "Join did not run.")
    case 1:
      notesDir = entry.stringValue
      let (ok, why) = check()
      if !ok { detail.stringValue = why.isEmpty ? "That folder cannot be used." : why; return }
      show(2)
    case 2:
      selected = apps.arrangedSubviews.compactMap { v in
        guard let b = v as? NSButton, b.state == .on, let id = b.identifier?.rawValue else { return nil }
        return id
      }
      show(3)
    case 3:
      backupURL = entry.stringValue
      confirmBackup = backupTick.state == .on
      let (ok, why) = check()
      if !ok { detail.stringValue = why.isEmpty ? "That remote cannot be recorded." : why; return }
      show(4)
    case 5:
      let ids = Set(ticks().filter { plainId($0) })
      if ids.isEmpty { show(6); return }
      owner.agents.ticked = ids
      owner.agents.connectSheet(onDone: { [weak self] in self?.show(6) })
    case 6:
      owner.agents.importSheet()
      owner.setPrimary("Continue")
      step = 61
    case 61:
      show(7)
    case 7:
      guard let seat = ticks().first, leadSeats.contains(seat) else { finish(); return }
      owner.sheet.present(SheetModel(title: "Make \(seat) the lead?", note: "It hands out tasks to the others and is the one you talk to. You can change the lead later.",
                                     source: "memev bus operator \(seat)", main: "Make \(seat) the lead", onMain: { [weak self] host in
        self?.owner.agents.run(host, doing: "Making \(seat) the lead.", args: ["bus", "operator", seat], timeout: 30000,
                               done: "The lead is set", failed: "The lead was not set", onDone: { [weak self] in self?.finish() })
      }))
    default:
      apply(failure: "The check did not pass.")
    }
  }

  func finish() {
    memev_shim_guide_done()
    owner.inGuide = false
    owner.setPrimary("Refresh")
    owner.reload()
    owner.showPage(0)
  }
}
