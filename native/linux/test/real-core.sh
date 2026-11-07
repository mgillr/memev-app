#!/bin/sh
# SPDX-License-Identifier: Apache-2.0
# Proof against a core checkout. The fixture in test/fixtures is not used.
set -eu
root=$(CDPATH= cd -- "$(dirname "$0")/.." && pwd)
core=${MEMEV_CORE:-/tmp/memev-core}
bin=$core/bin/memev.mjs
if [ ! -f "$bin" ]; then
  echo "core checkout is missing at $core" >&2
  exit 2
fi
chmod +x "$bin"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT
mkdir -p "$work/home" "$work/config" "$work/vault"
export HOME=$work/home
export MEMEV_CONFIG_DIR=$work/config
export MEMEV_NO_SERVICE=1
export MEMEV_NO_TUNNEL=1
export MEMEV_NO_BACKGROUND=1
export MEMEV_NO_DOWNLOAD=1
export MEMEV_GUIDE_PORT=${MEMEV_GUIDE_PORT:-18799}
set +e
"$root/proof" "$bin" "$work/vault"
rc=$?
set -e
"$bin" stop >/dev/null 2>&1 || true
if [ "$rc" -ne 0 ]; then
  exit "$rc"
fi
if git -C "$work/vault" remote get-url origin >/dev/null 2>&1; then
  echo "a remote was recorded without a confirm" >&2
  exit 1
fi
echo "real-core proof passed"
