# Memev desktop app

Native programs for macOS, Windows and Linux. Each reads a Memev core on the same computer. The core stays in the memev repository.

The first slice is a Linux window in `native/linux`. It reads `memev status`, `memev doctor` and `memev card`, and loopback `/healthz`. macOS and Windows are not built here. See `native/SLICE.md`.

The unsigned Linux command-line installer for the free core is built from [installers/cli/README.md](installers/cli/README.md). Packages are tested in CI and are not uploaded as Actions artifacts. Nothing is published yet.

Nothing is released from this repository.

This repository holds no licence, seat or paid logic. The app only reads the core's answers. Licensed under Apache-2.0.
