// SPDX-License-Identifier: Apache-2.0
import AppKit

if CommandLine.arguments.count > 1 && CommandLine.arguments[1] == "--version" {
  print("memev-desktop 0.0.1-slice1")
  exit(0)
}

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.setActivationPolicy(.regular)
app.run()
