# Memev desktop app

Native programs for macOS, Windows and Linux. Each reads a Memev core on the same computer. The core stays in the memev repository.

Linux, Windows and macOS each ship a window. On a first launch with no vault yet, it asks which computer this is, where the notes go, which apps the core lists, and whether to record your own git remote, then checks a saved note (`native/CLI-CONTRACT.md`). When that finishes, or when a vault is already on the computer, the window opens on Operations so the lead can be told what to do. Later starts open the executive overview. The other pages read the live core: projects, bots and agents, memory, checked work and settings (`native/PAGES.md`).

`installers/linux/install.sh` installs the personal desktop for the current user. Windows and macOS installers are `installers/windows` and `installers/macos`.

Nothing is released from this repository.

This repository holds no licence, seat or paid logic. The app only reads the core's answers. Licensed under Apache-2.0.
