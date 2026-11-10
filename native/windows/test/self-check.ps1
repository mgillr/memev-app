# SPDX-License-Identifier: Apache-2.0
# Runs the Windows window and checks the rev2 shell, with the same fixture answers and the same values as the Linux and macOS
# self-checks. It opens a real window for about a second, so it runs in CI and is not meant for a person's own screen.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$bin = Join-Path $root 'Memev.Desktop\bin\Release\net9.0-windows'
$exe = Join-Path $bin 'memev-desktop.exe'
$fix = Join-Path $root 'test\fixtures\memev.cmd'
if (-not (Test-Path $exe)) { throw "not built: $exe" }

# A window program writes to stdout only when it is redirected, so every run goes to a file.
function Invoke-Window([hashtable]$vars, [string[]]$arguments) {
  $out = [System.IO.Path]::GetTempFileName()
  $saved = @{}
  foreach ($k in $vars.Keys) { $saved[$k] = [Environment]::GetEnvironmentVariable($k); [Environment]::SetEnvironmentVariable($k, $vars[$k]) }
  try {
    $start = @{ FilePath = $exe; RedirectStandardOutput = $out; PassThru = $true; NoNewWindow = $true }
    if ($arguments) { $start.ArgumentList = $arguments }
    $p = Start-Process @start
    if (-not $p.WaitForExit(180000)) { $p.Kill(); throw "the window did not exit within 180 s" }
    return @{ Code = $p.ExitCode; Lines = @(Get-Content $out) }
  } finally {
    # PowerShell passes $null to a .NET string as an empty string, which does not remove the variable, so removal is explicit
    foreach ($k in $saved.Keys) { if ($null -eq $saved[$k]) { Remove-Item "Env:$k" -ErrorAction SilentlyContinue } else { Set-Item "Env:$k" $saved[$k] } }
    Remove-Item $out -ErrorAction SilentlyContinue
  }
}

$v = Invoke-Window @{} @('--version')
if ($v.Code -ne 0 -or ($v.Lines -notcontains 'memev-desktop 0.0.1-slice1')) { throw "version: rc $($v.Code), '$($v.Lines -join ' | ')'" }

# The design's fonts are built into the program; with MEMEV_FONTS=off the system fonts stand in.
$f = Invoke-Window @{} @('--fonts')
$f.Lines | ForEach-Object { Write-Output $_ }
foreach ($w in 'font_sans=Space Grotesk', 'font_mono=IBM Plex Mono') { if ($f.Lines -cnotcontains $w) { throw "fonts missing: $w" } }
$f = Invoke-Window @{ MEMEV_FONTS = 'off' } @('--fonts')
if ($f.Lines -cnotcontains 'font_sans=Segoe UI') { throw "the fallback is not Segoe UI: $($f.Lines -join ' | ')" }

