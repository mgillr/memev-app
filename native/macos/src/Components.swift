// SPDX-License-Identifier: Apache-2.0
// The rev2 shell's parts as reusable views (native/REV2-SCREENS.md section 1): labels, status glyphs, buttons with the one-amber
// rule, menu entries, section labels, the header rule, tabs, the page header, lists, expander rows, the sample tag and
// "Coming later" rows. Every view repaints itself from Theme.current.
import AppKit

class FlippedView: NSView {
  override var isFlipped: Bool { true }
}

func vstack(_ views: [NSView] = [], spacing: CGFloat = 0, insets: NSEdgeInsets = NSEdgeInsetsZero) -> NSStackView {
  let s = NSStackView(views: views)
  s.orientation = .vertical
  s.alignment = .leading
  s.spacing = spacing
  s.edgeInsets = insets
  s.translatesAutoresizingMaskIntoConstraints = false
  return s
}

func hstack(_ views: [NSView] = [], spacing: CGFloat = 8) -> NSStackView {
  let s = NSStackView(views: views)
  s.orientation = .horizontal
  s.alignment = .centerY
  s.spacing = spacing
  s.translatesAutoresizingMaskIntoConstraints = false
  return s
}

/// Adds child to a vertical stack and makes it as wide as the stack's content.
func addFull(_ child: NSView, to stack: NSStackView) {
  stack.addArrangedSubview(child)
  child.widthAnchor.constraint(equalTo: stack.widthAnchor, constant: -(stack.edgeInsets.left + stack.edgeInsets.right)).isActive = true
}

func clear(_ stack: NSStackView) {
  for v in stack.arrangedSubviews { stack.removeArrangedSubview(v); v.removeFromSuperview() }
}

func spacer() -> NSView {
  let v = NSView()
  v.translatesAutoresizingMaskIntoConstraints = false
  v.setContentHuggingPriority(.init(1), for: .horizontal)
  v.setContentHuggingPriority(.init(1), for: .vertical)
  v.setContentCompressionResistancePriority(.init(1), for: .horizontal)
  return v
}

/// A plain rectangle in token colours: a fill, an optional border and a radius.
class Box: FlippedView, Themed {
  var fill: Role { didSet { applyTheme() } }
  var border: Role? { didSet { applyTheme() } }
  var radius: CGFloat
  var borderWidth: CGFloat = 1

  init(_ fill: Role, border: Role? = nil, radius: CGFloat = 0) {
    self.fill = fill
    self.border = border
    self.radius = radius
    super.init(frame: .zero)
    wantsLayer = true
    translatesAutoresizingMaskIntoConstraints = false
    applyTheme()
  }

  required init?(coder: NSCoder) { fatalError() }

  func applyTheme() {
    layer?.backgroundColor = fill.color.cgColor
    layer?.cornerRadius = radius
    layer?.borderWidth = border == nil ? 0 : borderWidth
    layer?.borderColor = border?.color.cgColor
  }
}

/// A 1 px line in the rule or line colour, horizontal unless asked otherwise.
func hairline(_ role: Role = .rule, vertical: Bool = false, length: CGFloat? = nil, thickness: CGFloat = 1) -> Box {
  let b = Box(role)
  if vertical {
    b.widthAnchor.constraint(equalToConstant: thickness).isActive = true
    if let l = length { b.heightAnchor.constraint(equalToConstant: l).isActive = true }
  } else {
    b.heightAnchor.constraint(equalToConstant: thickness).isActive = true
    if let l = length { b.widthAnchor.constraint(equalToConstant: l).isActive = true }
    b.setContentHuggingPriority(.init(1), for: .horizontal)
  }
  return b
}

/// A text label in one of the type styles and a token colour. Setting stringValue keeps the style.
final class Label: NSTextField, Themed {
  var style: TypeStyle = .body { didSet { render() } }
  var role: Role = .text { didSet { render() } }
  var underline = false { didSet { render() } }
  private var content = ""
  private var rendering = false

