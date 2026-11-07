#!/bin/sh
# Install Memev from this folder. No administrator rights. No download.
# The program files include Node and Git. Your notes are a separate folder and uninstall keeps them.
set -eu

say() { printf '%s\n' "$1"; }

die() { say "$1"; exit 1; }

need_user() {
  if [ "$(id -u)" -eq 0 ]; then
    die "Run this as your own user. It does not need administrator rights."
  fi
}

need_platform() {
  os=$(uname -s)
  arch=$(uname -m)
  if [ "$os" != "Linux" ] || [ "$arch" != "x86_64" ]; then
    die "This installer is for Linux on 64-bit Intel and AMD computers only. This computer is ${os} ${arch}. macOS and Windows installers are not built yet."
  fi
  libc=$(getconf GNU_LIBC_VERSION 2>/dev/null || true)
  major=$(printf '%s\n' "$libc" | sed -n 's/.* \([0-9][0-9]*\)\.\([0-9][0-9]*\).*/\1/p')
  minor=$(printf '%s\n' "$libc" | sed -n 's/.* \([0-9][0-9]*\)\.\([0-9][0-9]*\).*/\2/p')
  if [ -z "$major" ] || [ -z "$minor" ]; then
    die "This installer could not read the system library version, so nothing was installed."
  fi
  if [ "$major" -lt 2 ] || { [ "$major" -eq 2 ] && [ "$minor" -lt 38 ]; }; then
    die "This installer needs system library glibc 2.38 or newer (this computer has ${libc}). Nothing was installed."
  fi
}

resolve_config() {
  if [ -n "${config_dir:-}" ]; then
    cfg=$config_dir
  elif [ -n "${MEMEV_CONFIG_DIR:-}" ]; then
    cfg=$MEMEV_CONFIG_DIR
  elif [ -n "${XDG_CONFIG_HOME:-}" ]; then
    cfg=$XDG_CONFIG_HOME/memev
  else
    cfg=${HOME:?}/.config/memev
  fi
  case $cfg in
    ""|/) die "Refusing to use that settings folder." ;;
  esac
}

here=$(CDPATH= cd -- "$(dirname "$0")" && pwd)
base_path=${PATH:-/usr/bin:/bin}
action=install
root=
port=
config_dir=
upgrade_file=

while [ $# -gt 0 ]; do
  case $1 in
    install|status|uninstall)
      action=$1
      shift
      ;;
    upgrade)
      action=upgrade
      upgrade_file=${2:-}
      if [ -z "$upgrade_file" ]; then
        die "To update, run: memev-cli upgrade <the installer file>"
      fi
      shift 2
      ;;
    --root)
      root=${2:-}
      [ -n "$root" ] || die "Missing the folder after --root."
      shift 2
      ;;
    --port)
      port=${2:-}
      printf '%s\n' "$port" | grep -Eq '^[0-9]+$' || die "The port must be a number."
      shift 2
      ;;
    --config-dir)
      config_dir=${2:-}
      [ -n "$config_dir" ] || die "Missing the folder after --config-dir."
      shift 2
      ;;
    *)
      die "This installer does not take that option ($1)."
      ;;
  esac
done

need_user
need_platform
resolve_config

checksum_fail() {
  die "This copy does not match its checksum, so nothing was installed. Download the installer again."
}

check_payload() {
  payload=$1
  if [ ! -f "$payload/checksums.txt" ]; then
    checksum_fail
  fi
  if ! (cd "$payload" && sha256sum -c checksums.txt >/dev/null 2>&1); then
    checksum_fail
  fi
}

apply_payload() {
  src=$1
  check_payload "$src/payload"
  work=$(mktemp -d)
  cp -a "$src/payload" "$work/payload"
  cp "$src/install" "$work/install"
  cp "$src/MANIFEST.json" "$work/MANIFEST.json"
  cp "$src/THIRD-PARTY-NOTICES.txt" "$work/THIRD-PARTY-NOTICES.txt"
  if ! (cd "$work/payload" && sha256sum -c checksums.txt >/dev/null 2>&1); then
    rm -rf "$work"
    checksum_fail
  fi
  mkdir -p "$cfg/cli"
  rm -rf "$cfg/cli/payload"
  mv "$work/payload" "$cfg/cli/payload"
  mv "$work/install" "$cfg/cli/install"
  mv "$work/MANIFEST.json" "$cfg/cli/MANIFEST.json"
  mv "$work/THIRD-PARTY-NOTICES.txt" "$cfg/cli/THIRD-PARTY-NOTICES.txt"
  chmod 755 "$cfg/cli/install" "$cfg/cli/payload/node/bin/node" "$cfg/cli/payload/git/bin/git"
  rmdir "$work" 2>/dev/null || rm -rf "$work"
}

