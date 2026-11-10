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
| The Connect tab, the Background agents tab and setup | `onboarding --json`, `runner status --json`, `bus roster --json` | Read. `onboarding --json` is the one output of the trial core that names each app's id (its items' `app`); it also rewrites the checklist file and registers the seats of apps already connected, and starts nothing (REV2-SCREENS.md 5.1) |
| Setup's Connect step | `discover --json`, else `onboarding --json` | Read. Every app the core found, with its `automation`: `auto` apps are ticked, `one-step` apps show their step with a Copy button and no tick, `none` apps are shown and cannot be ticked. A core without `discover` answers with `onboarding --json` (Memev.Reader's GuideModel) |
| Setup's Connect step, after its sheet names the apps | `connect ID ID ... --json` | Only ticked `auto` ids the core printed. The guide moves to the import step only when it has ended. When an app did not connect, the guide reads `doctor --json` and shows each check that is not ok with its fix text |
| Fix the safe ones, in the result sheet | `doctor --fix --safe-only`, then `connect ID ... --json` once | Offered only when a check has a fix marked `destructive: false`. The connect names only the apps that did not connect |
| Setup's lead step | `status --json`, then `start` when Memev is not running, then `bus roster --json` | The setup's check step already said it starts Memev on this computer. With no seat, the step offers Retry, which reads the roster again |
| Connect, after its sheet names the apps | `connect ID ID ...` | Only ids the core printed in `onboarding --json`, only the rows the person ticked. Never `connect` with no app, never `connect all` |
| Disconnect, after its sheet | `disconnect ID` | An id from `onboarding --json` |
| Import history | `import scan --json` | Read. It imports nothing, needs no vault and does not stop Memev |
| Import, after its confirmation sheet names the sources | `import --source ID --source ID ... --yes` | Only ids `import scan --json` printed, only the ticked ones. It runs in the background; the core stops Memev while it imports |
| A chat export, and earlier imports | `import --export FILE --preview`, `import list` | The person pressed Choose or List. The core stops Memev for the moment of each and starts it again |
| An export, after its sheet shows the preview | `import --export FILE --yes` | The person pressed Import in the sheet that showed what is kept |
| Undo an import, after its sheet | `import undo ID` | An id `import list` printed |
| Background agents, after their sheet | `runner on APP --agents N --max-runs-per-day M`, `runner off SEAT` | N from 1 to 20; the daily limit is required, a whole number of 1 or more |
| Choose the lead, in setup, after its sheet | `bus operator SEAT` | A seat `bus roster --json` printed |
| The Chats screen | `chat status --json` | Read |
| Chats, after their sheets | `chat telegram setup --json`, `chat telegram pair --json`, `chat telegram disable --json`, `chat whatsapp enable --accept-risk --json`, `chat whatsapp link [--phone N] --json`, `chat whatsapp disable --json`, `chat discord setup --json`, `chat discord pair --guild G --channel C --zai Z [--person P] --json`, `chat discord disable --json`, `chat lark disable --json` | A token goes in `MEMEV_TELEGRAM_TOKEN` or `MEMEV_DISCORD_TOKEN` for that run, never as an argument. WhatsApp is turned on only after the person ticks the risk line. Discord ids are long numbers |

The window never runs `connect` with no app, never runs `connect` with a guessed id, never runs `push`, `github setup`, or `cloud` itself. On Windows the ids come from `onboarding --json` (above); the macOS window does not run `connect` yet. Pushing to a recorded remote is the core's own sync, described above.

`clients` on the core at `7544bb3e64e8fc166dc9fbc52c38f6caecd604e3` prints a marker, a name and a state. It does not print the id `connect` requires. SR-2.09.2 is not in that tree. Until the core prints an id, the window shows the live rows and does not connect anything. It does not keep a second copy of the app list.

A useful later line, for the core's owners, would be `clients --json` with `id`, `name`, `found`, `supported` and `connected`. This repository does not add that command.

## Proof

A passing check is a recall of the note just saved, then an overview sentence from `status --json` while that core is running. A stand-in command is not that proof.