  init(_ text: String, _ style: TypeStyle = .body, _ role: Role = .text, wrap: CGFloat = 0) {
    super.init(frame: .zero)
    isEditable = false
    isSelectable = false
    isBordered = false
    drawsBackground = false
    translatesAutoresizingMaskIntoConstraints = false
    if wrap > 0 {
      cell?.wraps = true
      cell?.isScrollable = false
      usesSingleLineMode = false
      lineBreakMode = .byWordWrapping
      preferredMaxLayoutWidth = wrap
      setContentCompressionResistancePriority(.defaultLow, for: .horizontal)
    } else {
      lineBreakMode = .byTruncatingTail
      cell?.truncatesLastVisibleLine = true
      setContentCompressionResistancePriority(.init(250), for: .horizontal)
    }
    self.style = style
    self.role = role
    content = text
    render()
  }

  required init?(coder: NSCoder) { fatalError() }

  override var stringValue: String {
    get { super.stringValue }
    set {
      if rendering { super.stringValue = newValue; return }
      content = newValue
      render()
    }
  }

  func render() {
    if rendering { return }
    rendering = true
    let p = NSMutableParagraphStyle()
    p.minimumLineHeight = style.line
    p.maximumLineHeight = style.line
    p.lineBreakMode = lineBreakMode
    var a: [NSAttributedString.Key: Any] = [.font: style.font, .foregroundColor: role.color, .paragraphStyle: p, .kern: style.tracking]
    if underline { a[.underlineStyle] = NSUnderlineStyle.single.rawValue }
    attributedStringValue = NSAttributedString(string: content, attributes: a)
    rendering = false
  }

  func applyTheme() { render() }
}

enum GlyphKind: String {
  case ok, warn, bad, live, off
}

/// A status glyph (1.13): each state has its own shape as well as its colour. As a pip (1.2), fine is filled in ctl.
final class Glyph: NSView, Themed {
  var kind: GlyphKind { didSet { needsDisplay = true } }
  let size: CGFloat
  let pip: Bool

  init(_ kind: GlyphKind, size: CGFloat = 8, pip: Bool = false) {
    self.kind = kind
    self.size = size
    self.pip = pip
    super.init(frame: NSRect(x: 0, y: 0, width: size, height: size))
    translatesAutoresizingMaskIntoConstraints = false
    widthAnchor.constraint(equalToConstant: size).isActive = true
    heightAnchor.constraint(equalToConstant: size).isActive = true
    setContentHuggingPriority(.required, for: .horizontal)
  }

  required init?(coder: NSCoder) { fatalError() }

  override var intrinsicContentSize: NSSize { NSSize(width: size, height: size) }

  func applyTheme() { needsDisplay = true }

  override func draw(_ dirtyRect: NSRect) {
    let r = bounds
    switch kind {
    case .ok:
      (pip ? Role.ctl : Role.good).color.setFill()
      NSBezierPath(roundedRect: r, xRadius: pip ? 1 : 2, yRadius: pip ? 1 : 2).fill()
    case .warn:
      Role.warn.color.setFill()
      let p = NSBezierPath()
      p.move(to: NSPoint(x: r.midX, y: r.minY))
      p.line(to: NSPoint(x: r.maxX, y: r.midY))
      p.line(to: NSPoint(x: r.midX, y: r.maxY))
      p.line(to: NSPoint(x: r.minX, y: r.midY))
      p.close()
      p.fill()
    case .bad:
      Role.bad.color.setFill()
      let p = NSBezierPath()
      p.move(to: NSPoint(x: r.minX, y: r.minY))
      p.line(to: NSPoint(x: r.maxX, y: r.minY))
      p.line(to: NSPoint(x: r.midX, y: r.maxY))
      p.close()
      p.fill()
    case .live:
      Role.series.color.setFill()
      NSBezierPath(ovalIn: r).fill()
    case .off:
      Role.ctl.color.setStroke()
      let p = NSBezierPath(roundedRect: r.insetBy(dx: 0.5, dy: 0.5), xRadius: 1, yRadius: 1)
      p.lineWidth = 1
      p.stroke()
    }
  }
}

