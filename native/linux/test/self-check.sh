#!/bin/sh
# SPDX-License-Identifier: Apache-2.0
# Runs the Linux window under a virtual display and checks the first slice.
set -eu
root=$(CDPATH= cd -- "$(dirname "$0")/.." && pwd)
bin=$root/memev-desktop
fix=$root/test/fixtures/memev
ver=$("$bin" --version)
printf '%s\n' "$ver" | grep -F 'memev-desktop 0.0.1-slice1' >/dev/null

out=$(NO_AT_BRIDGE=1 MEMEV_BIN="$fix" MEMEV_HEALTH_URL=off MEMEV_THEME=dark MEMEV_SELF_CHECK=1 \
  xvfb-run -a "$bin")
printf '%s\n' "$out"
printf '%s\n' "$out" | grep -F 'overview=Memev 0.1.0 is running for demo. 3 notes are in the index. 1 check to look at.' >/dev/null
printf '%s\n' "$out" | grep -F 'health=1 check to look at.' >/dev/null
printf '%s\n' "$out" | grep -F 'star=Ship the window' >/dev/null
printf '%s\n' "$out" | grep -F 'attention=1' >/dev/null
printf '%s\n' "$out" | grep -F 'checks=2' >/dev/null
printf '%s\n' "$out" | grep -F 'primary=1' >/dev/null
printf '%s\n' "$out" | grep -F 'minw=1024' >/dev/null
printf '%s\n' "$out" | grep -F 'minh=700' >/dev/null
printf '%s\n' "$out" | grep -F 'sidebar=240' >/dev/null
printf '%s\n' "$out" | grep -F 'accel=1' >/dev/null
printf '%s\n' "$out" | grep -F 'match=1' >/dev/null
printf '%s\n' "$out" | grep -F 'secret=absent' >/dev/null
focus=$(printf '%s\n' "$out" | sed -n 's/^focusable=//p')
if [ "${focus:-0}" -lt 9 ]; then
  echo "too few focusable controls: $focus" >&2
  exit 1
fi
if printf '%s\n' "$out" | grep -F 'should-not-appear' >/dev/null; then
  echo "a hidden field reached the window text" >&2
  exit 1
fi
if printf '%s\n' "$out" | grep -F 'vault-marker' >/dev/null; then
  echo "a vault path reached the window text" >&2
  exit 1
fi
# Built at run time so this file does not itself contain a forbidden name.
needle=$(printf '%s%s' 'mem''ev-' 'mer''ge')
other=$(printf '%s%s' 'cr''dt-' 'mer''ge')
if grep -a -F "$needle" "$bin" >/dev/null || grep -a -F "$other" "$bin" >/dev/null; then
  echo "binary names a forbidden engine token" >&2
  exit 1
fi

# A live process with health checks off must not hold a non-loopback TCP socket.
NO_AT_BRIDGE=1 MEMEV_BIN="$fix" MEMEV_HEALTH_URL=off MEMEV_THEME=dark \
  xvfb-run -a sh -c '
    "$1" >/tmp/memev-desktop-out.txt 2>/tmp/memev-desktop-err.txt &
    pid=$!
    sleep 1
    if command -v lsof >/dev/null 2>&1; then
      lsof -nP -a -p "$pid" -iTCP > /tmp/memev-desktop-lsof.txt || true
      if grep -E "ESTABLISHED|SYN_SENT" /tmp/memev-desktop-lsof.txt | grep -v "127.0.0.1" | grep -v "\\[::1\\]" >/dev/null; then
        echo "non-loopback socket" >&2
        cat /tmp/memev-desktop-lsof.txt >&2
        kill "$pid" || true
        exit 1
      fi
    else
      echo "lsof missing; socket check skipped" >&2
      exit 2
    fi
    kill "$pid" || true
    wait "$pid" || true
  ' sh "$bin"

echo "self-check passed"
