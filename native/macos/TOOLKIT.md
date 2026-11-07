# macOS toolkit

AppKit in Swift, floor macOS 12. The build uses the Command Line Tools (`swiftc`, `cc`, `xcrun`) and needs no Xcode project.

`make` builds `memev-desktop`; `make app` puts it in `Memev.app` with `Info.plist` (`LSMinimumSystemVersion` 12.0). `make test` runs the shared reader tests from `native/linux/test/test-read.c` on macOS, then `test/self-check.sh`.

The window reads the core through the same reader as the Linux window, `native/linux/src/read.c`, built unchanged with `-D_DARWIN_C_SOURCE`. A small C shim (`src/shim.c`) gives Swift plain accessors. The window is the same first slice as Linux: the health sentence under the wordmark, the seven menu entries, Help and support, the overview sentence, the checks, one filled Refresh button and Command-R. It writes nothing and sends nothing beyond loopback.

`MEMEV_SELF_CHECK=1` prints the window's values and quits, as on Linux. `MEMEV_SELF_CHECK_PNG=1` also prints a picture of the window as base64 on standard output; nothing is written to a file.

Built and checked on macOS 12.7.6 on Intel. CI runs `make test` on `macos-15-intel` and `macos-latest`. Signing, a disk image and an installer are later work.
