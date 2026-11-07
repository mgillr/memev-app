# SPDX-License-Identifier: Apache-2.0
# Runs the Windows window and checks the first slice, with the same fixture answers and the same values as the Linux and macOS
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
    if (-not $p.WaitForExit(60000)) { $p.Kill(); throw "the window did not exit within 60 s" }
    return @{ Code = $p.ExitCode; Lines = @(Get-Content $out) }
  } finally {
    # PowerShell passes $null to a .NET string as an empty string, which does not remove the variable, so removal is explicit
    foreach ($k in $saved.Keys) { if ($null -eq $saved[$k]) { Remove-Item "Env:$k" -ErrorAction SilentlyContinue } else { Set-Item "Env:$k" $saved[$k] } }
    Remove-Item $out -ErrorAction SilentlyContinue
  }
}

$v = Invoke-Window @{} @('--version')
if ($v.Code -ne 0 -or ($v.Lines -notcontains 'memev-desktop 0.0.1-slice1')) { throw "version: rc $($v.Code), '$($v.Lines -join ' | ')'" }

foreach ($theme in 'dark', 'light') {
  $r = Invoke-Window @{ MEMEV_BIN = $fix; MEMEV_HEALTH_URL = 'off'; MEMEV_THEME = $theme; MEMEV_SELF_CHECK = '1' } @()
  $r.Lines | ForEach-Object { Write-Output $_ }
  $want = @(
    'overview=Memev 0.1.0 is running for demo. 3 notes are in the index. 1 check to look at.',
    'health=1 check to look at.', 'star=Ship the window', 'attention=1', 'checks=2', 'primary=1',
    'minw=1024', 'minh=700', 'sidebar=240', 'accel=1', 'match=1', 'secret=absent', 'visible=1', "theme=$theme")
  foreach ($w in $want) { if ($r.Lines -cnotcontains $w) { throw "missing ($theme): $w" } }
  $focus = ($r.Lines | Where-Object { $_ -like 'focusable=*' } | Select-Object -First 1) -replace '^focusable=', ''
  if (-not $focus -or [int]$focus -lt 9) { throw "too few focusable controls ($theme): '$focus'" }
  if ($r.Code -ne 0) { throw "the self-check exited $($r.Code) ($theme)" }
}

# Built at run time so this file does not itself contain a forbidden name.
$needles = @(('mem' + 'ev-' + 'mer' + 'ge'), ('cr' + 'dt-' + 'mer' + 'ge'))
foreach ($f in Get-ChildItem $bin -Include '*.exe', '*.dll' -Recurse) {
  $text = [System.Text.Encoding]::Latin1.GetString([System.IO.File]::ReadAllBytes($f.FullName))
  foreach ($n in $needles) { if ($text.Contains($n)) { throw "$($f.Name) names a forbidden engine token" } }
}

# A live window with health checks off must hold no TCP connection other than loopback. The window must still be running
# when the connections are read, or the check would pass on a window that had already gone.
Remove-Item Env:MEMEV_SELF_CHECK -ErrorAction SilentlyContinue
$env:MEMEV_BIN = $fix; $env:MEMEV_HEALTH_URL = 'off'; $env:MEMEV_THEME = 'dark'; $env:MEMEV_POLL_MS = '500'
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
  Remove-Item Env:MEMEV_BIN, Env:MEMEV_HEALTH_URL, Env:MEMEV_THEME, Env:MEMEV_POLL_MS -ErrorAction SilentlyContinue
}

Write-Output 'self-check passed'