/// The wordmark's mark: a circle in text crossed by an amber line, 28 px.
final class Mark: NSView, Themed {
  override init(frame: NSRect) {
    super.init(frame: frame)
    translatesAutoresizingMaskIntoConstraints = false
    widthAnchor.constraint(equalToConstant: 28).isActive = true
    heightAnchor.constraint(equalToConstant: 28).isActive = true
  }

  required init?(coder: NSCoder) { fatalError() }

  func applyTheme() { needsDisplay = true }

  override func draw(_ dirtyRect: NSRect) {
    Role.text.color.setStroke()
    let c = NSBezierPath(ovalIn: bounds.insetBy(dx: 4, dy: 4))
    c.lineWidth = 2
    c.stroke()
    Role.accent.color.setFill()
    NSBezierPath(rect: NSRect(x: 1, y: bounds.midY - 1, width: bounds.width - 2, height: 2)).fill()
  }
}

enum ButtonStyle {
  case secondary, ghost, danger, link
}

/// A button in the rev2 look. It is never amber by itself: amberRank marks it as a candidate, and AmberRule picks at most one
/// visible candidate in a window or sheet to fill (SH-20). Rank 1 is a page action, 2 a row or empty-state button, 3 the stopped
/// banner's button or a sheet's main button.
final class MButton: NSButton, Themed {
  var base: ButtonStyle { didSet { applyTheme() } }
  var amberRank: Int
  var large: Bool
  var onPress: (() -> Void)?
  private(set) var amber = false
  var text: String { didSet { applyTheme(); invalidateIntrinsicContentSize() } }

  init(_ text: String, _ base: ButtonStyle = .secondary, rank: Int = 0, large: Bool = false, action: (() -> Void)? = nil) {
    self.base = base
    self.text = text
    self.amberRank = rank
    self.large = large
    self.onPress = action
    super.init(frame: .zero)
    isBordered = false
    wantsLayer = true
    translatesAutoresizingMaskIntoConstraints = false
    target = self
    self.action = #selector(fire)
    if base != .link { heightAnchor.constraint(equalToConstant: large ? 40 : 32).isActive = true }
    setContentHuggingPriority(.required, for: .horizontal)
    setContentCompressionResistancePriority(.required, for: .horizontal)
    applyTheme()
  }

  required init?(coder: NSCoder) { fatalError() }

  @objc func fire() { onPress?() }

  func setAmber(_ on: Bool) {
    if on == amber && (identifier?.rawValue == "primary") == on { return }
    amber = on
    identifier = on ? NSUserInterfaceItemIdentifier("primary") : nil
    applyTheme()
  }

  override var intrinsicContentSize: NSSize {
    let w = (attributedTitle.size().width).rounded(.up)
    let pad: CGFloat = base == .link ? 0 : large ? 16 : 12
    return NSSize(width: w + pad * 2, height: base == .link ? 16 : large ? 40 : 32)
  }

  func applyTheme() {
    layer?.cornerRadius = 8
    var fg = Role.text.color
    var font = TypeStyle.bodyMedium.font
    var underline = false
    if amber {
      // The mockup's 1 px accentRing edge (tpl.html:43): in light mode the fill alone is too faint against white (WCAG 1.4.11).
      layer?.backgroundColor = Role.accentFill.color.cgColor
      layer?.borderWidth = 1
      layer?.borderColor = Role.accentRing.color.cgColor
      fg = Role.onAccent.color
    } else {
      switch base {
      case .secondary:
        layer?.backgroundColor = Role.raise.color.cgColor
        layer?.borderWidth = 1
        layer?.borderColor = Role.line.color.cgColor
      case .ghost:
        layer?.backgroundColor = NSColor.clear.cgColor
        layer?.borderWidth = 0
        fg = Role.sub.color
      case .danger:
        layer?.backgroundColor = Role.raise.color.cgColor
        layer?.borderWidth = 1
        layer?.borderColor = Role.bad.color.cgColor
        fg = Role.bad.color
      case .link:
        layer?.backgroundColor = NSColor.clear.cgColor
        layer?.borderWidth = 0
        fg = Role.sub.color
        font = TypeStyle.small.font
        underline = true
      }
    }
    var a: [NSAttributedString.Key: Any] = [.foregroundColor: fg, .font: font]
    if underline { a[.underlineStyle] = NSUnderlineStyle.single.rawValue }
    attributedTitle = NSAttributedString(string: text, attributes: a)
    alphaValue = isEnabled ? 1 : 0.5
  }

