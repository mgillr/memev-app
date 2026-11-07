# SPDX-License-Identifier: Apache-2.0
# The first launch against a real core checkout on Windows, as native/linux/test/real-core.sh. The fixture is not used.
# The core runs through node from a .cmd, as npm's memev.cmd runs it, with its own home, config and notes folder.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$core = if ($env:MEMEV_CORE) { $env:MEMEV_CORE } else { Join-Path $env:RUNNER_TEMP 'memev-core' }
$mjs = Join-Path $core 'bin\memev.mjs'
if (-not (Test-Path $mjs)) { throw "core checkout is missing at $core" }
$work = Join-Path ([System.IO.Path]::GetTempPath()) ("memev-proof-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force -Path "$work\home", "$work\config" | Out-Null
$bin = Join-Path $work 'memev.cmd'
Set-Content -Path $bin -Value "@node `"$mjs`" %*" -Encoding ascii
$env:HOME = "$work\home"; $env:USERPROFILE = "$work\home"; $env:MEMEV_CONFIG_DIR = "$work\config"
$env:MEMEV_NO_SERVICE = '1'; $env:MEMEV_NO_TUNNEL = '1'; $env:MEMEV_NO_BACKGROUND = '1'; $env:MEMEV_NO_DOWNLOAD = '1'
if (-not $env:MEMEV_GUIDE_PORT) { $env:MEMEV_GUIDE_PORT = '18799' }
$vault = Join-Path $work 'vault'
try {
  & dotnet run --project (Join-Path $root 'Memev.Reader.Check') -c Release -- proof $bin $vault
  if ($LASTEXITCODE -ne 0) { throw "the proof exited $LASTEXITCODE" }
  & git -C $vault remote get-url origin 2>$null | Out-Null
  if ($LASTEXITCODE -eq 0) { throw 'a remote was recorded without a confirm' }
} finally {
  & cmd.exe /d /c "`"$bin`" stop" 2>$null | Out-Null
}
Write-Output 'real-core proof passed'
