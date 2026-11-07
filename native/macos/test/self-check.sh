#!/bin/sh
# SPDX-License-Identifier: Apache-2.0
# Runs the macOS window and checks the rev2 shell, with the same fixture and the same values as the Linux self-check, then the
# shell's own parts: the 14 pips, every screen's title and tabs, the one-amber rule on every tab, and the sheets.
# It opens a real window for about a second, so it runs in CI and is not meant for a person's own screen.
set -eu
root=$(CDPATH= cd -- "$(dirname "$0")/.." && pwd)
fix=$root/../linux/test/fixtures/memev
for bin in "$root/memev-desktop" "$root/Memev.app/Contents/MacOS/memev-desktop"; do
  "$bin" --version | grep -F 'memev-desktop 0.0.1-slice1' >/dev/null
  # The design's fonts are found next to the binary (native/macos/fonts) or in the app's Resources/fonts, with no window.
  fonts=$("$bin" --fonts)
  for want in 'font_sans=Space Grotesk' 'font_mono=IBM Plex Mono' 'font_weights=400,500,600' 'font_mono_faces=IBMPlexMono-Regular,IBMPlexMono-Medium'; do
    printf '%s\n' "$fonts" | grep -Fx "$want" >/dev/null || { printf '%s\n' "$fonts" >&2; echo "fonts missing: $want ($bin)" >&2; exit 1; }
  done
  for theme in dark light; do
    # The window's own verdict is its exit code; its lines are printed first, so a failure shows which value was wrong.
    out=$(MEMEV_BIN="$fix" MEMEV_HEALTH_URL=off MEMEV_THEME=$theme MEMEV_GUIDE=off MEMEV_SELF_CHECK=1 "$bin" 2>&1) && rc=0 || rc=$?
    printf '%s\n' "$out"
    if [ "$rc" -ne 0 ]; then
      echo "the window's self-check exited $rc ($bin, $theme)" >&2
      exit 1
    fi
    for want in \
      'overview=Memev 0.1.0 is running for demo. 3 notes are in the index. 1 check to look at.' \
      'health=1 check to look at.' 'star=Ship the window' 'attention=1' 'checks=2' 'primary=1' \
      'minw=1024' 'minh=700' 'sidebar=240' 'accel=1' 'match=1' 'secret=absent' 'visible=1' "theme=$theme" \
      'pips=14' 'pip=Backup:needs' 'pip=Running:fine' 'pip=Shared:unread' \
      'screen=overview|Executive overview|' 'screen=operations|Operations|Workspace,Messages' 'screen=projects|Projects|' \
      'screen=bots|Bots and agents|Team,Connect,Access' 'screen=memory|Memory|Search,Review,Browse,History,Health' \
      'screen=checked|Checked work|' 'screen=settings|Settings|General,Plan and computers,Storage and backup,Advanced,Help and support' \
      'screen=health|Health|' 'confirm_amber=0' 'confirm_main=red' 'sheet_amber=1' 'sheet_ring=1' 'amber_ring=1' \
      'start_sheet=Start Memev?|Start Memev' 'reads=6' 'font_sans=Space Grotesk' 'font_mono=IBM Plex Mono' 'font_weights=400,500,600'; do
      if ! printf '%s\n' "$out" | grep -Fx "$want" >/dev/null; then
        echo "missing: $want" >&2
        exit 1
      fi
    done
    # primary= counts the filled amber buttons a person can see on the Overview; amber_max= is the most on any screen and tab.
    # The rule (SH-20) is at most one, and every screen but Operations has one, so the most is exactly one.
    if ! printf '%s\n' "$out" | grep -Fx 'amber_max=1' >/dev/null; then
      echo "a screen shows other than one amber button at most: $(printf '%s\n' "$out" | sed -n 's/^amber_max=//p')" >&2
      exit 1
    fi
    if [ "$(printf '%s\n' "$out" | grep -c '^pip=')" -ne 14 ]; then
      echo "the health button does not carry 14 pips" >&2
      exit 1
    fi
    focus=$(printf '%s\n' "$out" | sed -n 's/^focusable=//p')
    if [ "${focus:-0}" -lt 9 ]; then
      echo "too few focusable controls: $focus" >&2
      exit 1
    fi
    if printf '%s\n' "$out" | grep -F -e 'should-not-appear' -e 'vault-marker' >/dev/null; then
      echo "a hidden field or a vault path reached the window text" >&2
      exit 1
    fi
  done