  override var isEnabled: Bool { didSet { alphaValue = isEnabled ? 1 : 0.5 } }
}

/// SH-20: at most one filled amber button among the visible buttons under a root.
enum AmberRule {
  static func walk(_ v: NSView, _ f: (NSView) -> Void) {
    f(v)
    for c in v.subviews { walk(c, f) }
  }

  /// In the stopped state only the banner's button may be amber. Otherwise a row or empty-state button wins, and the last page
  /// action is amber only when none is visible and the screen is not marked noPrimary.
  static func enforce(_ root: NSView, stopped: Bool, noPrimary: Bool) {
    var all: [MButton] = []
    walk(root) { if let b = $0 as? MButton, b.amberRank > 0 { all.append(b) } }
    let shown = all.filter { !$0.isHiddenOrHasHiddenAncestor }
    var winner = shown.first { $0.amberRank == 3 }
    if winner == nil && !stopped {
      winner = shown.first { $0.amberRank == 2 } ?? (noPrimary ? nil : shown.last { $0.amberRank == 1 })
    }
    for b in all { b.setAmber(b === winner) }
  }

  /// Whether every filled amber button a person can see under root carries the 1 px accentRing edge.
  static func ringed(_ root: NSView) -> Bool {
    var ok = true
    let want = Role.accentRing.color.usingColorSpace(.sRGB)
    walk(root) { v in
      guard let b = v as? NSButton, b.identifier?.rawValue == "primary", !b.isHiddenOrHasHiddenAncestor else { return }
      let got = b.layer?.borderColor.flatMap { NSColor(cgColor: $0)?.usingColorSpace(.sRGB) }
      let same = got != nil && want != nil && abs(got!.redComponent - want!.redComponent) < 0.002
        && abs(got!.greenComponent - want!.greenComponent) < 0.002 && abs(got!.blueComponent - want!.blueComponent) < 0.002
      if b.layer?.borderWidth != 1 || !same { ok = false }
    }
    return ok
  }

  /// The filled amber buttons a person can see under root.
  static func visibleAmber(_ root: NSView) -> Int {
    var n = 0
    walk(root) { if let b = $0 as? NSButton, b.identifier?.rawValue == "primary", !b.isHiddenOrHasHiddenAncestor { n += 1 } }
    return n
  }
}

/// A button that is a whole area (a row, the health button, an expander's head): it draws only its subviews and its layer, and
/// a click anywhere in it that is not on an inner button presses it.
class ClickArea: NSButton {
  var onPress: (() -> Void)?

  override init(frame: NSRect) {
    super.init(frame: frame)
    isBordered = false
    title = ""
    imagePosition = .imageOnly
    wantsLayer = true
    translatesAutoresizingMaskIntoConstraints = false
    target = self
    action = #selector(fire)
    setContentHuggingPriority(.init(1), for: .horizontal)
  }

  required init?(coder: NSCoder) { fatalError() }

  @objc func fire() { onPress?() }

  override func draw(_ dirtyRect: NSRect) {}

  override var intrinsicContentSize: NSSize { NSSize(width: NSView.noIntrinsicMetric, height: NSView.noIntrinsicMetric) }

  override func hitTest(_ point: NSPoint) -> NSView? {
    guard let v = super.hitTest(point) else { return nil }
    var x: NSView? = v
    while let c = x, c !== self {
      if c is NSButton || (c is NSControl && !(c is NSTextField)) { return c }
      x = c.superview
    }
    return self
  }

  /// Pins content inside with these insets.
  func hold(_ content: NSView, _ i: NSEdgeInsets) {
    addSubview(content)
    NSLayoutConstraint.activate([
      content.leadingAnchor.constraint(equalTo: leadingAnchor, constant: i.left),
      content.trailingAnchor.constraint(equalTo: trailingAnchor, constant: -i.right),
      content.topAnchor.constraint(equalTo: topAnchor, constant: i.top),
      content.bottomAnchor.constraint(equalTo: bottomAnchor, constant: -i.bottom),
    ])
  }
}

