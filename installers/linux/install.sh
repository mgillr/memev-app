#!/bin/sh
# SPDX-License-Identifier: Apache-2.0
# Installs the Memev personal desktop for the current user: the window, a core at the pinned commit,
# and a Node.js runtime. The notes stay on this computer. Nothing is connected.
set -eu

prefix=${HOME}/.local
notes=${HOME}/memev
core=
system_node=0
do_init=1
launch=0

usage() {
  cat <<'EOF'
Usage: install.sh [--prefix DIR] [--core DIR] [--notes DIR] [--system-node] [--no-init] [--launch]

Installs the window, a Memev core and Node.js under PREFIX (default: $HOME/.local).
The first run creates a personal vault at NOTES (default: $HOME/memev) when that folder is not already a vault.
--core uses a checkout you already have, instead of the pinned commit.
--system-node uses node on PATH when it is version 22 or newer.
--no-init leaves the vault for the window's first launch.
--launch opens the window when the install finishes.
EOF
}

while [ $# -gt 0 ]; do
  case "$1" in
    --prefix) prefix=$2; shift 2 ;;
    --core) core=$2; shift 2 ;;
    --notes) notes=$2; shift 2 ;;
    --system-node) system_node=1; shift ;;
    --no-init) do_init=0; shift ;;
    --launch) launch=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "unknown argument: $1" >&2; usage >&2; exit 2 ;;
  esac
done

here=$(CDPATH= cd -- "$(dirname "$0")" && pwd)
root=$(CDPATH= cd -- "$here/../.." && pwd)
pin=$here/pins.json

need() {
  if ! command -v "$1" >/dev/null 2>&1; then
    echo "memev install needs $1" >&2
    exit 1
  fi
}

json_get() {
  key=$1
  file=$2
  sed -n "s/.*\"$key\"[[:space:]]*:[[:space:]]*\"\\([^\"]*\\)\".*/\\1/p" "$file" | head -n 1
}

node_major() {
  "$1" -p 'process.versions.node.split(".")[0]' 2>/dev/null || echo 0
}

need git
need make
need tar
need sha256sum

memev_commit=$(json_get memev "$pin")
node_ver=$(json_get node "$pin")
node_file=$(json_get node_file "$pin")
node_sha=$(json_get node_sha256 "$pin")
if [ -z "$memev_commit" ] || [ -z "$node_ver" ] || [ -z "$node_file" ] || [ -z "$node_sha" ]; then
  echo "pins.json is missing a value" >&2
  exit 1
fi

echo "Building the window"
make -C "$root/native/linux"

lib=$prefix/lib/memev
mkdir -p "$lib" "$prefix/bin" "$prefix/share/applications"
install -m 0755 "$root/native/linux/memev-desktop" "$lib/memev-desktop"

node_bin=
if [ "$system_node" -eq 1 ] && command -v node >/dev/null 2>&1; then
  major=$(node_major node)
  if [ "$major" -ge 22 ]; then
    node_bin=$(command -v node)
  fi
fi
if [ -z "$node_bin" ]; then
  need curl
  cache=${XDG_CACHE_HOME:-$HOME/.cache}/memev-install
  mkdir -p "$cache"
  tarball=$cache/$node_file
  if [ ! -f "$tarball" ] || ! printf '%s  %s\n' "$node_sha" "$tarball" | sha256sum -c >/dev/null 2>&1; then
    echo "Downloading Node.js $node_ver"
    rm -f "$tarball"
    curl -fsSL "https://nodejs.org/dist/v$node_ver/$node_file" -o "$tarball"
    if ! printf '%s  %s\n' "$node_sha" "$tarball" | sha256sum -c >/dev/null; then
      rm -f "$tarball"
      echo "Node.js download did not match its checksum" >&2
      exit 1
    fi
  fi
  rm -rf "$lib/node"
  mkdir -p "$lib/node"
  tar -xJf "$tarball" -C "$lib/node" --strip-components=1
  node_bin=$lib/node/bin/node
fi

if [ -n "$core" ]; then
  if [ ! -f "$core/bin/memev.mjs" ]; then
    echo "no core at $core" >&2
    exit 1
  fi
  src=$core
else
  echo "Fetching the core"
  src=$(mktemp -d)
  git init -q "$src"
  git -C "$src" fetch --depth 1 "https://github.com/mgillr/memev.git" "$memev_commit"
  git -C "$src" checkout -q FETCH_HEAD
fi
rm -rf "$lib/core"
mkdir -p "$lib/core"
tar -C "$src" --exclude .git -cf - . | tar -C "$lib/core" -xf -
if [ -z "$core" ]; then
  rm -rf "$src"
fi
chmod +x "$lib/core/bin/memev.mjs"

cat > "$prefix/bin/memev" <<EOF
#!/bin/sh
exec "$node_bin" "$lib/core/bin/memev.mjs" "\$@"
EOF
cat > "$prefix/bin/memev-desktop" <<EOF
#!/bin/sh
export MEMEV_BIN="$prefix/bin/memev"
exec "$lib/memev-desktop" "\$@"
EOF
chmod 0755 "$prefix/bin/memev" "$prefix/bin/memev-desktop"

cat > "$prefix/share/applications/memev.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Memev
Comment=Notes and the lead agent on this computer
Exec=$prefix/bin/memev-desktop
Terminal=false
Categories=Utility;
EOF
data_home=${XDG_DATA_HOME:-$HOME/.local/share}
if [ "$prefix/share/applications/memev.desktop" != "$data_home/applications/memev.desktop" ]; then
  mkdir -p "$data_home/applications"
  cp "$prefix/share/applications/memev.desktop" "$data_home/applications/memev.desktop"
fi

if [ "$do_init" -eq 1 ] && [ ! -d "$notes/.memev" ]; then
  echo "Creating the personal vault"
  MEMEV_NO_SERVICE=1 MEMEV_NO_TUNNEL=1 MEMEV_NO_BACKGROUND=1 MEMEV_NO_DOWNLOAD=1 \
    "$prefix/bin/memev" init --root "$notes" --local-only --no-connect --no-service </dev/null
fi

echo "Installed. The window is $prefix/bin/memev-desktop"
case ":$PATH:" in
  *":$prefix/bin:"*) ;;
  *) echo "Add $prefix/bin to PATH to run memev from a terminal." ;;
esac

if [ "$launch" -eq 1 ]; then
  exec "$prefix/bin/memev-desktop"
fi
