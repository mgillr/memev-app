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
# the oldest macOS the app supports, from the app's own Info.plist, so the two never disagree
minos=$(/usr/libexec/PlistBuddy -c 'Print :LSMinimumSystemVersion' "$repo/native/macos/Info.plist")
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
# The system's own tools only: a build machine's Homebrew curl or libraries must never reach the app (a Mac without them could not
# fetch or push over https). curl-config is macOS's own, so the https helpers link /usr/lib/libcurl.
# Built for the oldest macOS the app supports (minos), not the build machine's, so nothing in git relies on a newer macOS than the app promises.
env PATH=/usr/bin:/bin:/usr/sbin:/sbin MACOSX_DEPLOYMENT_TARGET="$minos" make -C "$gsrc" -j4 prefix=/ RUNTIME_PREFIX=YesPlease NO_GETTEXT=1 NO_TCLTK=1 NO_PERL=1 \
  NO_PYTHON=1 NO_RUST=YesPlease NO_INSTALL_HARDLINKS=1 INSTALL_SYMLINKS=1 CURL_CONFIG=/usr/bin/curl-config DESTDIR="$res/git" install \
  >"$work/git-build.log" 2>&1 || { tail -20 "$work/git-build.log" >&2; exit 1; }
cp "$gsrc/COPYING" "$res/git/COPYING"

# 5. The memev command inside the app, which the window uses when MEMEV_BIN is not set.
cat > "$res/bin/memev" <<'SH'
#!/bin/sh
here=$(CDPATH= cd -- "$(dirname "$0")/.." && pwd)
# the app's own git comes first, so the core never reaches for the system's
PATH="$here/git/bin:$PATH"
# the core writes its own launcher, which AI apps and the login service run; it puts these folders first on PATH too
MEMEV_TOOLS_PATH="$here/git/bin"
# no system-wide git settings reach the notes repository
GIT_CONFIG_NOSYSTEM=1
export PATH MEMEV_TOOLS_PATH GIT_CONFIG_NOSYSTEM
exec "$here/node/bin/node" "$here/core/bin/memev.mjs" "$@"
SH
chmod +x "$res/bin/memev"
printf '%s\n' "memev $commit" "node $(pin node.version) darwin-$arch" "git $(pin git.version)" > "$res/PINS.txt"

# Every program in the app links only libraries that every Mac has: git and each of its helpers, Node, and the window.
bad=0
for f in $(find "$app" -type f -perm -u+x); do
  file -b "$f" | grep -q 'Mach-O' || continue
  for lib in $(otool -L "$f" | tail -n +2 | awk '{print $1}'); do
    case "$lib" in /usr/lib/*|/System/Library/*|@rpath/*|@executable_path/*|@loader_path/*) ;; *) echo "${f#$app/} links $lib" >&2; bad=1 ;; esac
  done
done
[ "$bad" = 0 ] || { echo "a program in the app links a library outside the system" >&2; exit 1; }

# Every program in the app starts on the oldest supported macOS: none asks for a newer one than $minos.
for f in $(find "$app" -type f -perm -u+x); do
  file -b "$f" | grep -q 'Mach-O' || continue
  for v in $(otool -l "$f" | awk '/LC_BUILD_VERSION/{b=1} b&&$1=="minos"{print $2; b=0} /LC_VERSION_MIN_MACOSX/{m=1} m&&$1=="version"{print $2; m=0}'); do
    [ "$(printf '%s\n%s\n' "$v" "$minos" | sort -t. -k1,1n -k2,2n | tail -1)" = "$minos" ] || { echo "${f#$app/} needs macOS $v, newer than $minos" >&2; bad=1; }
  done
done
[ "$bad" = 0 ] || { echo "a program in the app needs a newer macOS than the app supports" >&2; exit 1; }

# 6. The disk image: the app and a link to Applications.
ln -s /Applications "$work/stage/Applications"
out=$dist/Memev-$version-macos-$arch.dmg
rm -f "$out"
hdiutil create -quiet -volname "Memev" -srcfolder "$work/stage" -fs HFS+ -format UDZO "$out"
shasum -a 256 "$out" | awk '{print $1}' > "$out.sha256"
echo "$out"
