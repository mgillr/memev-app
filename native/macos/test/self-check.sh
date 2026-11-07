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
      'screen=health|Health|' 'confirm_amber=0' 'confirm_main=red' 'sheet_amber=1'; do
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