done
# The guided first launch: its first step, with Continue as the one filled button a person can see.
guide=$(MEMEV_BIN="$fix" MEMEV_HEALTH_URL=off MEMEV_THEME=dark MEMEV_GUIDE=1 MEMEV_SELF_CHECK=1 "$root/memev-desktop" 2>&1) && rc=0 || rc=$?
printf '%s\n' "$guide"
[ "$rc" -eq 0 ] || { echo "the guide's self-check exited $rc" >&2; exit 1; }
for want in 'guide=Is this your first computer?' 'primary_label=Continue' 'primary=1' 'visible=1'; do
  printf '%s\n' "$guide" | grep -Fx "$want" >/dev/null || { echo "guide missing: $want" >&2; exit 1; }
done
# Fonts that cannot be registered fall back to the system fonts: a copy of the binary with no fonts beside it still runs.
bare=$(mktemp -d)
cp "$root/memev-desktop" "$bare/memev-desktop"
"$bare/memev-desktop" --fonts | grep -Fx 'font_sans=Space Grotesk' >/dev/null && { echo "the copy with no fonts still reports Space Grotesk" >&2; exit 1; }
out=$(MEMEV_BIN="$fix" MEMEV_HEALTH_URL=off MEMEV_THEME=light MEMEV_GUIDE=off MEMEV_SELF_CHECK=1 "$bare/memev-desktop" 2>&1) && rc=0 || rc=$?
printf '%s\n' "$out"
[ "$rc" -eq 0 ] || { echo "the window with system fonts exited $rc" >&2; exit 1; }
printf '%s\n' "$out" | grep -Fx 'font_sans=Space Grotesk' >/dev/null && { echo "the window with no fonts claims Space Grotesk" >&2; exit 1; }

# The window shows before the core answers: every core command here waits 2 s. Reading on the main thread would hold the
# window back for the reader's status, doctor and card, then six more reads (well over 10 s). It must show within 3 s, and
# the self-check still waits for all six reads before it prints.
printf '#!/bin/sh\nsleep 2\nexec "%s" "$@"\n' "$fix" > "$bare/slow-memev"
chmod +x "$bare/slow-memev"
out=$(MEMEV_BIN="$bare/slow-memev" MEMEV_HEALTH_URL=off MEMEV_THEME=dark MEMEV_GUIDE=off MEMEV_SELF_CHECK=1 "$root/memev-desktop" 2>&1) && rc=0 || rc=$?
printf '%s\n' "$out"
[ "$rc" -eq 0 ] || { echo "the window over a slow core exited $rc" >&2; exit 1; }
shown=$(printf '%s\n' "$out" | sed -n 's/^shown_ms=//p')
[ -n "$shown" ] && [ "$shown" -ge 0 ] && [ "$shown" -lt 3000 ] || { echo "the window took ${shown:-?} ms to show over a slow core" >&2; exit 1; }
for want in 'reads=6' 'overview=Memev 0.1.0 is running for demo. 3 notes are in the index. 1 check to look at.' 'pips=14'; do
  printf '%s\n' "$out" | grep -Fx "$want" >/dev/null || { echo "slow core, missing: $want" >&2; exit 1; }
done

