#!/bin/sh
# SPDX-License-Identifier: Apache-2.0
# Installs into a temporary home and checks the personal vault and the Operations prompt.
set -eu
here=$(CDPATH= cd -- "$(dirname "$0")" && pwd)
install=$here/../install.sh
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
mkdir -p "$work/user"
export HOME=$work/user
prefix=$work/prefix
notes=$HOME/notes
extra=
if [ -n "${MEMEV_CORE:-}" ]; then
  extra="$extra --core $MEMEV_CORE"
fi
if [ "${MEMEV_SYSTEM_NODE:-}" = "1" ]; then
  extra="$extra --system-node"
fi
# shellcheck disable=SC2086
"$install" --prefix "$prefix" --notes "$notes" $extra
# A second run keeps the vault and replaces the same files.
# shellcheck disable=SC2086
"$install" --prefix "$prefix" --notes "$notes" $extra
test -x "$prefix/bin/memev"
test -x "$prefix/bin/memev-desktop"
test -f "$prefix/share/applications/memev.desktop"
test -d "$notes/.memev"
plan=$("$prefix/bin/memev" plan --json)
printf '%s\n' "$plan" | grep -F '"plan"' >/dev/null
status=$("$prefix/bin/memev" status --json)
printf '%s\n' "$status" | grep -E '"version"' >/dev/null
out=$(MEMEV_SELF_CHECK=1 MEMEV_HEALTH_URL=off xvfb-run -a "$prefix/bin/memev-desktop")
printf '%s\n' "$out"
printf '%s\n' "$out" | grep -Fx 'page=Operations' >/dev/null
printf '%s\n' "$out" | grep -Fx 'prompt=Tell your lead agent what you want done, or ask a question' >/dev/null
printf '%s\n' "$out" | grep -F 'lead_sentence=' >/dev/null
printf '%s\n' "$out" | grep -Fx 'secret=absent' >/dev/null
test -f "$HOME/.config/memev/desktop-first-launch-done"
"$prefix/bin/memev" stop >/dev/null 2>&1 || true
echo "linux install check passed"
