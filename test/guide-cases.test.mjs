// SPDX-License-Identifier: Apache-2.0
// The guide's connect, lead and fix decisions, headless: each case runs Memev.Reader.Check --case <name> against its fixture
// core, on any computer with .NET 9, and passes only when the check prints `ok <name>`. The window cases that need the built
// WPF app stay in native/windows/test/self-check.ps1, which CI runs on Windows.
import { test, before } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';

const project = 'native/windows/Memev.Reader.Check';
const CASES = [
  ['T1-c01', 'connect-list'], ['T1-c01', 'connect-onestep'], ['T1-c01', 'connect-selected'], ['T1-c01', 'connect-result'], ['T1-c01', 'connect-unreported'], ['T1-c01', 'configured-unverified'], ['T1-c01', 'all-configured-title'], ['T1-c01', 'already-connected'],
  ['T2-c01', 'order'], ['T2-c01', 'lead-registered'], ['T2-c01', 'lead-retry'],
  ['T3-c01', 'fix-text'], ['T3-c01', 'fix-retry'], ['T3-c01', 'fix-manual-skipped'],
];

let built = null;
before(() => {
  // one build, so each case below only runs it
  built = spawnSync('dotnet', ['build', project, '-c', 'Release', '--nologo', '-v', 'q'], { encoding: 'utf8', timeout: 900000, windowsHide: true });
});

for (const [task, name] of CASES) {
  test(`${task}: ${name}`, { timeout: 300000 }, () => {
    assert.equal(built?.status, 0, `the check project did not build:\n${built?.stdout ?? ''}${built?.stderr ?? ''}`);
    const r = spawnSync('dotnet', ['run', '--project', project, '-c', 'Release', '--no-build', '--', '--case', name], { encoding: 'utf8', timeout: 240000, windowsHide: true });
    const lines = (r.stdout || '').split(/\r?\n/);
    assert.equal(r.status, 0, `case ${name} exited ${r.status}:\n${r.stdout}${r.stderr}`);
    assert.ok(lines.includes(`ok ${name}`), `case ${name} did not print 'ok ${name}':\n${r.stdout}${r.stderr}`);
  });
}
