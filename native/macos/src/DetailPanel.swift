// SPDX-License-Identifier: Apache-2.0
// SH-21: the 400 px detail panel on the right. Parts in order: title and close, the sample tag, the state, why, up to two
// buttons, facts, groups, and TECHNICAL pinned at the foot when technical detail is on.
import AppKit

struct DetailFact {
  let name: String
  let value: String
}

struct DetailRow {
  var glyph: GlyphKind? = nil
  let title: String
  var sub = ""
  var figure = ""
}

struct DetailGroup {
  let label: String
  let rows: [DetailRow]
}

struct DetailButton {
  let label: String
  let press: () -> Void
}

struct DetailModel {
  /// What was clicked; a second click on the same key closes the panel.
  var key: String
  var title: String
  var sample = false
  var state: (GlyphKind, String)? = nil
  var why = ""
  var buttons: [DetailButton] = []
  var facts: [DetailFact] = []
  var groups: [DetailGroup] = []
  var plan = ""
  var source = ""
}

final class DetailPanel: Box {
  let content = vstack([], spacing: 24, insets: NSEdgeInsets(top: 24, left: 24, bottom: 24, right: 24))
  let scroll = NSScrollView()
  let tech = vstack([], spacing: 8, insets: NSEdgeInsets(top: 16, left: 24, bottom: 24, right: 24))
  let close = MButton("✕", .ghost)
  private(set) var model: DetailModel?
  var onClose: (() -> Void)?
  var titleLabel: Label?

  init() {
    super.init(.panel)
    let doc = FlippedView()
    doc.translatesAutoresizingMaskIntoConstraints = false
    doc.addSubview(content)
    scroll.documentView = doc
    scroll.hasVerticalScroller = true
    scroll.drawsBackground = false
    scroll.translatesAutoresizingMaskIntoConstraints = false
    addSubview(scroll)
    addSubview(tech)
    close.setAccessibilityLabel("Close details")
    close.toolTip = "Close details"
    close.onPress = { [weak self] in self?.onClose?() }
    NSLayoutConstraint.activate([
      widthAnchor.constraint(equalToConstant: 400),
      scroll.leadingAnchor.constraint(equalTo: leadingAnchor), scroll.trailingAnchor.constraint(equalTo: trailingAnchor),
      scroll.topAnchor.constraint(equalTo: topAnchor), scroll.bottomAnchor.constraint(equalTo: tech.topAnchor),
      tech.leadingAnchor.constraint(equalTo: leadingAnchor), tech.trailingAnchor.constraint(equalTo: trailingAnchor),
      tech.bottomAnchor.constraint(equalTo: bottomAnchor),
      doc.leadingAnchor.constraint(equalTo: scroll.contentView.leadingAnchor), doc.trailingAnchor.constraint(equalTo: scroll.contentView.trailingAnchor),
      doc.topAnchor.constraint(equalTo: scroll.contentView.topAnchor),
      content.leadingAnchor.constraint(equalTo: doc.leadingAnchor), content.trailingAnchor.constraint(equalTo: doc.trailingAnchor),
      content.topAnchor.constraint(equalTo: doc.topAnchor), content.bottomAnchor.constraint(equalTo: doc.bottomAnchor),
    ])
  }

  required init?(coder: NSCoder) { fatalError() }

  func show(_ m: DetailModel, technical: Bool) {
    model = m
    clear(content)
    clear(tech)
    let width: CGFloat = 352
    let title = Label(m.title, .panelTitle, .text, wrap: 300)
    titleLabel = title
    addFull(hstack([title, spacer(), close], spacing: 12), to: content)
    if m.sample { content.addArrangedSubview(sampleTag()) }
    if let st = m.state {
      content.addArrangedSubview(hstack([Glyph(st.0), Label(st.1, .bodyMedium, st.0 == .bad ? .bad : .text)], spacing: 8))
    }
    if !m.why.isEmpty { content.addArrangedSubview(Label(m.why, .body, .sub, wrap: width)) }
    if !m.buttons.isEmpty {
      content.addArrangedSubview(hstack(m.buttons.prefix(2).map { b in MButton(b.label, .secondary, action: b.press) }, spacing: 8))
    }
    if !m.facts.isEmpty {
      let facts = vstack()
      for f in m.facts {
        let name = Label(f.name, .body, .sub)
        name.widthAnchor.constraint(equalToConstant: 120).isActive = true
        let row = hstack([name, Label(f.value, .body, .text, wrap: width - 132)], spacing: 12)
        row.alignment = .firstBaseline
        row.heightAnchor.constraint(greaterThanOrEqualToConstant: 36).isActive = true
        addFull(row, to: facts)
      }
      addFull(facts, to: content)
    }
    for g in m.groups {
      let block = vstack([], spacing: 4)
      addFull(SectionLabel(g.label), to: block)
      for r in g.rows { addFull(RowView(glyph: r.glyph, title: r.title, sub: r.sub, figure: r.figure, minHeight: 36, wrap: 300), to: block) }
      addFull(block, to: content)
    }
    tech.isHidden = !technical || (m.plan.isEmpty && m.source.isEmpty)
    if !tech.isHidden {
      let copy = MButton("Copy", .secondary) { [m] in
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString([m.plan, m.source].filter { !$0.isEmpty }.joined(separator: "\n"), forType: .string)
      }
      addFull(hairline(.rule), to: tech)
      addFull(hstack([Label("TECHNICAL", .label, .sub), spacer(), copy], spacing: 12), to: tech)
      if !m.plan.isEmpty { tech.addArrangedSubview(Label(m.plan, .codeBlock, .dim, wrap: width)) }
      if !m.source.isEmpty { tech.addArrangedSubview(Label(m.source, .codeBlock, .dim, wrap: width)) }
    }
    repaintTree(self)
  }
}