/// A menu entry (SH-3): 32 px, radius 8; selected is on raise at weight 500 with a 2 px amber line at its left edge. A needs-you
/// entry carries the warn diamond. indent is 28 for a project entry under Projects.
final class NavButton: NSButton, Themed {
  let edge = Box(.accent)
  let diamond = Glyph(.warn)
  var selectedState = false { didSet { applyTheme() } }
  var needsYou = false { didSet { diamond.isHidden = !needsYou } }
  var indent: CGFloat = 0
  var text = "" { didSet { applyTheme() } }

  convenience init(title: String, target: AnyObject?, action: Selector?) {
    self.init(frame: .zero)
    text = title
    self.target = target
    self.action = action
    isBordered = false
    alignment = .left
    wantsLayer = true
    translatesAutoresizingMaskIntoConstraints = false
    heightAnchor.constraint(greaterThanOrEqualToConstant: 32).isActive = true
    edge.isHidden = true
    addSubview(edge)
    diamond.isHidden = true
    addSubview(diamond)
    NSLayoutConstraint.activate([
      edge.leadingAnchor.constraint(equalTo: leadingAnchor), edge.topAnchor.constraint(equalTo: topAnchor, constant: 6),
      edge.bottomAnchor.constraint(equalTo: bottomAnchor, constant: -6), edge.widthAnchor.constraint(equalToConstant: 2),
      diamond.trailingAnchor.constraint(equalTo: trailingAnchor, constant: -12), diamond.centerYAnchor.constraint(equalTo: centerYAnchor),
    ])
    applyTheme()
  }

  private var painting = false

  override var title: String {
    get { super.title }
    set { if painting { super.title = newValue } else if newValue != text { text = newValue } }
  }

  /// Kept for the guide's choice buttons.
  func paint(_ t: Theme) { applyTheme() }

  func applyTheme() {
    if painting { return }
    painting = true
    defer { painting = false }
    let style = NSMutableParagraphStyle()
    style.alignment = .left
    style.firstLineHeadIndent = 12 + indent
    style.headIndent = 12 + indent
    style.tailIndent = -24
    style.lineBreakMode = .byWordWrapping
    attributedTitle = NSAttributedString(string: text, attributes: [
      .foregroundColor: selectedState ? Role.text.color : Role.sub.color,
      .font: selectedState ? TypeStyle.bodyMedium.font : TypeStyle.body.font, .paragraphStyle: style,
    ])
    layer?.cornerRadius = 8
    layer?.backgroundColor = selectedState ? Role.raise.color.cgColor : NSColor.clear.cgColor
    edge.isHidden = !selectedState
    edge.applyTheme()
    diamond.needsDisplay = true
  }
}

/// SH-19: "Sample, not built yet", 20 px high on raise.
func sampleTag() -> NSView {
  let b = Box(.raise, radius: 4)
  let l = Label("Sample, not built yet", .tag, .sub)
  b.addSubview(l)
  NSLayoutConstraint.activate([
    b.heightAnchor.constraint(equalToConstant: 20),
    l.leadingAnchor.constraint(equalTo: b.leadingAnchor, constant: 8), l.trailingAnchor.constraint(equalTo: b.trailingAnchor, constant: -8),
    l.centerYAnchor.constraint(equalTo: b.centerYAnchor),
  ])
  b.setContentHuggingPriority(.required, for: .horizontal)
  return b
}

/// SH-17: a 16 by 2 px amber line, 12 px gap, the label in mono 12 at 0.06em in sub, the sample tag when the block has one, a
/// 1 px rule to the right edge, and a "Show all (n)" link when the list has more rows.
final class SectionLabel: NSStackView {
  let text: Label
  var link: MButton?

