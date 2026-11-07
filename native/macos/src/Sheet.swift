// SPDX-License-Identifier: Apache-2.0
// SH-26 and 1.16: a sheet is a focused form over the window, 600 px wide at most, radius 12, padding 24, 72 px from the top,
// over a scrim. Focus moves in, the rest of the window takes no clicks, Tab stays inside, Esc or a click outside closes it.
// In a sheet the main button is amber; in a confirmation both buttons look the same and the main one has a red ring and text.
import AppKit

struct SheetModel {
  var title: String
  var sample = false
  var note = ""
  var fields: [NSView] = []
  var rows: [DetailRow] = []
  var notice = ""
  var source = ""
  var main: String
  var second: String? = nil
  var confirm = false
  var onMain: (SheetHost) -> Void = { $0.dismiss() }
  var onSecond: (SheetHost) -> Void = { $0.dismiss() }
}

/// The confirmation sheets of 1.16. Each names what changes. The window runs none of these commands yet; a caller supplies what
/// the main button does once the command is in the window's contract.
enum ConfirmKind: String, CaseIterable {
  case revoke, replace, rmpc, rmpath, uninstall, purge, stop, rotate, compact, disconnect, drop, cancel, mark, switchoff

  func title(_ subject: String) -> String {
    switch self {
    case .revoke: return "Revoke the key for \(subject)?"
    case .replace: return "Replace the key for \(subject)?"
    case .rmpc: return "Remove \(subject)?"
    case .rmpath: return "Remove the memev command from your PATH?"
    case .uninstall: return "Remove Memev?"
    case .purge: return "Also remove the runtime and launcher?"
    case .stop: return "Stop Memev?"
    case .rotate: return "Make a new web-access key?"
    case .compact: return "Tidy old history files?"
    case .disconnect: return "Disconnect \(subject)?"
    case .drop: return "Drop \u{201C}\(subject)\u{201D}?"
    case .cancel: return "Cancel \u{201C}\(subject)\u{201D}?"
    case .mark: return "Mark \u{201C}\(subject)\u{201D} as checked?"
    case .switchoff: return "Switch off Checked work?"
    }
  }

  var note: String {
    switch self {
    case .revoke: return "It stops working at once on this Mac. You can issue a new key at any time."
    case .replace: return "The old key stops working, and its holder needs the new one."
    case .rmpc: return "It stops reaching your memory, and one computer on your plan comes free. You can add it again later with a new code."
    case .rmpath: return "Terminals and AI apps can no longer call memev by name until you add it back."
    case .uninstall: return "This disconnects every app, stops Memev and removes the login service. Your notes stay in ~/memev."
    case .purge: return "This does everything Uninstall does, then removes the runtime and launcher as well. To use Memev again you would install it again."
    case .stop: return "Your AI apps lose their shared memory until it starts again."
    case .rotate: return "This cuts off anyone holding the old address."
    case .compact: return "This frees very little disk space and stops Memev for a moment."
    case .disconnect: return "It loses its shared memory until you connect it again."
    case .drop: return "It leaves your memory and stops being recalled."
    case .cancel: return "The task stops here. You give a reason, and it is kept with the task."
    case .mark: return "The lead only, with evidence, on this computer."
    case .switchoff: return "Nothing is held or blocked while it is off."
    }
  }

  var main: String {
    switch self {
    case .revoke: return "Revoke key"
    case .replace: return "Replace key"
    case .rmpc: return "Remove computer"
    case .rmpath: return "Remove command"
    case .uninstall: return "Uninstall"
    case .purge: return "Purge"
    case .stop: return "Stop Memev"
    case .rotate: return "Rotate key"
    case .compact: return "Compact"
    case .disconnect: return "Disconnect"
    case .drop: return "Drop"
    case .cancel: return "Cancel the task"
    case .mark: return "Mark as checked"
    case .switchoff: return "Switch off"
    }
  }

  func sheet(_ subject: String, onConfirm: @escaping (SheetHost) -> Void) -> SheetModel {
    SheetModel(title: title(subject), note: note, main: main, confirm: true, onMain: onConfirm)
  }
}

/// The scrim and the sheet over the window's content.
final class SheetHost: NSView, Themed {
  let panel = Box(.panel, border: .line, radius: 12)
  let stack = vstack([], spacing: 16, insets: NSEdgeInsets(top: 24, left: 24, bottom: 24, right: 24))
  let status = Label("", .small, .sub, wrap: 552)
  private(set) var model: SheetModel?
  private var returnFocus: NSResponder?
  var mainButton: MButton?
  var otherButtons: [MButton] = []

