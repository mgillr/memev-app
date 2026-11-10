#!/bin/sh
# SPDX-License-Identifier: Apache-2.0
# Installs the disk image into a temporary Applications folder and home, with failing node, npm, npx, memev and git first on PATH, then: the bundled core inits a notes folder and
# starts, status and doctor answer, the window reads that live core, the core stops, and uninstall leaves no process behind.
# The window step opens a real window for about a second; set MEMEV_SKIP_WINDOW=1 to leave it out on a person's own screen.
set -eu
dmg=$1
t=$(mktemp -d)
mnt=$t/mnt
mkdir -p "$mnt" "$t/Applications" "$t/home"
hdiutil attach -quiet -nobrowse -readonly -mountpoint "$mnt" "$dmg"
[ -L "$mnt/Applications" ] || { echo "the image has no Applications link" >&2; exit 1; }
cp -R "$mnt/Memev.app" "$t/Applications/"
hdiutil detach -quiet "$mnt"
app=$t/Applications/Memev.app
bin=$app/Contents/Resources/bin/memev
# The design's fonts reached the image unchanged, with their licences, and the window finds them there.
for f in SpaceGrotesk-wght.ttf IBMPlexMono-Regular.ttf IBMPlexMono-Medium.ttf OFL-SpaceGrotesk.txt OFL-IBMPlexMono.txt; do
  grep -F "  $f" "$app/Contents/Resources/fonts/SHA256SUMS" >/dev/null || { echo "the image's font list has no $f" >&2; exit 1; }
done
(cd "$app/Contents/Resources/fonts" && shasum -a 256 -c SHA256SUMS >/dev/null) || { echo "a font in the image does not match its SHA-256" >&2; exit 1; }
"$app/Contents/MacOS/memev-desktop" --fonts | grep -Fx 'font_sans=Space Grotesk' >/dev/null || { echo "the installed window does not use Space Grotesk" >&2; exit 1; }
"$app/Contents/MacOS/memev-desktop" --fonts | grep -Fx 'font_mono=IBM Plex Mono' >/dev/null || { echo "the installed window does not use IBM Plex Mono" >&2; exit 1; }
export HOME=$t/home
unset MEMEV_BIN NODE_OPTIONS NODE_PATH || true
# No node, npm or memev from this machine: stand-ins that fail come first on PATH.
mkdir -p "$t/deny"
for c in node npm npx memev git; do printf '#!/bin/sh\necho "%s from PATH was used" >&2\nexit 97\n' "$c" > "$t/deny/$c"; chmod +x "$t/deny/$c"; done
export PATH=$t/deny:/usr/bin:/bin:/usr/sbin:/sbin
port=$("$app/Contents/Resources/node/bin/node" -e "const s=require('net').createServer().listen(0,'127.0.0.1',()=>{console.log(s.address().port);s.close()})")
"$bin" init --root "$HOME/notes" --local-only --no-connect --no-service --port "$port" > "$t/init.txt" 2>&1 || { cat "$t/init.txt" >&2; exit 1; }
"$app/Contents/Resources/git/bin/git" --version | grep -E '^git version [0-9]' >/dev/null || { echo "the bundled git does not run" >&2; exit 1; }
[ -d "$HOME/notes/.git" ] || { echo "init made no git repository with the bundled git" >&2; exit 1; }
"$bin" status --json > "$t/status.json"
"$app/Contents/Resources/node/bin/node" -e "const s=require(process.argv[1]); if(!s.version) process.exit(1); console.log('core', s.version)" "$t/status.json"
"$bin" doctor --json > "$t/doctor.json" || true
grep -q '"checks"\|^\[' "$t/doctor.json" || { echo "doctor gave no checks" >&2; cat "$t/doctor.json" >&2; exit 1; }
if [ "${MEMEV_SKIP_WINDOW:-}" != 1 ]; then
  out=$(MEMEV_HEALTH_URL="http://127.0.0.1:$port/healthz" MEMEV_THEME=dark MEMEV_SELF_CHECK=1 "$app/Contents/MacOS/memev-desktop")
  printf '%s\n' "$out"
  printf '%s\n' "$out" | grep -E '^overview=Memev [0-9.]+ is running' >/dev/null || { echo "the window did not read the running core" >&2; exit 1; }
  printf '%s\n' "$out" | grep -Fx 'match=1' >/dev/null
  printf '%s\n' "$out" | grep -Fx 'secret=absent' >/dev/null
  # Each page over the live core: it answers, shows its sections and leaks no path. Memory finds a note saved just now.
  "$bin" remember "the window reads this note" --force >/dev/null 2>&1
  for page in memory checked bots projects settings; do
    pout=$(MEMEV_GUIDE=off MEMEV_HEALTH_URL="http://127.0.0.1:$port/healthz" MEMEV_THEME=dark MEMEV_SELF_CHECK=1 MEMEV_SELF_CHECK_PAGE=$page \
      MEMEV_SELF_CHECK_QUERY="window reads" "$app/Contents/MacOS/memev-desktop") || { printf '%s\n' "$pout" >&2; echo "page $page failed" >&2; exit 1; }
    printf '%s\n' "$pout"
    printf '%s\n' "$pout" | grep -Fx "page=$page" >/dev/null && printf '%s\n' "$pout" | grep -Fx 'leak=absent' >/dev/null || { echo "page $page: bad report" >&2; exit 1; }
    if printf '%s\n' "$pout" | grep -E '^(row|empty)=' | grep -Ei 'did not answer|could not be read' >/dev/null; then echo "page $page: a command did not answer" >&2; exit 1; fi
  done
  # The page check must be able to fail: against a memev that always fails, every section says it did not answer.
  printf '#!/bin/sh\nexit 1\n' > "$t/broken-memev"; chmod +x "$t/broken-memev"
  bad=$(MEMEV_BIN="$t/broken-memev" MEMEV_GUIDE=off MEMEV_HEALTH_URL=off MEMEV_SELF_CHECK=1 MEMEV_SELF_CHECK_PAGE=checked "$app/Contents/MacOS/memev-desktop" || true)
  printf '%s\n' "$bad" | grep -E '^empty=.*did not answer' >/dev/null || { printf '%s\n' "$bad" >&2; echo "a failing core was not reported on the page" >&2; exit 1; }
  printf '%s\n' "$(MEMEV_GUIDE=off MEMEV_HEALTH_URL=off MEMEV_SELF_CHECK=1 MEMEV_SELF_CHECK_PAGE=memory MEMEV_SELF_CHECK_QUERY="window reads" "$app/Contents/MacOS/memev-desktop")" | grep -F 'row=the window reads this note' >/dev/null || { echo "memory search did not find the saved note" >&2; exit 1; }
