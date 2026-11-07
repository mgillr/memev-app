# Windows installer (unsigned)

`build.ps1` makes `dist\Memev-<version>-windows-x64-setup.exe`. It installs Memev for the current user only, into `%LOCALAPPDATA%\Programs\Memev`, with no administrator rights, a Start menu entry and an uninstaller. Inside:

- the window from `native/windows`, published self-contained, so .NET is not needed on the PC
- the Memev core at the commit in `pins.json`, packed as `npm pack` packs it
- the pinned Node.js for 64-bit Windows, checked against its SHA-256 from nodejs.org
- `resources\bin\memev.cmd`, which the window uses when `MEMEV_BIN` is not set

Nothing else needs to be installed first; the core still uses the PC's own `git`. Nothing is signed or published, so Windows SmartScreen warns on first run: choose More info, then Run anyway. Uninstall stops the core it started and keeps the notes folder.

`test\install-check.ps1 <setup.exe>` installs silently into a temporary folder and home, with failing stand-ins for `node`, `npm`, `npx` and `memev` first on PATH. The bundled core inits a notes folder and starts, status and doctor answer, the window reads that live core, the first launch saves and reads back a note, the core stops, and uninstall leaves no process from the install and keeps the notes. It opens a real window for about a second, so it runs in CI.

Building and checking need PowerShell 7 (`pwsh`), .NET 9, Node 22 and Inno Setup 6. Installing needs none of them.

Not yet: a login item, code signing.
