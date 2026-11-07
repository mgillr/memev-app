// SPDX-License-Identifier: Apache-2.0
import AppKit

if CommandLine.arguments.count > 1 && CommandLine.arguments[1] == "--version" {
  print("memev-desktop 0.0.1-slice1")
  exit(0)
}

// An installed app carries its own core: use it unless MEMEV_BIN names another.
if ProcessInfo.processInfo.environment["MEMEV_BIN"] == nil,
   let bundled = Bundle.main.resourceURL?.appendingPathComponent("bin/memev").path,
   FileManager.default.isExecutableFile(atPath: bundled) {
  setenv("MEMEV_BIN", bundled, 1)
}

// The design's fonts, registered before any view is made; the system fonts stand in when they cannot be.
Fonts.register()

// The fonts in use, printed with no window: the self-check reads them from a copy of the binary with no fonts beside it.
if CommandLine.arguments.count > 1 && CommandLine.arguments[1] == "--fonts" {
  print("font_sans=\(TypeStyle.body.font.familyName ?? "")")
  print("font_mono=\(TypeStyle.label.font.familyName ?? "")")
  print("font_weights=\(Fonts.weightOf(TypeStyle.body.font)),\(Fonts.weightOf(TypeStyle.bodyMedium.font)),\(Fonts.weightOf(TypeStyle.wordmark.font))")
  print("font_mono_faces=\(TypeStyle.code.font.fontName),\(TypeStyle.label.font.fontName)")
  exit(0)
}

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.setActivationPolicy(.regular)
app.run()
