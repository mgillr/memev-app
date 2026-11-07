# Memev desktop app

Native programs for macOS, Windows and Linux. Each reads a Memev core on the same computer. The core stays in the memev repository.

The first slice is a Linux window in `native/linux`. It reads `memev status`, `memev doctor` and `memev card`, and loopback `/healthz`. On a first launch it asks which computer this is, where the notes go, which apps the core lists, and whether to record your own git remote, then checks a saved note with that core (`native/CLI-CONTRACT.md`). macOS and Windows are not built here. See `native/SLICE.md`.

Nothing is released from this repository.

This repository holds no licence, seat or paid logic. The app only reads the core's answers. Licensed under Apache-2.0.
