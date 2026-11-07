#!/bin/sh
# SPDX-License-Identifier: Apache-2.0
# Runs the macOS window and checks the first slice, with the same fixture and the same values as the Linux self-check.
# It opens a real window for about a second, so it runs in CI and is not meant for a person's own screen.
set -eu
root=$(CDPATH= cd -- "$(dirname "$0")/.." && pwd)
fix=$root/../linux/test/fixtures/memev
for bin in "$root/memev-desktop" "$root/Memev.app/Contents/MacOS/memev-desktop"; do
  "$bin" --version | grep -F 'memev-desktop 0.0.1-slice1' >/dev/null
  for theme in dark light; do
    out=$(MEMEV_BIN="$fix" MEMEV_HEALTH_URL=off MEMEV_THEME=$theme MEMEV_SELF_CHECK=1 "$bin")
    printf '%s\n' "$out"
    for want in \
      'overview=Memev 0.1.0 is running for demo. 3 notes are in the index. 1 check to look at.' \
      'health=1 check to look at.' 'star=Ship the window' 'attention=1' 'checks=2' 'primary=1' \
      'minw=1024' 'minh=700' 'sidebar=240' 'accel=1' 'match=1' 'secret=absent' 'visible=1' "theme=$theme"; do
      if ! printf '%s\n' "$out" | grep -Fx "$want" >/dev/null; then
        echo "missing: $want" >&2
        exit 1
      fi
    done
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
env -u MEMEV_SELF_CHECK -u MEMEV_SELF_CHECK_PNG MEMEV_BIN="$fix" MEMEV_HEALTH_URL=off MEMEV_THEME=dark "$live" >/dev/null 2>&1 &
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