  init(_ label: String, sample: Bool = false, link: MButton? = nil, lineWidth: CGFloat = 16, textRole: Role = .sub) {
    text = Label(label, .label, textRole)
    self.link = link
    super.init(frame: .zero)
    orientation = .horizontal
    alignment = .centerY
    spacing = 12
    translatesAutoresizingMaskIntoConstraints = false
    let line = Box(.accent)
    line.widthAnchor.constraint(equalToConstant: lineWidth).isActive = true
    line.heightAnchor.constraint(equalToConstant: 2).isActive = true
    addArrangedSubview(line)
    addArrangedSubview(text)
    text.setContentCompressionResistancePriority(.required, for: .horizontal)
    if sample { addArrangedSubview(sampleTag()) }
    addArrangedSubview(hairline(.rule))
    if let l = link { addArrangedSubview(l) }
  }

  required init?(coder: NSCoder) { fatalError() }
}

/// SH-15: on a screen with no tabs, a 48 px 2 px amber line, then a 1 px rule to the right edge.
final class HeaderRule: NSStackView {
  init() {
    super.init(frame: .zero)
    orientation = .horizontal
    alignment = .bottom
    spacing = 0
    translatesAutoresizingMaskIntoConstraints = false
    let line = Box(.accent)
    line.widthAnchor.constraint(equalToConstant: 48).isActive = true
    line.heightAnchor.constraint(equalToConstant: 2).isActive = true
    addArrangedSubview(line)
    addArrangedSubview(hairline(.rule))
  }

  required init?(coder: NSCoder) { fatalError() }
}

struct TabItem {
  let id: String
  var label: String
  var needsYou = false
}

/// SH-16: text-only tabs, 40 px high, 24 px apart, one 1 px rule under the strip; the selected tab is text at weight 500 with a
/// 2 px amber underline. A tab that needs the person carries the warn diamond.
final class TabStrip: NSView {
  private let row = hstack([], spacing: 24)
  private(set) var buttons: [(String, TabButton)] = []
  var onSelect: ((String) -> Void)?

  override init(frame: NSRect) {
    super.init(frame: frame)
    translatesAutoresizingMaskIntoConstraints = false
    row.alignment = .bottom
    let rule = hairline(.rule)
    addSubview(rule)
    addSubview(row)
    NSLayoutConstraint.activate([
      heightAnchor.constraint(equalToConstant: 41),
      row.leadingAnchor.constraint(equalTo: leadingAnchor), row.topAnchor.constraint(equalTo: topAnchor),
      row.heightAnchor.constraint(equalToConstant: 40),
      rule.leadingAnchor.constraint(equalTo: leadingAnchor), rule.trailingAnchor.constraint(equalTo: trailingAnchor),
      rule.bottomAnchor.constraint(equalTo: bottomAnchor),
    ])
  }

  required init?(coder: NSCoder) { fatalError() }

  func set(_ tabs: [TabItem], selected: String) {
    clear(row)
    buttons = []
    for t in tabs {
      let b = TabButton(t.label, needsYou: t.needsYou)
      b.selectedState = t.id == selected
      b.onPress = { [weak self] in self?.onSelect?(t.id) }
      b.setAccessibilityLabel(t.label)
      row.addArrangedSubview(b)
      buttons.append((t.id, b))
    }
  }

  var labels: [String] { buttons.map { $0.1.label.stringValue } }
}

final class TabButton: ClickArea, Themed {
  let label: Label
  let underline = Box(.accent)
  let diamond = Glyph(.warn)
  var selectedState = false { didSet { applyTheme() } }

  init(_ text: String, needsYou: Bool) {
    label = Label(text, .body, .sub)
    super.init(frame: .zero)
    title = text
    let row = hstack([label, diamond], spacing: 6)
    diamond.isHidden = !needsYou
    addSubview(row)
    addSubview(underline)
    NSLayoutConstraint.activate([
      heightAnchor.constraint(equalToConstant: 40),
      row.leadingAnchor.constraint(equalTo: leadingAnchor), row.trailingAnchor.constraint(equalTo: trailingAnchor),
      row.centerYAnchor.constraint(equalTo: centerYAnchor),
      underline.leadingAnchor.constraint(equalTo: leadingAnchor), underline.trailingAnchor.constraint(equalTo: trailingAnchor),
      underline.bottomAnchor.constraint(equalTo: bottomAnchor), underline.heightAnchor.constraint(equalToConstant: 2),
    ])
    applyTheme()
  }

