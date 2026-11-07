# What the desktop window asks of the core

This is the contract the window uses. It is not a signed amendment. The window does not install the core and does not own the list of apps.

The command is `MEMEV_BIN`, or `memev` on `PATH`. The window passes arguments in a list. It does not use a shell.

## Commands

| When | Command | Only if |
| --- | --- | --- |
| This computer, notes folder confirmed | `init --root DIR --local-only --no-connect --no-service --port N` | The folder is a full path and not the disk root |
| Another computer, code confirmed | `join CODE` | The person pressed Join. Nothing is sent before that |
| Remote ticked and the address is accepted | `backup set URL` | The address is `file://`, `https://`, `ssh://`, `git://` or `git@host:path`. This records the remote as git `origin`. From then on the core's daemon commits and pushes the notes to it: at start, every `sync.interval_min` minutes (5 by default), and at stop. The window says so before the tick |
| The check step | `remember PHRASE --force` then `recall PHRASE --json` | This computer only. The phrase has to come back |
| After the check, while the window is open | `status --json`, `doctor --json`, `card --json` | Read only |
| The app list | `clients` | Read only. Names and states are taken from that text |
| Start Memev, after its sheet | `start` | The person pressed Start Memev in the sheet that says it starts Memev's background service on this Mac (the owner's ruling D-1: the window runs a command itself only after a confirmation that names what it changes). `start` opens the daemon for the notes and returns when it answers. It needs no terminal and registers no login service; that is `service install`, which the window does not run |

The window never runs `connect` with no app, never runs `connect` with a guessed id, never runs `push`, `github setup`, or `cloud` itself. Pushing to a recorded remote is the core's own sync, described above.

`clients` on the core at `7544bb3e64e8fc166dc9fbc52c38f6caecd604e3` prints a marker, a name and a state. It does not print the id `connect` requires. SR-2.09.2 is not in that tree. Until the core prints an id, the window shows the live rows and does not connect anything. It does not keep a second copy of the app list.

A useful later line, for the core's owners, would be `clients --json` with `id`, `name`, `found`, `supported` and `connected`. This repository does not add that command.

## Proof

A passing check is a recall of the note just saved, then an overview sentence from `status --json` while that core is running. A stand-in command is not that proof.
