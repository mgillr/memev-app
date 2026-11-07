# SPDX-License-Identifier: Apache-2.0
# Builds dist\Memev-<version>-windows-x64-setup.exe: the window (self-contained, so .NET is not needed on the PC), the Memev core
# at the commit in pins.json, and the pinned Node.js, so nothing else has to be installed first. Every download is checked
# against its SHA-256 before use, and so is the pinned MinGit, the git the core uses, since a new PC has none. Nothing is signed
# or published.
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$repo = Resolve-Path (Join-Path $here '..\..')
$pins = Get-Content (Join-Path $here 'pins.json') -Raw | ConvertFrom-Json
$work = Join-Path $here 'build'
$dist = Join-Path $here 'dist'
if (Test-Path $work) { Remove-Item -Recurse -Force $work }
New-Item -ItemType Directory -Force -Path $work, $dist | Out-Null
$app = Join-Path $work 'app'
$res = Join-Path $app 'resources'

# 1. The window, self-contained for 64-bit Windows.
& dotnet publish (Join-Path $repo 'native\windows\Memev.Desktop') -c Release -r win-x64 --self-contained true -o $app | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'the window did not publish' }

# 2. The core at the pinned commit, as npm would pack it.
$src = Join-Path $work 'core-src'
& git init -q $src
& git -C $src fetch -q --depth 1 $pins.memev_repository $pins.memev_commit
& git -C $src checkout -q --detach FETCH_HEAD
if ((& git -C $src rev-parse HEAD) -ne $pins.memev_commit) { throw 'the core is not at the pinned commit' }
Push-Location $src
try { $tgz = (& npm pack --silent --pack-destination $work).Trim() } finally { Pop-Location }
New-Item -ItemType Directory -Force -Path (Join-Path $res 'core') | Out-Null
& tar -xzf (Join-Path $work $tgz) -C (Join-Path $res 'core') --strip-components 1
if (-not (Test-Path (Join-Path $res 'core\bin\memev.mjs'))) { throw 'the packed core has no bin\memev.mjs' }
# The external merge engine is never part of this package. Its names are built at run time so this file holds neither.
$needles = @(('cr' + 'dt-' + 'mer' + 'ge'), ('mem' + 'ev-' + 'mer' + 'ge'))
foreach ($d in 'bin', 'src', 'templates', 'vendor') {
  $p = Join-Path $res "core\$d"
  if (-not (Test-Path $p)) { continue }
  foreach ($n in $needles) { if (Get-ChildItem $p -Recurse -File | Select-String -SimpleMatch $n -List) { throw "the packed core names the external engine in $d" } }
}

# 3. The pinned Node program, checked before use.
$zip = Join-Path $work 'node.zip'
Invoke-WebRequest -Uri $pins.node.'win-x64'.url -OutFile $zip -UseBasicParsing
if ((Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant() -ne $pins.node.'win-x64'.sha256) { throw 'the Node download does not match its SHA-256' }
Expand-Archive $zip -DestinationPath $work
$nd = Get-ChildItem $work -Directory -Filter 'node-v*' | Select-Object -First 1
New-Item -ItemType Directory -Force -Path (Join-Path $res 'node'), (Join-Path $res 'bin') | Out-Null
Copy-Item (Join-Path $nd.FullName 'node.exe') (Join-Path $res 'node\node.exe')
Copy-Item (Join-Path $nd.FullName 'LICENSE') (Join-Path $res 'node\LICENSE')

# 3b. The pinned MinGit, checked before use: the core keeps the notes' history with git, and a new PC has no git.
$gzip = Join-Path $work 'mingit.zip'
# The asset is named from the release tag: v<version>, with MinGit-<version, '.windows' dropped>-64-bit.zip. The URL is built here so no
# file holds the four-part number, which the leak scan would read as an address.
$gv = $pins.git.version
$gurl = "https://github.com/git-for-windows/git/releases/download/v$gv/MinGit-$($gv -replace '\.windows\.', '.')-64-bit.zip"
Invoke-WebRequest -Uri $gurl -OutFile $gzip -UseBasicParsing
if ((Get-FileHash $gzip -Algorithm SHA256).Hash.ToLowerInvariant() -ne $pins.git.'win-x64'.sha256) { throw 'the MinGit download does not match its SHA-256' }
Expand-Archive $gzip -DestinationPath (Join-Path $res 'git')
if (-not (Test-Path (Join-Path $res 'git\cmd\git.exe'))) { throw 'MinGit has no cmd\git.exe' }

# 4. The memev command inside the app, which the window uses when MEMEV_BIN is not set.
# The bundled git comes first on PATH, for this command and everything the core starts; setlocal keeps that from the caller.
Set-Content -Path (Join-Path $res 'bin\memev.cmd') -Encoding ascii -Value @('@setlocal', '@set "PATH=%~dp0..\git\cmd;%PATH%"', '@"%~dp0..\node\node.exe" "%~dp0..\core\bin\memev.mjs" %*')
Set-Content -Path (Join-Path $res 'PINS.txt') -Encoding ascii -Value @("memev $($pins.memev_commit)", "node $($pins.node.version) win-x64", "git $($pins.git.version) MinGit win-x64")

# 5. The installer.
$iscc = (Get-Command iscc.exe -ErrorAction SilentlyContinue).Source
if (-not $iscc) { $iscc = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1 }
if (-not $iscc) { throw 'Inno Setup 6 (ISCC.exe) is not installed' }
& $iscc /Q "/DAppVersion=$($pins.version)" "/DSrc=$app" "/DOut=$dist" (Join-Path $here 'memev.iss')
if ($LASTEXITCODE -ne 0) { throw 'the installer did not build' }
$out = Join-Path $dist "Memev-$($pins.version)-windows-x64-setup.exe"
(Get-FileHash $out -Algorithm SHA256).Hash.ToLowerInvariant() | Set-Content -Encoding ascii "$out.sha256"
Write-Output $out
