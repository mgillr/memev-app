# Memev command-line installer (unsigned, Linux x64)

This folder builds a command-line installer for the free core. It lives in this repository (memev-app). It does not change the memev core repository's `main` branch.

The installer is a single archive a person can unpack and run. It contains:

- Memev `0.1.0` from commit `7544bb3e64e8fc166dc9fbc52c38f6caecd604e3`
- the pinned Node.js `22.23.4` program from `scripts/node-runtime.json` in that commit (the `node` program only, not npm)
- a pinned Git `2.43.0` so the notes folder can be created when the computer has no Git

No administrator rights. No download during install. The notes folder is not removed by uninstall.

Nothing here is signed, tagged, or published. The archive is not on GitHub Releases yet. npm is not used as a delivery path.

## Build

From this directory, with Node available on the build machine:

```sh
node build.mjs
```

The recipe checks out that memev commit, checks `scripts/node-runtime.json` against `pins.json`, downloads the pinned Node archive and the pinned Git packages over HTTPS, and checks every SHA-256 before use. It writes:

- `dist/memev-cli-0.1.0-linux-x64.tar.gz`
- `dist/memev-cli-0.1.0-linux-x64.tar.gz.sha256`

Run it twice. The two archives have the same SHA-256. `dist/` is not committed.

The file list inside the Memev package is the list `npm pack --dry-run` produces at that commit (the same set `scripts/release-tarball.mjs` packs). The archive itself is built with a fixed timestamp so the checksum does not change between runs. The external merge engine is not part of the package. The build stops if that engine's source, a package import, or a Business Source License header shows up in `bin/`, `src/`, `vendor/`, `templates/`, or `package.json`.

## Install on Linux x64

The computer needs glibc 2.38 or newer (Ubuntu 24.04 meets this). It does not need Node, npm, or Git.

```sh
tar -xzf memev-cli-0.1.0-linux-x64.tar.gz
dash memev-cli-0.1.0-linux-x64/install
```

`sh` is enough. The script does not use bash-only syntax. A checksum mismatch prints one sentence and installs nothing.

After install:

- `memev status` and `memev-cli status` report whether it is running, including the local health check
- `memev-cli upgrade <archive>` checks the `.sha256` file beside the archive, then updates. Notes stay.
- running the same `install` again updates in place and keeps notes
- `memev-cli uninstall` removes the program, the bundled Node, and the bundled Git, and leaves the notes folder

The installer adds one line to the shell startup file so a new terminal can run `memev`. It says so before it edits the file. `memev path remove` takes that line out, and uninstall does that too.

This install does not connect AI apps, does not turn on remote access, and does not register a login service. After a restart of the computer, run `memev start`. A login service is not included yet because the current service file would not see the bundled Git.

## What was verified

`node --test test/isolated-linux.mjs` builds the archive twice, then on this Linux machine:

- refuses a damaged package
- installs with failing stand-ins for `node`, `npm`, `npx`, `git`, `curl`, `wget`, and `sudo` first on `PATH`
- checks the running program is the bundled Node
- checks health, saves a note, updates, reads the note back, uninstalls, and checks the note is still there

The measured checksum and the host are in `PROOF.md` after that test. That file records this machine only.

## Not built

These pins exist in memev `scripts/node-runtime.json` and are listed in the manifest as not built:

- `linux-arm64`
- `darwin-arm64`
- `darwin-x64`
- `win-x64`

`win-arm64` is not in that pin file. No macOS or Windows run was attempted. A pass on those systems is not claimed.

## Later: macOS disk image and Windows executable

Do these only on a real machine of that system. Do not treat a Linux run as a result for them.

1. macOS, on macOS 12 or newer, for both `darwin-arm64` and `darwin-x64`: build an unsigned `.dmg` that contains the same kind of command-line payload, using the Node archive and checksum already in `scripts/node-runtime.json`. No Apple signing and no notarisation.
2. Windows, on Windows, for `win-x64`: build an unsigned `.exe` (the current Windows script shape checks the hash with `Get-FileHash`). Bundle the pinned Node archive. Bundle a pinned Git as well, checked on that machine. `win-arm64` waits until a pin exists.
3. Signing stays off until the owner decides otherwise.

The old double-click `.command` and `.cmd` wrappers are not this step. The owner asked for `.dmg` and `.exe`, unsigned, after the command-line installer.

## Later change in the memev repository

The wording for the memev README, quick start, and AI install page is in `follow-up/memev-core-pr.md`. It is not applied. `main` stays untouched. Do not publish a release, a tag, or an npm package from this work.

## What this does not do

- It does not mark any contract done, and it does not reuse draft numbers that already belong to other signed work.
- It does not run the guided setup flow. That flow is not in the pinned memev commit. This installer runs the existing first-run command with local-only, no app connections, and no login service.
- It does not bake in a licence server, a price, or a payment change.
- It does not publish a container image.
