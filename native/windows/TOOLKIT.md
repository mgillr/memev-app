# Windows toolkit

The Windows window is WPF on .NET 9. It builds with `dotnet build` on a stock Windows runner, with no Windows App SDK and no packaging step, which is why it was chosen over WinUI 3 for the first slice.

- `Memev.Reader`: the reader, plain .NET with no window code. It does what `native/linux/src/read.c` does: it runs `memev status --json`, `memev doctor --json` and `memev card --json`, reads loopback `/healthz`, keeps only the version, the note count, the project, the north star and each check's id, title and status, and builds the same health and overview sentences. `read.c` is not used here because it is POSIX only. On Windows it finds `memev` through `PATHEXT`, so the `memev.cmd` that npm installs is found, and runs a `.cmd` through `cmd.exe`.
- `Memev.Reader.Check`: the reader's tests, the cases of `native/linux/test/test-read.c`. They run on any computer with .NET 9: `dotnet run --project native/windows/Memev.Reader.Check`.
- `Memev.Desktop`: the window, `memev-desktop.exe`. It runs only on Windows. It compiles elsewhere with `EnableWindowsTargeting`.
- `test/self-check.ps1`: opens the window against `test/fixtures/memev.cmd`, which prints the Linux fixture's answers, and checks the same values as the Linux and macOS self-checks in both themes. It also checks that the binaries name no engine token, and that a running window holds no connection other than loopback.

- First launch: `Memev.Reader/Guide.cs` is `native/linux/src/guide.c` in C#: this computer or another, where the notes live (a full path, not the root of a disk or share), which apps the core lists (nothing is connected), and whether to record your own git remote, which the core then syncs. Only the commands in `native/CLI-CONTRACT.md` are built. Its commands run off the window's thread. A `.cmd` core gets every argument quoted, and one holding `"`, `%` or `!` is refused, so typed text cannot become a command line. `MEMEV_GUIDE=off` skips it; it shows until it is finished once (`desktop-first-launch-done` under `MEMEV_CONFIG_DIR` or `%APPDATA%\memev`).
- `test/real-core.ps1`: the first launch against a real core checkout through node, as `native/linux/test/real-core.sh`.

An installer is not in this slice. Nothing is packaged or uploaded.
