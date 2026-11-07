# Linux command-line installer proof

Date: 2026-11-07
Host: Linux 6.12.113+ x64
System library: glibc 2.39
Memev commit: 7544bb3e64e8fc166dc9fbc52c38f6caecd604e3
Version: 0.1.0
Platform built and tested: linux-x64
Artifact: memev-cli-0.1.0-linux-x64.tar.gz
SHA-256: 6c193fbde166bc29127a3fa60f25c84b0712e8096d5fc55b213ca3a42feda8c7
Two builds produced the same SHA-256.
Unsigned. Not published to GitHub Releases. Not published to npm.

Checked on this machine, with node, npm, npx, git, curl, wget, and sudo replaced by programs that fail if called:

- A damaged package was refused and created no notes folder.
- Install started Memev. The running program was the bundled Node binary.
- Health answered on 127.0.0.1:44773.
- A note was saved, an update ran, and the same note was still there.
- Uninstall removed the program and left the note.
- No stand-in for node, npm, or git was called.

Not built and not tested here:

- linux-arm64
- darwin-arm64
- darwin-x64
- win-x64
- win-arm64
- macos-dmg
- windows-exe

macOS and Windows were not run, including no simulated pass.