foreach ($theme in 'dark', 'light') {
  $r = Invoke-Window @{ MEMEV_BIN = $fix; MEMEV_HEALTH_URL = 'off'; MEMEV_THEME = $theme; MEMEV_GUIDE = 'off'; MEMEV_SELF_CHECK = '1' } @()
  $r.Lines | ForEach-Object { Write-Output $_ }
  $want = @(
    'overview=Memev 0.1.0 is running for demo. 3 notes are in the index. 1 check to look at.',
    'health=1 check to look at.', 'star=Ship the window', 'attention=1', 'checks=2', 'primary=1',
    'minw=1024', 'minh=700', 'sidebar=240', 'accel=1', 'match=1', 'secret=absent', 'leak=absent', 'visible=1', "theme=$theme",
    'pips=14', 'pip=Backup:needs', 'pip=Running:fine', 'pip=Shared:unread', 'pip=Bots:unread',
    'screen=overview|Executive overview|', 'screen=operations|Operations|Workspace,Messages', 'screen=projects|Projects|',
    'screen=bots|Bots and agents|Team,Connect,Background agents,Access', 'screen=chats|Chats|', 'screen=memory|Memory|Search,Review,Browse,History,Health',
    'screen=checked|Checked work|', 'screen=settings|Settings|General,Plan and computers,Storage and backup,Advanced,Help and support',
    'screen=health|Health|', 'amber_max=1', 'amber_ring=1', 'confirm_amber=0', 'confirm_main=red', 'sheet_amber=1', 'sheet_ring=1',
    'start_sheet=Start Memev?|Start Memev', 'reads=6', 'font_sans=Space Grotesk', 'font_mono=IBM Plex Mono',
    'connect_read=ok', 'connect_apps=5', 'connect_ticked=2', 'connect_connected=1', 'connect_manual=1', 'connect_notfound=2',
    'agents_read=ok', 'agents_rows=2', 'chats_read=ok', 'chats_on=1', 'chat=telegram|set up, not paired', 'chat=whatsapp|off', 'qr_modules=29', 'sheet=telegram_setup|Set up Telegram|Set up|amber=1|main=amber', 'sheet=whatsapp_on|Turn on WhatsApp?|Turn on|amber=1|main=amber', 'sheet=discord_setup|Set up the Discord route|Set up|amber=1|main=amber', 'sheet=chat_off|Turn off Telegram?|Turn off|amber=0|main=red', 'import_read=ok', 'import_sources=3', 'import_marked=1', 'sheet=import_confirm|Import Claude Code?|Import|amber=1|main=amber', 'confirms=14', 'sheets_ok=1', 'verdict=pass',
    'sheet=connect|Connect 2 AI apps?|Connect|amber=1|main=amber', 'sheet=disconnect|Disconnect Claude Code?|Disconnect|amber=0|main=red',
    'sheet=agents|Add background agents|Start|amber=1|main=amber', 'sheet=agents_stop|Stop the background agents for claude-code/2?|Stop|amber=0|main=red',
    'sheet=import|Import||amber=0|main=none', 'sheet=import_undo|Undo import import-1?|Undo import|amber=0|main=red')
  foreach ($w in $want) { if ($r.Lines -cnotcontains $w) { throw "missing ($theme): $w" } }
  if (@($r.Lines | Where-Object { $_ -like 'pip=*' }).Count -ne 14) { throw "the health button does not carry 14 pips ($theme)" }
  $focus = ($r.Lines | Where-Object { $_ -like 'focusable=*' } | Select-Object -First 1) -replace '^focusable=', ''
  if (-not $focus -or [int]$focus -lt 9) { throw "too few focusable controls ($theme): '$focus'" }
  if ($r.Code -ne 0) { throw "the self-check exited $($r.Code) ($theme)" }
}

# The window with no fonts of its own still passes, on the system fonts.
$r = Invoke-Window @{ MEMEV_BIN = $fix; MEMEV_HEALTH_URL = 'off'; MEMEV_THEME = 'light'; MEMEV_GUIDE = 'off'; MEMEV_SELF_CHECK = '1'; MEMEV_FONTS = 'off' } @()
if ($r.Code -ne 0 -or $r.Lines -cnotcontains 'font_sans=Segoe UI') { $r.Lines | ForEach-Object { Write-Output $_ }; throw "the window on system fonts: rc $($r.Code)" }

# The first launch: it opens on its first question, with Continue as the one filled button.
$g = Invoke-Window @{ MEMEV_BIN = $fix; MEMEV_HEALTH_URL = 'off'; MEMEV_THEME = 'dark'; MEMEV_GUIDE = '1'; MEMEV_SELF_CHECK = '1' } @()
$g.Lines | ForEach-Object { Write-Output $_ }
foreach ($w in 'guide=Is this your first computer?', 'primary_label=Continue', 'primary=1', 'reads=6') { if ($g.Lines -cnotcontains $w) { throw "missing (guide): $w" } }
if ($g.Code -ne 0) { throw "the guide self-check exited $($g.Code)" }

# The three questions after the note check, in order, each with Skip and one filled button.
$steps = @{ connect = @('guide=Connect your AI apps', 'primary_label=Connect 2 apps', 'guide_rows=5'); import = @('guide=Bring in your existing AI chat history?', 'primary_label=Import 1 source', 'guide_rows=3'); lead = @('guide=Which app should lead?', 'primary_label=Finish') }
foreach ($s in 'connect', 'import', 'lead') {
  $g = Invoke-Window @{ MEMEV_BIN = $fix; MEMEV_HEALTH_URL = 'off'; MEMEV_THEME = 'light'; MEMEV_GUIDE = '1'; MEMEV_SELF_CHECK = '1'; MEMEV_SELF_CHECK_GUIDE_STEP = $s } @()
  $g.Lines | ForEach-Object { Write-Output $_ }
  foreach ($w in $steps[$s] + @('primary=1', 'skip=1', 'leak=absent')) { if ($g.Lines -cnotcontains $w) { throw "missing (guide $s): $w" } }
  if ($g.Code -ne 0) { throw "the guide step $s exited $($g.Code)" }
}

