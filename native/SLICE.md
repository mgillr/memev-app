# First native slice

Ownership record A10895, 7 Nov 2026, 11:36:23 Europe/Zurich. Baseline of this repository: `7bd85e1222634bf99bae51b7c136913b05c2ad31`.

This change is the first runnable slice of the native app. Installers, release workflows and release tarballs are a different lane and are not in this repository's change.

## Toolkit

The lead note of 5 Nov 2026, 21:51Z, keeps a native toolkit per platform and sets aside the thin shell and the single drawn web view. This slice follows that:

| Platform | Toolkit | Built here |
| --- | --- | --- |
| Linux | GTK 4 and libadwaita | Yes. `native/linux` |
| macOS | AppKit in Swift, floor macOS 12, Command Line Tools, no Xcode build | No. This runner is Linux. |
| Windows | WinUI 3 or WPF | No. This runner is Linux. |

A window that is a web page is not used as a stand-in.

## What the window does

It reads a local `memev` process (`status --json`, `doctor --json`, `card --json`) and, unless `MEMEV_HEALTH_URL=off`, `GET /healthz` on loopback only. Names other than `127.0.0.1`, `localhost` and `[::1]` are refused before a socket is opened. `localhost` is connected as `127.0.0.1` with no name lookup.

The window shows the health sentence under the wordmark, the seven menu entries from the rev2 decisions note, Help and support at the foot, and the overview sentence. Health lists each returned check as its title and status word. One button is filled: Refresh on the overview, Continue in the first launch. The minimum size is 1024 by 700. The sidebar is 240 wide. Colours and the amber section mark follow the visual system, with the later line and rule values from the 6 Nov decisions note. The theme follows the computer unless `MEMEV_THEME` is `dark` or `light`.

Before the overview, a first launch asks four things from the revised plan of 7 Nov 2026: this computer or another, where the notes live, which apps the core lists, and whether to record the person's own git remote. Continue on the last step saves one note with the core and reads it back. Nothing is connected, and no remote is recorded, unless that step says so and the person confirms. A recorded remote is pushed to by the core's own sync, at start, every 5 minutes by default and at stop, and the step says so before the tick. See `CLI-CONTRACT.md`. `MEMEV_GUIDE=off` skips it.

The other menu entries say they are not in this slice. They show no sample figures. Charts are not drawn. The window writes only the one note the first launch saves, through the core, and sends nothing on its own.

Fields that are not the version, the note count, the project name, the north star, or a check's id, title and status are not shown. A project name that contains a slash is not shown.

## Source trace

Design notes used as design, not as signed contracts: the information architecture, the visual system, the interaction model, the owner additions, and the 5 Nov decisions note (health sentence under the wordmark, menu of seven, Executive overview, one filled button). Signed amendment text for the page contracts was not in the handover, so none was written here and none is marked signed.

The unsigned research draft's first build step is the one this slice implements on Linux: a window whose sentence comes from live core answers. Criteria that need macOS 12.7.6, a screen-reader walk, or a signed bundle are not claimed.

## Out of this slice

macOS and Windows binaries, disk images, installers, release workflows, packaging, code signing, the rest of the screens' data, and any change to the core repository.