  override init(frame: NSRect) {
    super.init(frame: frame)
    wantsLayer = true
    translatesAutoresizingMaskIntoConstraints = false
    panel.addSubview(stack)
    addSubview(panel)
    NSLayoutConstraint.activate([
      panel.topAnchor.constraint(equalTo: topAnchor, constant: 72), panel.centerXAnchor.constraint(equalTo: centerXAnchor),
      panel.widthAnchor.constraint(lessThanOrEqualToConstant: 600), panel.widthAnchor.constraint(lessThanOrEqualTo: widthAnchor, constant: -48),
      { let c = panel.widthAnchor.constraint(equalToConstant: 600); c.priority = .defaultHigh; return c }(),
      stack.leadingAnchor.constraint(equalTo: panel.leadingAnchor), stack.trailingAnchor.constraint(equalTo: panel.trailingAnchor),
      stack.topAnchor.constraint(equalTo: panel.topAnchor), stack.bottomAnchor.constraint(equalTo: panel.bottomAnchor),
    ])
    isHidden = true
  }

  required init?(coder: NSCoder) { fatalError() }

  override var isFlipped: Bool { true }

  func applyTheme() {
    let t = Theme.current
    layer?.backgroundColor = t.scrim.cgColor
    panel.layer?.shadowColor = NSColor.black.cgColor
    panel.layer?.shadowOpacity = t.shadowOpacity
    panel.layer?.shadowRadius = t.shadowRadius
    panel.layer?.shadowOffset = NSSize(width: 0, height: -t.shadowY)
    panel.layer?.masksToBounds = false
  }

  var isOpen: Bool { !isHidden }

  func present(_ m: SheetModel) {
    model = m
    clear(stack)
    let w: CGFloat = 552
    addFull(Label(m.title, .panelTitle, .text, wrap: w), to: stack)
    if m.sample { stack.addArrangedSubview(sampleTag()) }
    if !m.note.isEmpty { addFull(Label(m.note, .body, .sub, wrap: w), to: stack) }
    for f in m.fields { addFull(f, to: stack) }
    for r in m.rows { addFull(RowView(glyph: r.glyph, title: r.title, sub: r.sub, figure: r.figure, minHeight: 36), to: stack) }
    if !m.notice.isEmpty {
      let box = Box(.raise, radius: 8)
      let l = Label(m.notice, .body, .text, wrap: w - 24)
      box.addSubview(l)
      NSLayoutConstraint.activate([
        l.leadingAnchor.constraint(equalTo: box.leadingAnchor, constant: 12), l.trailingAnchor.constraint(equalTo: box.trailingAnchor, constant: -12),
        l.topAnchor.constraint(equalTo: box.topAnchor, constant: 12), l.bottomAnchor.constraint(equalTo: box.bottomAnchor, constant: -12),
      ])
      addFull(box, to: stack)
    }
    if !m.source.isEmpty && (NSApp.delegate as? AppDelegate)?.technical == true { addFull(Label(m.source, .codeBlock, .dim, wrap: w), to: stack) }
    status.stringValue = ""
    addFull(status, to: stack)
    let cancel = MButton("Cancel", .secondary) { [weak self] in self?.dismiss() }
    var buttons: [MButton] = [cancel]
    if let s = m.second {
      let b = MButton(s, .secondary) { [weak self] in if let me = self { m.onSecond(me) } }
      buttons.append(b)
    }
    let main = MButton(m.main, m.confirm ? .danger : .secondary, rank: m.confirm ? 0 : 3) { [weak self] in if let me = self { m.onMain(me) } }
    buttons.append(main)
    mainButton = main
    otherButtons = Array(buttons.dropLast())
    addFull(hstack([spacer()] + buttons, spacing: 8), to: stack)
    // A sheet has its own amber rule: its main button, never in a confirmation.
    AmberRule.enforce(stack, stopped: false, noPrimary: m.confirm)
    isHidden = false
    repaintTree(self)
    returnFocus = window?.firstResponder
    // Tab stays inside: the sheet's controls form a closed loop.
    var controls: [NSView] = []
    AmberRule.walk(stack) { v in if let c = v as? NSControl, c.acceptsFirstResponder, !(c is Label), !(c is ClickArea) { controls.append(c) } }
    for (i, c) in controls.enumerated() { c.nextKeyView = controls[(i + 1) % controls.count] }
    window?.makeFirstResponder(controls.first)
  }

  func dismiss() {
    isHidden = true
    clear(stack)
    model = nil
    mainButton = nil
    otherButtons = []
    if let r = returnFocus { window?.makeFirstResponder(r) }
    returnFocus = nil
  }

  // The rest of the window takes no clicks while a sheet is open; a click outside the sheet closes it.
  override func hitTest(_ point: NSPoint) -> NSView? {
    if isHidden { return nil }
    return super.hitTest(point) ?? self
  }

  override func mouseDown(with event: NSEvent) {
    let p = convert(event.locationInWindow, from: nil)
    if !panel.frame.contains(p) { dismiss() }
  }
}
