# SPDX-License-Identifier: Apache-2.0
# Installs the setup program silently into a temporary folder and home, then: the bundled core inits a notes folder and starts,
# status and doctor answer, the window reads that live core with its own bundled memev, the first launch saves and reads back
# a note, the core stops, and uninstall leaves no process from the install and keeps the notes. It opens a real window for
# about a second, so it runs in CI and is not meant for a person's own screen. No git is on PATH, as on a new PC: the core
# must use the git the setup brought.
param([Parameter(Mandatory = $true)][string]$Setup)
$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$t = Join-Path ([System.IO.Path]::GetTempPath()) ("memev-install-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
$dir = Join-Path $t 'Memev'
New-Item -ItemType Directory -Force -Path "$t\home", "$t\config", "$t\deny" | Out-Null
$env:HOME = "$t\home"; $env:USERPROFILE = "$t\home"; $env:MEMEV_CONFIG_DIR = "$t\config"
$env:MEMEV_NO_SERVICE = '1'; $env:MEMEV_NO_TUNNEL = '1'; $env:MEMEV_NO_BACKGROUND = '1'; $env:MEMEV_NO_DOWNLOAD = '1'
Remove-Item Env:MEMEV_BIN, Env:NODE_OPTIONS, Env:NODE_PATH -ErrorAction SilentlyContinue
# No node, npm or memev from this machine: stand-ins that fail come first on PATH.
foreach ($c in 'node', 'npm', 'npx', 'memev') { Set-Content -Encoding ascii -Path "$t\deny\$c.cmd" -Value "@echo $c from PATH was used 1>&2`r`n@exit /b 97" }
$env:PATH = "$t\deny;" + $env:PATH
# No git either: every PATH folder that holds one is dropped, and a stand-in that fails catches a lookup through cmd.exe.
$env:PATH = ($env:PATH -split ';' | Where-Object { $_ -and -not (Test-Path (Join-Path $_ 'git.exe')) }) -join ';'
Set-Content -Encoding ascii -Path "$t\deny\git.cmd" -Value "@echo git from PATH was used 1>&2`r`n@exit /b 97"
if (Get-Command git.exe -ErrorAction SilentlyContinue) { throw 'a git.exe is still on PATH' }

$p = Start-Process -FilePath $Setup -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/CURRENTUSER', '/NORESTART', "/DIR=$dir" -PassThru -Wait
if ($p.ExitCode -ne 0) { throw "setup exited $($p.ExitCode)" }
$bin = Join-Path $dir 'resources\bin\memev.cmd'
$exe = Join-Path $dir 'memev-desktop.exe'
$git = Join-Path $dir 'resources\git\cmd\git.exe'
foreach ($f in $bin, $exe, $git, (Join-Path $dir 'resources\node\node.exe'), (Join-Path $dir 'resources\core\bin\memev.mjs')) { if (-not (Test-Path $f)) { throw "not installed: $f" } }

function Get-FreePort { $l = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0); $l.Start(); $n = $l.LocalEndpoint.Port; $l.Stop(); return $n }
$port = Get-FreePort
# the installer's own traces: a Start menu entry and an uninstall key for this user
$shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Memev.lnk'
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\computer.memev.Desktop_is1'
if (-not (Test-Path $shortcut)) { throw "no Start menu entry at $shortcut" }
if (-not (Test-Path $key)) { throw 'no uninstall entry for this user' }
$notes = Join-Path $t 'home\notes'
try {
  & cmd.exe /d /c "`"$bin`" init --root `"$notes`" --local-only --no-connect --no-service --port $port"
  if ($LASTEXITCODE -ne 0) { throw "init exited $LASTEXITCODE" }
  # the repository is the notes folder itself: git is pointed at its .git, so it cannot walk up into a parent folder's
  if (-not (Test-Path (Join-Path $notes '.git'))) { throw 'init made no git repository in the notes folder' }
  & $git --git-dir (Join-Path $notes '.git') rev-parse --git-dir 2>&1 | Out-Null
  if ($LASTEXITCODE -ne 0) { throw 'the bundled git cannot read the notes repository' }
  $status = (& cmd.exe /d /c "`"$bin`" status --json") -join "`n" | ConvertFrom-Json
  if (-not $status.version) { throw 'status gave no version' }
  Write-Output "core $($status.version)"
  $doctor = (& cmd.exe /d /c "`"$bin`" doctor --json") -join "`n"
  if ($doctor -notmatch '"checks"|^\s*\[') { throw "doctor gave no checks: $doctor" }

  # The window, with no MEMEV_BIN, finds the core it was installed with and reads it live.
  $out = [System.IO.Path]::GetTempFileName()
  $env:MEMEV_HEALTH_URL = "http://127.0.0.1:$port/healthz"; $env:MEMEV_THEME = 'dark'; $env:MEMEV_GUIDE = 'off'; $env:MEMEV_SELF_CHECK = '1'
  $w = Start-Process -FilePath $exe -RedirectStandardOutput $out -PassThru -NoNewWindow
  if (-not $w.WaitForExit(180000)) { $w.Kill(); throw 'the window did not exit' }
  $wcode = $w.ExitCode
  Remove-Item Env:MEMEV_HEALTH_URL, Env:MEMEV_THEME, Env:MEMEV_GUIDE, Env:MEMEV_SELF_CHECK -ErrorAction SilentlyContinue
  $lines = @(Get-Content $out); $lines | ForEach-Object { Write-Output $_ }
  if (-not ($lines | Where-Object { $_ -cmatch '^overview=Memev [0-9.]+ is running' })) { throw 'the window did not read the running core' }
  if ($wcode -ne 0) { throw "the window's self-check exited $wcode" }
  # Every screen renders against the live core, the amber rules hold, six first reads, and the three new screens read it.
  foreach ($want in 'match=1', 'secret=absent', 'leak=absent', 'reads=6', 'pips=14', 'amber_max=1', 'amber_ring=1', 'sheets_ok=1',
    'connect_read=ok', 'agents_read=ok', 'screen=bots|Bots and agents|Team,Connect,Background agents,Access', 'verdict=pass') {
    if ($lines -cnotcontains $want) { throw "missing: $want" }
  }

  # Start Memev from the window: the core is stopped, the sheet's main button runs start, and the window reads it running.
  & cmd.exe /d /c "`"$bin`" stop" | Out-Null
  $out = [System.IO.Path]::GetTempFileName()
  $env:MEMEV_HEALTH_URL = "http://127.0.0.1:$port/healthz"; $env:MEMEV_THEME = 'light'; $env:MEMEV_GUIDE = 'off'; $env:MEMEV_SELF_CHECK = '1'; $env:MEMEV_SELF_CHECK_START = '1'
  $w = Start-Process -FilePath $exe -RedirectStandardOutput $out -PassThru -NoNewWindow
  if (-not $w.WaitForExit(180000)) { $w.Kill(); throw 'the window did not exit after Start Memev' }
  Remove-Item Env:MEMEV_HEALTH_URL, Env:MEMEV_THEME, Env:MEMEV_GUIDE, Env:MEMEV_SELF_CHECK, Env:MEMEV_SELF_CHECK_START -ErrorAction SilentlyContinue
  $lines = @(Get-Content $out); $lines | ForEach-Object { Write-Output $_ }
  foreach ($want in 'start_sheet=Start Memev?|Start Memev', 'start_before=stopped', 'start_rc=0', 'start_after=running', 'start_banner=hidden') {
    if ($lines -cnotcontains $want) { throw "missing after Start Memev: $want" }
  }
} finally {
  & cmd.exe /d /c "`"$bin`" stop" 2>$null | Out-Null
}

# The first launch through the installed core, headless: a fresh notes folder, a note saved and read back.
New-Item -ItemType Directory -Force -Path "$t\proof-home", "$t\proof-config" | Out-Null
$env:HOME = "$t\proof-home"; $env:USERPROFILE = "$t\proof-home"; $env:MEMEV_CONFIG_DIR = "$t\proof-config"
$env:MEMEV_GUIDE_PORT = [string](Get-FreePort)
& dotnet run --project (Join-Path $repo 'native\windows\Memev.Reader.Check') -c Release -- proof $bin (Join-Path $t 'proof-notes')
$rc = $LASTEXITCODE
& cmd.exe /d /c "`"$bin`" stop" 2>$null | Out-Null
if ($rc -ne 0) { throw "the first-launch proof exited $rc" }
# the bundled git reads the notes: they are a repository with a commit, and no remote was recorded
$pgit = Join-Path $t 'proof-notes\.git'
if (-not (Test-Path $pgit)) { throw 'the first launch made no git repository in its notes folder' }
$head = (& $git --git-dir $pgit rev-parse --verify HEAD 2>&1) -join ' '
if ($LASTEXITCODE -ne 0) { throw "the first launch made no commit with the bundled git: $head" }
Write-Output "notes commit $head"
& $git --git-dir $pgit remote get-url origin 2>$null | Out-Null
if ($LASTEXITCODE -eq 0) { throw 'a remote was recorded without a tick' }

# Uninstall: nothing from the install keeps running, and the notes stay.
Start-Sleep -Seconds 1
$un = Get-ChildItem $dir -Filter 'unins*.exe' | Select-Object -First 1
if (-not $un) { throw 'no uninstaller' }
$u = Start-Process -FilePath $un.FullName -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -PassThru -Wait
if ($u.ExitCode -ne 0) { throw "uninstall exited $($u.ExitCode)" }
Start-Sleep -Seconds 3
$left = @(Get-Process | Where-Object { $_.Path -and $_.Path.StartsWith($dir, [System.StringComparison]::OrdinalIgnoreCase) })
if ($left.Count -gt 0) { $left | Format-Table Id, Path | Out-String | Write-Output; throw 'a process from the install is still running after uninstall' }
if (Test-Path $exe) { throw 'the window is still installed after uninstall' }
if (Test-Path (Join-Path $dir 'resources\git')) { throw 'the bundled git is still there after uninstall' }
if (Test-Path $shortcut) { throw 'the Start menu entry is still there after uninstall' }
if (Test-Path $key) { throw 'the uninstall entry is still there after uninstall' }
if (-not (Test-Path $notes)) { throw 'the notes folder was not kept' }
Write-Output 'install check passed'
# the last native command here is a check meant to fail (no remote), and its code would otherwise become the script's
exit 0