  required init?(coder: NSCoder) { fatalError() }

  func applyTheme() {
    label.style = selectedState ? .bodyMedium : .body
    label.role = selectedState ? .text : .sub
    underline.isHidden = !selectedState
  }
}

/// SH-11 to SH-16: title, "What is this?", up to two page actions, the sentence, then the header rule or the tab strip.
final class PageHeader: NSStackView {
  let title = Label("", .pageTitle, .text)
  let sentence = Label("", .sentence, .sub, wrap: 640)
  let what = MButton("What is this?", .ghost)
  let actions = hstack([], spacing: 8)
  let rule = HeaderRule()
  let tabs = TabStrip()

  init() {
    super.init(frame: .zero)
    orientation = .vertical
    alignment = .leading
    spacing = 8
    translatesAutoresizingMaskIntoConstraints = false
    title.setContentCompressionResistancePriority(.defaultLow, for: .horizontal)
    let top = hstack([title, spacer(), what, actions], spacing: 12)
    addFull(top, to: self)
    addArrangedSubview(sentence)
    setCustomSpacing(24, after: sentence)
    addFull(rule, to: self)
    addFull(tabs, to: self)
  }

  required init?(coder: NSCoder) { fatalError() }

  /// Shows the tabs when there are any, else the header rule. At most two actions are kept.
  func configure(title t: String, sentence s: String, tabs list: [TabItem], selected: String, showWhat: Bool, actions acts: [MButton]) {
    title.stringValue = t
    sentence.stringValue = s
    sentence.isHidden = s.isEmpty
    what.isHidden = !showWhat
    clear(actions)
    for a in acts.prefix(2) { actions.addArrangedSubview(a) }
    rule.isHidden = !list.isEmpty
    tabs.isHidden = list.isEmpty
    tabs.set(list, selected: selected)
  }
}

/// A list on panel with a 1 px line round it, radius 12, padding 4 12; rows inside are split by a 1 px line (1.1).
final class ListBox: Box {
  let stack = vstack()

  init() {
    super.init(.panel, border: .line, radius: 12)
    addSubview(stack)
    NSLayoutConstraint.activate([
      stack.leadingAnchor.constraint(equalTo: leadingAnchor, constant: 12), stack.trailingAnchor.constraint(equalTo: trailingAnchor, constant: -12),
      stack.topAnchor.constraint(equalTo: topAnchor, constant: 4), stack.bottomAnchor.constraint(equalTo: bottomAnchor, constant: -4),
    ])
  }

  required init?(coder: NSCoder) { fatalError() }

  func add(_ row: NSView) {
    if !stack.arrangedSubviews.isEmpty { addFull(hairline(.line), to: stack) }
    addFull(row, to: stack)
  }

  var rows: [RowView] { stack.arrangedSubviews.compactMap { $0 as? RowView } }
}

/// A list row: at least 44 px high (36 in the detail panel), padding 12, a glyph, a title, a second line, a command line in mono
/// when technical detail is on, a figure on the right and a button. A selected row is on raise with a 1 px inset text ring.
final class RowView: ClickArea, Themed {
  let titleLabel: Label
  let subLabel: Label
  let codeLabel: Label
  var selectedState = false { didSet { applyTheme() } }

  init(glyph: GlyphKind?, title: String, sub: String = "", code: String = "", figure: String = "", button: MButton? = nil,
       minHeight: CGFloat = 44, tech: Bool = false, wrap: CGFloat = 0) {
    titleLabel = Label(title, .bodyMedium, .text, wrap: wrap)
    subLabel = Label(sub, .small, .sub, wrap: wrap)
    codeLabel = Label(code, .code, .dim)
    super.init(frame: .zero)
    self.title = title
    subLabel.isHidden = sub.isEmpty
    codeLabel.isHidden = code.isEmpty || !tech
    let words = vstack([titleLabel, subLabel, codeLabel], spacing: 2)
    var parts: [NSView] = []
    if let g = glyph { parts.append(Glyph(g)) }
    parts.append(words)
    parts.append(spacer())
    if !figure.isEmpty {
      let f = Label(figure, .body, .text)
      f.font = Fonts.sans(14, .regular, tabular: true)
      parts.append(f)
    }
    if let b = button { parts.append(b) }
    let row = hstack(parts, spacing: 12)
    hold(row, NSEdgeInsets(top: 12, left: 8, bottom: 12, right: 8))
    heightAnchor.constraint(greaterThanOrEqualToConstant: minHeight).isActive = true
    applyTheme()
  }