prepare_env() {
  PATH="$cfg/cli/payload/node/bin:$cfg/cli/payload/git/bin:$base_path"
  export PATH
  export GIT_EXEC_PATH="$cfg/cli/payload/git/libexec/git-core"
  export LD_LIBRARY_PATH="$cfg/cli/payload/git/lib"
  export MEMEV_CONFIG_DIR=$cfg
  export MEMEV_NO_SERVICE=1
  export MEMEV_NO_TUNNEL=1
  export MEMEV_NO_DOWNLOAD=1
  export MEMEV_NO_BACKGROUND=1
  export MEMEV_NO_PATH_PROMPT=1
  unset GH_TOKEN GITHUB_TOKEN GH_ENTERPRISE_TOKEN GIT_ASKPASS || true
}

node_bin() { printf '%s\n' "$cfg/cli/payload/node/bin/node"; }

pkg_version() {
  sed -n 's/.*"version"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' "$cfg/cli/payload/package/package.json" | head -n 1
}

sq() {
  printf "'%s'" "$(printf '%s' "$1" | sed "s/'/'\\\\''/g")"
}

write_wrapper() {
  ver=$(pkg_version)
  js=$cfg/runtime/$ver/bin/memev.mjs
  [ -f "$js" ] || die "The installed program is incomplete, so Memev was not left half-configured. Run the installer again."
  mkdir -p "$cfg/bin"
  rec=$(sq "$(node_bin)")
  jsq=$(sq "$js")
  pathq=$(sq "$cfg/cli/payload/node/bin:$cfg/cli/payload/git/bin")
  gitq=$(sq "$cfg/cli/payload/git/libexec/git-core")
  libq=$(sq "$cfg/cli/payload/git/lib")
  {
    printf '%s\n' '#!/bin/sh'
    printf 'export PATH=%s:"$PATH"\n' "$pathq"
    printf 'export GIT_EXEC_PATH=%s\n' "$gitq"
    printf 'export LD_LIBRARY_PATH=%s\n' "$libq"
    printf 'REC=%s\n' "$rec"
    printf 'JS=%s\n' "$jsq"
    cat <<'EOF'
N=$(command -v node 2>/dev/null)
if [ -n "$N" ] && [ "$N" != "$REC" ]; then
  V=$("$N" --version 2>/dev/null); V=${V#v}; V=${V%%.*}
  case "$V" in ''|*[!0-9]*) ;; *) [ "$V" -ge 22 ] && exec "$N" "$JS" "$@" ;; esac
fi
[ -x "$REC" ] && exec "$REC" "$JS" "$@"
echo "Memev could not start. The included Node program is missing." >&2
exit 127
EOF
  } > "$cfg/bin/memev"
  chmod 755 "$cfg/bin/memev"
  {
    printf '%s\n' '#!/bin/sh'
    printf 'root=$(CDPATH= cd -- "$(dirname "$0")/../cli" && pwd)\n'
    printf 'if [ $# -eq 0 ]; then exec "$root/install" status; fi\n'
    printf 'exec "$root/install" "$@"\n'
  } > "$cfg/bin/memev-cli"
  chmod 755 "$cfg/bin/memev-cli"
}

startup_file() {
  shell_name=$(basename "${SHELL:-sh}")
  case $shell_name in
    zsh) printf '%s\n' "$HOME/.zshrc" ;;
    bash) printf '%s\n' "$HOME/.bashrc" ;;
    *) printf '%s\n' "$HOME/.profile" ;;
  esac
}

add_path_line() {
  rc=$(startup_file)
  say "This adds one line to ${rc} so a new terminal can run memev. To take that line out, run: memev path remove"
  prepare_env
  "$(node_bin)" "$cfg/runtime/$(pkg_version)/bin/memev.mjs" path add
}

vault_file() {
  if [ -f "$cfg/default" ]; then
    awk 'NR==1 { print; exit }' "$cfg/default"
  fi
}

run_init() {
  prepare_env
  set -- init --local-only --no-connect --no-service
  if [ -n "$root" ]; then
    set -- "$@" --root "$root"
  fi
  if [ -n "$port" ]; then
    set -- "$@" --port "$port"
  fi
  "$(node_bin)" "$cfg/cli/payload/package/bin/memev.mjs" "$@"
}

run_update() {
  prepare_env
  ver=$(pkg_version)
  js=$cfg/runtime/$ver/bin/memev.mjs
  if [ ! -f "$js" ]; then
    js=$cfg/cli/payload/package/bin/memev.mjs
  fi
  "$(node_bin)" "$js" update "$cfg/cli/payload/memev.tgz"
}

finish_program() {
  write_wrapper
  prepare_env
  "$cfg/bin/memev" restart
  ver=$(pkg_version)
  printf '%s\n' "$ver" > "$cfg/cli/INSTALLED"
  notes=$(vault_file)
  say "Memev is installed and running."
  say "Version ${ver}."
  if [ -n "$notes" ]; then
    say "Your notes are in ${notes}."
  fi
  say "This install does not connect AI apps and does not turn on remote access."
  say "Memev does not start by itself when you log in. After a restart, run: memev start"
  add_path_line
  say "To check it, run: $cfg/bin/memev-cli status"
  say "To update it, run: $cfg/bin/memev-cli upgrade <the installer file>"
  say "To remove the program and keep your notes, run: $cfg/bin/memev-cli uninstall"
}