fi
"$bin" stop > /dev/null 2>&1 || true
# Start Memev from the window (owner ruling D-1): its sheet's main button runs the bundled core's start, and the window then
# reads a running core. The window's own card read can start the core as well, so this proves the start answers 0 and the
# core runs after it, not which of the two started it.
if [ "${MEMEV_SKIP_WINDOW:-}" != 1 ]; then
  sout=$(MEMEV_GUIDE=off MEMEV_HEALTH_URL="http://127.0.0.1:$port/healthz" MEMEV_THEME=light MEMEV_SELF_CHECK=1 MEMEV_SELF_CHECK_START=1 \
    "$app/Contents/MacOS/memev-desktop") || { printf '%s\n' "$sout" >&2; echo "the start check failed" >&2; exit 1; }
  printf '%s\n' "$sout"
  for want in 'start_sheet=Start Memev?|Start Memev' 'start_rc=0' 'start_result=Memev is running' 'start_after=running' 'start_banner=hidden'; do
    printf '%s\n' "$sout" | grep -Fx "$want" >/dev/null || { echo "start from the window, missing: $want" >&2; exit 1; }
  done
  "$bin" status --json >/dev/null || { echo "the core is not running after the window started it" >&2; exit 1; }
  "$bin" stop > /dev/null 2>&1 || true
fi
# The guided first launch, headless, against the app's own core: a fresh notes folder, a note saved and read back, and the
# overview from the running core. proof is native/macos's build of the shared guide.
if [ -n "${MEMEV_PROOF:-}" ]; then
  mkdir -p "$t/proof-home" "$t/proof-config"
  pport=$("$app/Contents/Resources/node/bin/node" -e "const s=require('net').createServer().listen(0,'127.0.0.1',()=>{console.log(s.address().port);s.close()})")
  HOME=$t/proof-home MEMEV_CONFIG_DIR=$t/proof-config MEMEV_NO_SERVICE=1 MEMEV_NO_TUNNEL=1 MEMEV_NO_BACKGROUND=1 MEMEV_NO_DOWNLOAD=1 \
    MEMEV_GUIDE_PORT=$pport "$MEMEV_PROOF" "$bin" "$t/proof-notes" || { HOME=$t/proof-home "$bin" stop >/dev/null 2>&1 || true; echo "the first-launch proof failed" >&2; exit 1; }
  HOME=$t/proof-home "$bin" stop >/dev/null 2>&1 || true
  if git -C "$t/proof-notes" remote get-url origin >/dev/null 2>&1; then echo "a remote was recorded without a tick" >&2; exit 1; fi
fi
sleep 1
rm -rf "$app"
if pgrep -f "$t/Applications/Memev.app" >/dev/null; then
  echo "a process from the app is still running after uninstall" >&2
  pgrep -fl "$t/Applications/Memev.app" >&2
  exit 1
fi
[ -d "$HOME/notes" ] || { echo "the notes folder was not kept" >&2; exit 1; }
rm -rf "$t"
echo "install check passed"