  required init?(coder: NSCoder) { fatalError() }

  func applyTheme() {
    layer?.cornerRadius = 8
    layer?.backgroundColor = selectedState ? Role.raise.color.cgColor : NSColor.clear.cgColor
    layer?.borderWidth = selectedState ? 1 : 0
    layer?.borderColor = Role.text.color.cgColor
  }
}

/// The expander's chevron: it points right when closed and down when open.
final class Chevron: NSView, Themed {
  var open = false { didSet { needsDisplay = true } }

  override init(frame: NSRect) {
    super.init(frame: frame)
    translatesAutoresizingMaskIntoConstraints = false
    widthAnchor.constraint(equalToConstant: 12).isActive = true
    heightAnchor.constraint(equalToConstant: 12).isActive = true
  }

  required init?(coder: NSCoder) { fatalError() }

  func applyTheme() { needsDisplay = true }

  override func draw(_ dirtyRect: NSRect) {
    Role.sub.color.setStroke()
    let p = NSBezierPath()
    p.lineWidth = 1.5
    if open {
      p.move(to: NSPoint(x: 2, y: 8)); p.line(to: NSPoint(x: 6, y: 4)); p.line(to: NSPoint(x: 10, y: 8))
    } else {
      p.move(to: NSPoint(x: 4, y: 10)); p.line(to: NSPoint(x: 8, y: 6)); p.line(to: NSPoint(x: 4, y: 2))
    }
    p.stroke()
  }
}

/// SH-18: a 1 px rule above, a chevron, a title at weight 500 and a summary in sub. Its contents open 24 px indented.
final class ExpanderRow: NSStackView {
  let head = ClickArea()
  let chevron = Chevron()
  let body = vstack([], spacing: 12, insets: NSEdgeInsets(top: 0, left: 24, bottom: 0, right: 0))
  var isOpen = false { didSet { chevron.open = isOpen; body.isHidden = !isOpen; onToggle?() } }
  var onToggle: (() -> Void)?

  init(title: String, summary: String) {
    super.init(frame: .zero)
    orientation = .vertical
    alignment = .leading
    spacing = 12
    translatesAutoresizingMaskIntoConstraints = false
    let t = Label(title, .bodyMedium, .text)
    let s = Label(summary, .body, .sub)
    head.title = title
    head.hold(hstack([chevron, t, s, spacer()], spacing: 12), NSEdgeInsets(top: 12, left: 0, bottom: 4, right: 0))
    head.onPress = { [weak self] in self?.isOpen.toggle() }
    addFull(hairline(.rule), to: self)
    addFull(head, to: self)
    addFull(body, to: self)
    body.isHidden = true
  }

  required init?(coder: NSCoder) { fatalError() }
}

/// One "Coming later" entry: fixed text that runs no command (SH-22).
struct LaterItem {
  let title: String
  let sub: String
  let why: String
  let plan: String // "PLANNED · <contract>", "NOT SCOPED · needs a contract" or "DRAFT · <spec>"
  let source: String
}

/// SH-22: an expander "Coming later (n)" with a list of hollow-glyph rows; each opens a panel tagged as a sample.
func comingLater(_ items: [LaterItem], open: @escaping (LaterItem, RowView) -> Void) -> ExpanderRow {
  let e = ExpanderRow(title: "Coming later (\(items.count))", summary: "not built yet; each opens a sample of what is planned")
  let list = ListBox()
  for it in items {
    let r = RowView(glyph: .off, title: it.title, sub: it.sub)
    r.onPress = { [weak r] in if let r = r { open(it, r) } }
    list.add(r)
  }
  addFull(list, to: e.body)
  return e
}
