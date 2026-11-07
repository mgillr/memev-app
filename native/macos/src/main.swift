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

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.setActivationPolicy(.regular)
app.run()