# Start Memev (owner ruling D-1): the sheet names what changes, its main button runs the core's start, and the window shows the
# core's answer. The fixture has no start, so it must say it did not start; a stand-in whose start succeeds must say it runs.
out=$(MEMEV_BIN="$fix" MEMEV_HEALTH_URL=off MEMEV_THEME=dark MEMEV_GUIDE=off MEMEV_SELF_CHECK=1 MEMEV_SELF_CHECK_START=1 "$root/memev-desktop" 2>&1) && rc=0 || rc=$?
printf '%s\n' "$out"
[ "$rc" -eq 0 ] || { echo "the start check exited $rc" >&2; exit 1; }
for want in 'start_sheet=Start Memev?|Start Memev' 'start_rc=2' 'start_result=Memev did not start' 'start_status=Memev did not start.'; do
  printf '%s\n' "$out" | grep -Fx "$want" >/dev/null || { echo "start, failing core, missing: $want" >&2; exit 1; }
done
printf '#!/bin/sh\n[ "${1:-}" = start ] && exit 0\nexec "%s" "$@"\n' "$fix" > "$bare/start-memev"
chmod +x "$bare/start-memev"
out=$(MEMEV_BIN="$bare/start-memev" MEMEV_HEALTH_URL=off MEMEV_THEME=light MEMEV_GUIDE=off MEMEV_SELF_CHECK=1 MEMEV_SELF_CHECK_START=1 "$root/memev-desktop" 2>&1) && rc=0 || rc=$?
printf '%s\n' "$out"
[ "$rc" -eq 0 ] || { echo "the start check exited $rc" >&2; exit 1; }
for want in 'start_rc=0' 'start_result=Memev is running' 'start_status=Memev is running.' 'start_after=running'; do
  printf '%s\n' "$out" | grep -Fx "$want" >/dev/null || { echo "start, starting core, missing: $want" >&2; exit 1; }
done
rm -rf "$bare"

# Built at run time so this file does not itself contain a forbidden name.
needle=$(printf '%s%s' 'mem''ev-' 'mer''ge')
other=$(printf '%s%s' 'cr''dt-' 'mer''ge')
if grep -a -F -e "$needle" -e "$other" "$root/memev-desktop" >/dev/null; then
  echo "binary names a forbidden engine token" >&2
  exit 1
fi
plutil -lint "$root/Memev.app/Contents/Info.plist" >/dev/null

# A live window with health checks off must hold no TCP socket other than loopback. The window must still be running when its
# sockets are read, or the check would pass on a process that is gone. MEMEV_DESKTOP_BIN replaces the binary for the test of this
# guard (test/guard-check.sh).
live=${MEMEV_DESKTOP_BIN:-$root/memev-desktop}
list=$(mktemp)
env -u MEMEV_SELF_CHECK -u MEMEV_SELF_CHECK_PNG MEMEV_BIN="$fix" MEMEV_HEALTH_URL=off MEMEV_THEME=dark MEMEV_GUIDE=off "$live" >/dev/null 2>&1 &
pid=$!
sleep 2
if ! kill -0 "$pid" 2>/dev/null; then
  wait "$pid" 2>/dev/null && code=0 || code=$?
  echo "the window exited ($code) before its sockets were read" >&2
  rm -f "$list"
  exit 1
fi
# lsof exits 1 when the process holds no TCP socket at all, which is a pass; any other failure is not.
lsof -nP -a -p "$pid" -iTCP > "$list" 2>/dev/null && lrc=0 || lrc=$?
alive=0
kill -0 "$pid" 2>/dev/null && alive=1
kill "$pid" 2>/dev/null || true
wait "$pid" 2>/dev/null || true
if [ "$alive" -ne 1 ] || [ "$lrc" -gt 1 ]; then
  echo "the window's sockets could not be read (lsof $lrc, alive $alive)" >&2
  rm -f "$list"
  exit 1
fi
if grep -E "ESTABLISHED|SYN_SENT" "$list" | grep -v "127.0.0.1" | grep -v "\[::1\]" >/dev/null; then
  echo "non-loopback socket" >&2
  cat "$list" >&2
  rm -f "$list"
  exit 1
fi
rm -f "$list"

echo "self-check passed"