do_install() {
  if [ -e "$cfg/runtime" ] || [ -e "$cfg/bin/memev" ]; then
    if [ ! -f "$cfg/cli/INSTALLED" ]; then
      die "This computer already has Memev in ${cfg}, from a different installer. Nothing was changed."
    fi
  fi
  check_payload "$here/payload"
  updating=0
  if [ -f "$cfg/cli/INSTALLED" ]; then
    updating=1
    say "Updating Memev. Your notes stay where they are."
  else
    say "Installing Memev."
  fi
  apply_payload "$here"
  if [ "$updating" -eq 1 ]; then
    run_update
  else
    run_init
  fi
  finish_program
}

do_status() {
  [ -x "$cfg/bin/memev" ] || die "Memev is not installed."
  prepare_env
  "$cfg/bin/memev" status --json || die "Memev is not running. Start it with: $cfg/bin/memev start"
  notes=$(vault_file)
  [ -n "$notes" ] || die "Memev is installed, but the notes folder is not recorded."
  port_now=$("$(node_bin)" -e 'const fs=require("fs"); const j=JSON.parse(fs.readFileSync(process.argv[1],"utf8")); if(!j.port) process.exit(1); process.stdout.write(String(j.port));' "$notes/.memev/daemon.json") || die "Memev is not running. Start it with: $cfg/bin/memev start"
  health=$("$(node_bin)" -e 'const port=process.argv[1]; fetch("http://127.0.0.1:"+port+"/healthz").then(async (r)=>{ const t=await r.text(); process.stdout.write(t); if(!r.ok || !/"ok"\s*:\s*true/.test(t)) process.exit(1); }).catch(()=>process.exit(1));' "$port_now") || die "Memev is not answering on this computer."
  say "Health: ok"
  say "$health"
  doctor=$("$cfg/bin/memev" doctor --json || true)
  printf '%s\n' "$doctor" | "$(node_bin)" -e 'let s=""; process.stdin.on("data",(d)=>{s+=d}); process.stdin.on("end",()=>{ const checks=JSON.parse(s); const care=["node","git","daemon","vault"]; const bad=checks.filter((c)=>care.includes(c.id)&&c.status==="fail"); for (const c of checks) { if (c.status==="fail") console.log("Needs attention: "+c.title+(c.detail?" ("+c.detail+")":"")); } if (bad.length) process.exit(1); });'
}

remove_tree() {
  target=$1
  if [ -L "$target" ]; then
    rm -f "$target"
    return
  fi
  if [ -e "$target" ]; then
    rm -rf "$target"
  fi
}

do_uninstall() {
  notes=$(vault_file || true)
  if [ ! -x "$cfg/bin/memev" ] && [ ! -d "$cfg/cli" ]; then
    say "Memev is not installed. No notes were deleted."
    exit 0
  fi
  if [ -x "$cfg/bin/memev" ]; then
    prepare_env
    "$cfg/bin/memev" stop || true
    "$cfg/bin/memev" uninstall || true
    "$cfg/bin/memev" path remove || true
  fi
  remove_tree "$cfg/runtime"
  remove_tree "$cfg/bin"
  remove_tree "$cfg/cli"
  remove_tree "$cfg/node"
  for d in "$cfg"/update-*; do
    [ -e "$d" ] || continue
    remove_tree "$d"
  done
  if [ -n "$notes" ]; then
    say "The program is removed. Your notes are still in ${notes}."
  else
    say "The program is removed. No notes folder was recorded, and no notes were deleted."
  fi
}

do_upgrade() {
  [ -f "$cfg/cli/INSTALLED" ] || die "Memev is not installed yet. Run the installer first."
  sumfile=${upgrade_file}.sha256
  [ -f "$sumfile" ] || die "The checksum file is missing, so nothing was updated. It should be next to the installer."
  expect=$(awk 'NR==1 { print $1; exit }' "$sumfile")
  actual=$(sha256sum "$upgrade_file" | awk 'NR==1 { print $1; exit }')
  if [ "$actual" != "$expect" ]; then
    die "This copy does not match its checksum, so nothing was updated. Download the installer again."
  fi
  tmp=$(mktemp -d)
  tar -xzf "$upgrade_file" -C "$tmp"
  set -- "$tmp"/*
  top=$1
  [ -f "$top/install" ] || { rm -rf "$tmp"; die "That file is not a Memev installer, so nothing was updated."; }
  MEMEV_CONFIG_DIR=$cfg "$top/install" install
  rm -rf "$tmp"
}

case $action in
  install) do_install ;;
  status) do_status ;;
  uninstall) do_uninstall ;;
  upgrade) do_upgrade ;;
  *) die "This installer does not take that option ($action)." ;;
esac