# The window shows before the core answers: every core command here waits about 2 s, and the window must show within 3 s.
$slowDir = Join-Path ([System.IO.Path]::GetTempPath()) ("memev-slow-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force -Path $slowDir | Out-Null
$slow = Join-Path $slowDir 'memev.cmd'
Set-Content -Path $slow -Encoding ascii -Value "@ping -n 3 127.0.0.1 >nul`r`n@call `"$fix`" %*"
$r = Invoke-Window @{ MEMEV_BIN = $slow; MEMEV_HEALTH_URL = 'off'; MEMEV_THEME = 'dark'; MEMEV_GUIDE = 'off'; MEMEV_SELF_CHECK = '1' } @()
$ms = ($r.Lines | Where-Object { $_ -like 'shown_ms=*' } | Select-Object -First 1) -replace '^shown_ms=', ''
Write-Output "slow core: shown_ms=$ms rc=$($r.Code)"
if (-not $ms -or [int]$ms -gt 3000) { throw "the window took $ms ms to show with a slow core" }
if ($r.Lines -cnotcontains 'reads=6' -or $r.Code -ne 0) { $r.Lines | ForEach-Object { Write-Output $_ }; throw 'the window with a slow core did not finish its reads' }
Remove-Item -Recurse -Force $slowDir -ErrorAction SilentlyContinue

# Built at run time so this file does not itself contain a forbidden name.
$needles = @(('mem' + 'ev-' + 'mer' + 'ge'), ('cr' + 'dt-' + 'mer' + 'ge'))
foreach ($f in Get-ChildItem $bin -Include '*.exe', '*.dll' -Recurse) {
  # .NET keeps string literals as UTF-16 and names as UTF-8, so the bytes are read both ways
  $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
  $texts = @([System.Text.Encoding]::Latin1.GetString($bytes), [System.Text.Encoding]::Unicode.GetString($bytes), [System.Text.Encoding]::Unicode.GetString($bytes, 1, $bytes.Length - 1))
  foreach ($t in $texts) { foreach ($n in $needles) { if ($t.Contains($n)) { throw "$($f.Name) names a forbidden engine token" } } }
}

# A live window with health checks off must hold no TCP connection other than loopback. The window must still be running
# when the connections are read, or the check would pass on a window that had already gone.
Remove-Item Env:MEMEV_SELF_CHECK -ErrorAction SilentlyContinue
$env:MEMEV_BIN = $fix; $env:MEMEV_HEALTH_URL = 'off'; $env:MEMEV_THEME = 'dark'; $env:MEMEV_POLL_MS = '500'; $env:MEMEV_GUIDE = 'off'
$p = Start-Process -FilePath $exe -PassThru
try {
  Start-Sleep -Seconds 3
  if ($p.HasExited) { throw "the window exited ($($p.ExitCode)) before its connections were read" }
  $conns = @(Get-NetTCPConnection -OwningProcess $p.Id -ErrorAction SilentlyContinue | Where-Object { $_.State -in 'Established', 'SynSent' -and $_.RemoteAddress -notin '127.0.0.1', '::1' })
  $p.Refresh()
  if ($p.HasExited) { throw "the window exited ($($p.ExitCode)) while its connections were read" }
  if ($conns.Count -gt 0) { $conns | Format-Table -AutoSize | Out-String | Write-Output; throw 'non-loopback connection' }
} finally {
  if (-not $p.HasExited) { $p.Kill(); $p.WaitForExit() }
  Remove-Item Env:MEMEV_BIN, Env:MEMEV_HEALTH_URL, Env:MEMEV_THEME, Env:MEMEV_POLL_MS, Env:MEMEV_GUIDE -ErrorAction SilentlyContinue
}

Write-Output 'self-check passed'
