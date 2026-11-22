# Linux installer

`install.sh` installs Memev for the current user. Nothing is signed and nothing asks for a password.

```sh
installers/linux/install.sh
```

That builds the window, fetches the core commit in `pins.json`, checks the Node.js 22 download against its SHA-256, and puts all three under `~/.local`:

- `~/.local/bin/memev` and `~/.local/bin/memev-desktop`
- `~/.local/lib/memev/` the window, the core and Node.js
- `~/.local/share/applications/memev.desktop`

The first run creates a personal vault at `~/memev` with `--local-only --no-connect --no-service`. A vault that is already there is left as it is. Running the script again replaces the window and the core and does not create a second vault.

Open the window from the application menu, or run `memev-desktop`. The first time it opens on Operations, so a lead can be chosen and told what to do. Later starts open the executive overview.

```sh
install.sh --prefix DIR --notes DIR --core CHECKOUT --system-node --no-init --launch
```

`--core` uses a checkout you already have. `--system-node` uses `node` on PATH when it is version 22 or newer, instead of the pinned download. Building needs gcc, pkg-config, GTK 4, libadwaita, make, git and curl. Running the installed copy needs none of the build tools.

`test/install-check.sh` installs into a temporary home, starts the vault, and checks that the window opens on Operations. It is what CI runs.
