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
    for v in [text, choices, entry, apps, backupTick, detail, back] as [NSView] { view.addArrangedSubview(v) }
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
    back.isHidden = s == 0
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
        self.finish()
      }
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
