# Windows toolkit

The Windows window is WPF on .NET 9. It builds with `dotnet build` on a stock Windows runner, with no Windows App SDK and no packaging step, which is why it was chosen over WinUI 3 for the first slice.

- `Memev.Reader`: the reader, plain .NET with no window code. It does what `native/linux/src/read.c` does: it runs `memev status --json`, `memev doctor --json` and `memev card --json`, reads loopback `/healthz`, keeps only the version, the note count, the project, the north star and each check's id, title and status, and builds the same health and overview sentences. `read.c` is not used here because it is POSIX only. On Windows it finds `memev` through `PATHEXT`, so the `memev.cmd` that npm installs is found, and runs a `.cmd` through `cmd.exe`.
- `Memev.Reader.Check`: the reader's tests, the cases of `native/linux/test/test-read.c`. They run on any computer with .NET 9: `dotnet run --project native/windows/Memev.Reader.Check`.
- `Memev.Desktop`: the window, `memev-desktop.exe`. It runs only on Windows. It compiles elsewhere with `EnableWindowsTargeting`.
- `test/self-check.ps1`: opens the window against `test/fixtures/memev.cmd`, which prints the Linux fixture's answers, and checks the same values as the Linux and macOS self-checks in both themes. It also checks that the binaries name no engine token, and that a running window holds no connection other than loopback.

The first-launch questions and an installer are not in this slice. Nothing is packaged or uploaded.
