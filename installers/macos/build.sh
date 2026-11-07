#!/bin/sh
# SPDX-License-Identifier: Apache-2.0
# Builds Memev-<version>-macos-<arch>.dmg: Memev.app with the core and the pinned Node inside, so nothing else has to be
# installed first. Every download is checked against its SHA-256 before use. Nothing is signed or published.
set -eu
here=$(CDPATH= cd -- "$(dirname "$0")" && pwd)
repo=$(CDPATH= cd -- "$here/../.." && pwd)
pins=$here/pins.json
pin() { node -e "const p=require(process.argv[1]); console.log(process.argv[2].split('.').reduce((o,k)=>o[k],p))" "$pins" "$1"; }
case $(uname -m) in
  x86_64) arch=x64 ;;
  arm64) arch=arm64 ;;
  *) echo "unsupported machine $(uname -m)" >&2; exit 1 ;;
esac
version=$(pin version)
work=$here/build
dist=$here/dist
rm -rf "$work"
mkdir -p "$work" "$dist"

# 1. The window, built from this repository.
make -C "$repo/native/macos" app >/dev/null
app=$work/stage/Memev.app
mkdir -p "$work/stage"
cp -R "$repo/native/macos/Memev.app" "$app"
res=$app/Contents/Resources
mkdir -p "$res/bin"

# 2. The core at the pinned commit, as npm would pack it.
commit=$(pin memev_commit)
git init -q "$work/core-src"
git -C "$work/core-src" fetch -q --depth 1 "$(pin memev_repository)" "$commit"
git -C "$work/core-src" checkout -q --detach FETCH_HEAD
[ "$(git -C "$work/core-src" rev-parse HEAD)" = "$commit" ] || { echo "the core is not at the pinned commit" >&2; exit 1; }
tgz=$(cd "$work/core-src" && npm pack --silent --pack-destination "$work")
mkdir -p "$res/core"
tar -xzf "$work/$tgz" -C "$res/core" --strip-components 1
[ -f "$res/core/bin/memev.mjs" ] || { echo "the packed core has no bin/memev.mjs" >&2; exit 1; }
# The external merge engine is never part of this package.
needle=$(printf '%s%s' 'cr''dt-' 'mer''ge')
if grep -rIl -e "$needle" "$res/core/bin" "$res/core/src" >/dev/null 2>&1; then
  echo "the packed core names the external engine" >&2
  exit 1
fi

# 3. The pinned Node program for this machine, checked before use.
url=$(pin "node.darwin-$arch.url")
sum=$(pin "node.darwin-$arch.sha256")
curl --proto '=https' --tlsv1.2 -fsSL "$url" -o "$work/node.tgz"
[ "$(shasum -a 256 "$work/node.tgz" | awk '{print $1}')" = "$sum" ] || { echo "the Node download does not match its SHA-256" >&2; exit 1; }
tar -xzf "$work/node.tgz" -C "$work"
nd=$(find "$work" -maxdepth 1 -type d -name 'node-v*' | head -1)
mkdir -p "$res/node/bin"
cp "$nd/bin/node" "$res/node/bin/node"
cp "$nd/LICENSE" "$res/node/LICENSE"

# 4. git, built here from its pinned source, so the Mac needs no developer tools. It links only libraries every Mac has, and finds
#    its own helpers relative to itself (RUNTIME_PREFIX), so it runs from inside the app wherever the app is.
gurl=$(pin git.url)
gsum=$(pin git.sha256)
curl --proto '=https' --tlsv1.2 -fsSL "$gurl" -o "$work/git.tgz"
[ "$(shasum -a 256 "$work/git.tgz" | awk '{print $1}')" = "$gsum" ] || { echo "the git source does not match its SHA-256" >&2; exit 1; }
tar -xzf "$work/git.tgz" -C "$work"
gsrc=$(find "$work" -maxdepth 1 -type d -name 'git-*' | head -1)
make -C "$gsrc" -j4 prefix=/ RUNTIME_PREFIX=YesPlease NO_GETTEXT=1 NO_TCLTK=1 NO_PERL=1 NO_PYTHON=1 NO_INSTALL_HARDLINKS=1 \
  INSTALL_SYMLINKS=1 DESTDIR="$res/git" install >"$work/git-build.log" 2>&1 || { tail -20 "$work/git-build.log" >&2; exit 1; }
cp "$gsrc/COPYING" "$res/git/COPYING"
if otool -L "$res/git/bin/git" | tail -n +2 | awk '{print $1}' | grep -vE '^(/usr/lib/|/System/Library/)' >/dev/null; then
  echo "the bundled git links a library outside the system" >&2
  exit 1
fi

# 5. The memev command inside the app, which the window uses when MEMEV_BIN is not set.
cat > "$res/bin/memev" <<'SH'
#!/bin/sh
here=$(CDPATH= cd -- "$(dirname "$0")/.." && pwd)
# the app's own git comes first, so the core never reaches for the system's
PATH="$here/git/bin:$PATH"
export PATH
exec "$here/node/bin/node" "$here/core/bin/memev.mjs" "$@"
SH
chmod +x "$res/bin/memev"
printf '%s\n' "memev $commit" "node $(pin node.version) darwin-$arch" "git $(pin git.version)" > "$res/PINS.txt"

# 6. The disk image: the app and a link to Applications.
ln -s /Applications "$work/stage/Applications"
out=$dist/Memev-$version-macos-$arch.dmg
rm -f "$out"
hdiutil create -quiet -volname "Memev" -srcfolder "$work/stage" -fs HFS+ -format UDZO "$out"
shasum -a 256 "$out" | awk '{print $1}' > "$out.sha256"
echo "$out"
