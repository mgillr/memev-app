# Later memev change: install pages

Not applied. The memev core repository's `main` stays at `7544bb3e64e8fc166dc9fbc52c38f6caecd604e3`. Do not open this as a commit on that `main`. Publish it only after the owner accepts a real release artifact. No tag, no GitHub Release, and no npm publish is part of this note.

The command-line archive built in this repository is the file those pages should name, once it is actually attached to a release:

`https://github.com/mgillr/memev/releases/download/v0.1.0/memev-cli-0.1.0-linux-x64.tar.gz`

That URL does not work today. The pages must not pretend it does. Until the file is on the release, the pages should keep saying that no release has been published.

## README.md

Replace the "clone, then npm install" block as the first path for a person, and keep the clone path as the path for someone working on the source. Suggested wording, still marked as not live until the release exists:

```
Linux (64-bit Intel or AMD, glibc 2.38 or newer), after the release file is published:

    curl -fsSL -o memev-cli-0.1.0-linux-x64.tar.gz https://github.com/mgillr/memev/releases/download/v0.1.0/memev-cli-0.1.0-linux-x64.tar.gz
    curl -fsSL -o memev-cli-0.1.0-linux-x64.tar.gz.sha256 https://github.com/mgillr/memev/releases/download/v0.1.0/memev-cli-0.1.0-linux-x64.tar.gz.sha256
    sha256sum -c memev-cli-0.1.0-linux-x64.tar.gz.sha256
    tar -xzf memev-cli-0.1.0-linux-x64.tar.gz
    sh memev-cli-0.1.0-linux-x64/install

This install includes Node.js and Git. You do not install them yourself.
macOS and Windows packages are not published yet.
To remove the program and keep your notes: memev-cli uninstall
```

Keep the existing sentence that no GitHub Release has been published, until one has.

## docs/QUICKSTART.md

Point the Linux install at the same archive and checksum. State that macOS and Windows are not in this release. Do not show `npx memev init` as a working path. That package is not published, and this work does not publish it.

## docs/INSTALL-FOR-AI.md

Tell an agent to run the `sh .../install` command above, not `npm install` and not `npx`. The install must not connect AI apps by itself. The person connects one app at a time afterwards with `memev connect <app>`.

## scripts/install.sh and scripts/install.ps1

Leave them on `main` until a separate unlanded branch is allowed. The behaviour to propose there, without editing `main` now:

- A release file with no extra argument, for the archive above, still checking the SHA-256 and installing nothing when it does not match.
- POSIX `sh`, including `dash`, for the Linux script.
- Use the Node and Git shipped in the archive, including when another Node or Git is already on `PATH`.
- A second run updates the program and does not delete notes.
- Do not add a licence-server address, a price, or a payment change.

The Windows script still has to be run on Windows. Parsing it on Linux is not a Windows result.

## Guided setup

The pinned commit has `memev init`. It does not have a separate guided setup command. This installer calls `memev init --local-only --no-connect --no-service`. A guided flow that connects apps, turns on remote access, or sets up GitHub is a later change in memev, not this installer.

## Disk image and Windows executable

After the Linux command-line archive is accepted, build an unsigned `.dmg` on macOS and an unsigned `.exe` on Windows. Do not sign them. Do not claim those builds from a Linux run.
