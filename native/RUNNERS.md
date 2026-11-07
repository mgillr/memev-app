# Runners, checked 7 Nov 2026

Public GitHub-hosted labels, from the hosted-runner reference:

- Linux: `ubuntu-latest`, `ubuntu-24.04`, `ubuntu-22.04`, `ubuntu-26.04`, and arm variants. The window's job uses `ubuntu-latest`.
- Windows: `windows-latest`, `windows-2025`, `windows-2022`, and arm variants. No Windows shell is built, so no Windows job is added.
- macOS: `macos-latest`, `macos-15`, `macos-26`, `macos-15-intel`, `macos-26-intel`. There is no `macos-12` label. `macos-13` was removed after the December 2025 retirement and is not used.

The owner's floor is macOS 12. Node 22 still publishes a darwin-x64 build, and the core pins one, but that does not provide a macOS 12 machine in GitHub Actions. A Mac shell is not claimed until a runner that can build it exists. This job does not upload a package.
