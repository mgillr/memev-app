#!/bin/sh
# SPDX-License-Identifier: Apache-2.0
# The self-check's network guard must fail when the window is not running. A stand-in that exits at once must make it fail.
set -eu
root=$(CDPATH= cd -- "$(dirname "$0")/.." && pwd)
stub=$(mktemp)
printf '#!/bin/sh\nexit 3\n' > "$stub"
chmod +x "$stub"
err=$(mktemp)
if MEMEV_DESKTOP_BIN="$stub" "$root/test/self-check.sh" >/dev/null 2>"$err"; then
  rm -f "$stub" "$err"
  echo "the self-check passed with a window that exited at once" >&2
  exit 1
fi
# It must fail for that reason, not for another one earlier in the self-check.
if ! grep -F 'the window exited (3) before its sockets were read' "$err" >/dev/null; then
  cat "$err" >&2
  rm -f "$stub" "$err"
  echo "the self-check failed, but not on the exited window" >&2
  exit 1
fi
rm -f "$stub" "$err"
echo "guard check passed"
