# macOS disk image (unsigned)

`build.sh` makes `dist/Memev-<version>-macos-<arch>.dmg` on the Mac it runs on (`x64` on Intel, `arm64` on Apple silicon). The image holds `Memev.app` and a link to Applications. Inside the app:

- the window from `native/macos`
- the Memev core at the commit in `pins.json`, packed as `npm pack` packs it
- the pinned Node.js program from the core's `scripts/node-runtime.json`, checked against its SHA-256
- `Contents/Resources/bin/memev`, which the window uses when `MEMEV_BIN` is not set

Nothing else needs to be installed first; the core still uses the Mac's own `git`. Nothing is signed, notarised or published. On first open, macOS asks because the app is unsigned: Control-click it and choose Open.

`test/install-check.sh <dmg>` installs into a temporary Applications folder and home with failing stand-ins for `node`, `npm` and `memev` first on PATH, then: the bundled core inits a notes folder and starts, status and doctor answer, the window reads that live core, the core stops, and uninstall leaves no process and keeps the notes. It opens a window for about a second; `MEMEV_SKIP_WINDOW=1` leaves that out.

Not yet: the guided first launch on macOS, a login item, signing and notarisation.
