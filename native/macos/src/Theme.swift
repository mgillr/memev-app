// SPDX-License-Identifier: Apache-2.0
// The rev2 colour tokens and type (native/REV2-SCREENS.md 1.12 and 1.14), with the exact hex of the mockup's latest values.
// Fonts: the design names Space Grotesk and IBM Plex Mono. Neither ships with macOS and this repository bundles no font
// files, so the window uses the system font and the system monospaced font at the design's sizes, weights and tracking.
import AppKit

func hex(_ v: UInt32, _ alpha: CGFloat = 1) -> NSColor {
  NSColor(srgbRed: CGFloat((v >> 16) & 0xFF) / 255, green: CGFloat((v >> 8) & 0xFF) / 255, blue: CGFloat(v & 0xFF) / 255, alpha: alpha)
}

struct Theme {
  let dark: Bool
  let page, panel, raise, line, rule, ctl, text, sub, dim, accent, accentFill, onAccent, good, warn, bad, series, sample: NSColor
  let scrim: NSColor
  let shadowOpacity: Float
  let shadowRadius: CGFloat
  let shadowY: CGFloat

  static let darkTokens = Theme(
    dark: true, page: hex(0x080B10), panel: hex(0x121822), raise: hex(0x1B222D), line: hex(0x2B3543), rule: hex(0x374252),
    ctl: hex(0x6B7789), text: hex(0xE8ECF2), sub: hex(0xA4ADBA), dim: hex(0x8A94A2), accent: hex(0xF2B544), accentFill: hex(0xF2B544),
    onAccent: hex(0x0A0D12), good: hex(0x5FD08F), warn: hex(0xF2B544), bad: hex(0xFF8A7A), series: hex(0x7F8B9A), sample: hex(0x5C6877),
    scrim: NSColor(srgbRed: 4 / 255, green: 6 / 255, blue: 9 / 255, alpha: 0.62), shadowOpacity: 0.5, shadowRadius: 24, shadowY: 16)

  static let lightTokens = Theme(
    dark: false, page: hex(0xEFF1F5), panel: hex(0xFFFFFF), raise: hex(0xE8EBF0), line: hex(0xD6DBE3), rule: hex(0xC3CAD4),
    ctl: hex(0x7C8797), text: hex(0x10151C), sub: hex(0x46505E), dim: hex(0x5F6978), accent: hex(0x8A5600), accentFill: hex(0xF2B544),
    onAccent: hex(0x0A0D12), good: hex(0x1D6B43), warn: hex(0x8A5600), bad: hex(0xB3261E), series: hex(0x8590A0), sample: hex(0xA3ADB9),
    scrim: NSColor(srgbRed: 16 / 255, green: 21 / 255, blue: 28 / 255, alpha: 0.32), shadowOpacity: 0.10, shadowRadius: 12, shadowY: 8)

  /// The theme in use. Every themed view reads it in applyTheme().
  static var current = darkTokens

  // Agent panes and the Source view use fixed dark colours in both themes (1.12).
  static let paneHeader = hex(0x1B222C), paneHeaderText = hex(0xE6E9EE), paneState = hex(0xB7BEC8), paneControl = hex(0x141A22)
  static let paneBody = hex(0x0B0F14), paneBodyText = hex(0xD3D8DF), paneBorder = hex(0x2A323D)
}

/// A colour by its token name, resolved against the current theme.
enum Role {
  case page, panel, raise, line, rule, ctl, text, sub, dim, accent, accentFill, onAccent, good, warn, bad, series, sample, clear

  var color: NSColor {
    let t = Theme.current
    switch self {
    case .page: return t.page
    case .panel: return t.panel
    case .raise: return t.raise
    case .line: return t.line
    case .rule: return t.rule
    case .ctl: return t.ctl
    case .text: return t.text
    case .sub: return t.sub
    case .dim: return t.dim
    case .accent: return t.accent
    case .accentFill: return t.accentFill
    case .onAccent: return t.onAccent
    case .good: return t.good
    case .warn: return t.warn
    case .bad: return t.bad
    case .series: return t.series
    case .sample: return t.sample
    case .clear: return .clear
    }
  }
}

/// The type scale of 1.14: size, line height, weight and tracking.
struct TypeStyle {
  let size: CGFloat
  let line: CGFloat
  let weight: NSFont.Weight
  let tracking: CGFloat // in points
  let mono: Bool

  var font: NSFont {
    mono ? NSFont.monospacedSystemFont(ofSize: size, weight: weight) : NSFont.systemFont(ofSize: size, weight: weight)
  }

  static let pageTitle = TypeStyle(size: 28, line: 36, weight: .medium, tracking: -0.56, mono: false)
  static let panelTitle = TypeStyle(size: 20, line: 28, weight: .medium, tracking: -0.2, mono: false)
  static let sentence = TypeStyle(size: 16, line: 24, weight: .regular, tracking: 0, mono: false)
  static let chartTitle = TypeStyle(size: 16, line: 24, weight: .medium, tracking: 0, mono: false)
  static let wordmark = TypeStyle(size: 16, line: 24, weight: .semibold, tracking: 0, mono: false)
  static let body = TypeStyle(size: 14, line: 20, weight: .regular, tracking: 0, mono: false)
  static let bodyMedium = TypeStyle(size: 14, line: 20, weight: .medium, tracking: 0, mono: false)
  static let small = TypeStyle(size: 12, line: 16, weight: .regular, tracking: 0, mono: false)
  static let smallMedium = TypeStyle(size: 12, line: 16, weight: .medium, tracking: 0, mono: false)
  static let tag = TypeStyle(size: 12, line: 20, weight: .medium, tracking: 0, mono: false)
  static let label = TypeStyle(size: 12, line: 16, weight: .medium, tracking: 0.72, mono: true) // 0.06em at 12 px
  static let code = TypeStyle(size: 12, line: 16, weight: .regular, tracking: 0, mono: true)
  static let codeBlock = TypeStyle(size: 12, line: 20, weight: .regular, tracking: 0, mono: true)
}

/// A view that repaints itself from Theme.current.
protocol Themed: AnyObject {
  func applyTheme()
}

/// Repaints every themed view under v.
func repaintTree(_ v: NSView) {
  (v as? Themed)?.applyTheme()
  for c in v.subviews { repaintTree(c) }
}
