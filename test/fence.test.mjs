// SPDX-License-Identifier: Apache-2.0
// The fence passes on this repository and fails on each way it could be broken, on a scratch tree.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

const fence = path.resolve('scripts/fence.mjs');
const run = (dir) => spawnSync(process.execPath, [fence, dir], { encoding: 'utf8' });
function tree(file, body) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'fence-'));
  execFileSync('git', ['init', '-q'], { cwd: dir });
  fs.mkdirSync(path.dirname(path.join(dir, file)), { recursive: true });
  fs.writeFileSync(path.join(dir, file), body);
  return dir;
}

test('this repository is inside the fence', () => assert.equal(run('.').status, 0));
test('a clean file passes', () => assert.equal(run(tree('app/main.swift', 'print("memev plan --json")\n')).status, 0));
test('ordinary words pass', () => assert.equal(run(tree('docs/a.md', 'Do this or set that. Colour e4e4e4 and limits on use.\n')).status, 0));
test('the licence text may use the word', () => assert.equal(run(tree('LICENSE', 'Grant of Patent License\n')).status, 0));
for (const [name, file, body, why] of [
  ['importing the engine', 'app/a.py', 'import crdt_merge\n', 'names the engine'],
  ['naming the package', 'app/a.swift', 'let p = "memev-merge"\n', 'names the engine'],
  ['a vendored engine folder', 'vendor/crdt-merge/x.txt', 'x\n', 'an engine path'],
  ['a packaged engine wheel', 'vendor/crdt_merge-0.9.1.whl', 'x\n', 'an engine path'],
  ['a packaged engine archive', 'vendor/memev-merge-1.0.tar.gz', 'x\n', 'an engine path'],
  ['an engine library', 'Frameworks/libcrdtmerge.dylib', 'x\n', 'an engine path'],
  ['the engine named in words', 'docs/how.md', 'Merging is done by CRDT merge.\n', 'names the engine'],
  ['another engine package', 'app/a.rs', 'use acfa_protocol;\n', 'names the engine'],
  ['the trust layer by name', 'docs/how.md', 'Checked by E4.\n', 'names the engine'],
  ['camelCase licence logic', 'app/a.js', 'verifyLicense(l);\n', 'licence or seat logic'],
  ['C# licence logic', 'app/a.cs', 'VerifyLicense(l);\n', 'licence or seat logic'],
  ['Rust licence logic', 'app/a.rs', 'verify_license(l);\n', 'licence or seat logic'],
  ['Swift seat logic', 'app/a.swift', 'SeatPolicy()\n', 'licence or seat logic'],
  ['Rust limits', 'app/a.rs', 'limits_for(l);\n', 'licence or seat logic'],
  ['a page describing the method', 'docs/how.md', 'Entries live in an ORSet ordered by a clock.\n', "describes the engine's method"],
  ['method words with a space', 'docs/how.md', 'It uses a vector clock.\n', "describes the engine's method"],
  ['observed-remove', 'docs/how.md', 'An observed-remove set.\n', "describes the engine's method"],
  ['proof-carrying', 'docs/how.md', 'Each is proof-carrying.\n', "describes the engine's method"],
  ['Merkle', 'docs/how.md', 'A Merkle tree of entries.\n', "describes the engine's method"],
  ['patent wording in a page', 'NOTICE', 'This covers a patent of ours.\n', 'patent or filing wording'],
]) test(`the fence fails on ${name}`, () => { const r = run(tree(file, body)); assert.equal(r.status, 1); assert.match(r.stderr, new RegExp(why)); });

// The leak scan must be able to fail: a scan that exits 0 and prints nothing would pass CI.
const leak = (dir) => spawnSync(process.execPath, [path.resolve('scripts/leak-scan.mjs')], { encoding: 'utf8', env: { ...process.env, LEAK_ROOT: dir } });
test('the leak scan passes a clean tree and says so', () => { const r = leak(tree('docs/a.md', 'plain text\n')); assert.equal(r.status, 0); assert.match(r.stdout, /leak scan: clean \(1 files/); });
test('the leak scan fails on a private key', () => { const r = leak(tree('docs/a.md', `-----BEGIN ${'PRIVATE'} KEY-----\n`)); assert.equal(r.status, 1); assert.match(r.stderr, /private key/); });
test('the leak scan fails on a personal e-mail address', () => { const r = leak(tree('docs/a.md', `mail someone${'@'}company-name.io\n`)); assert.equal(r.status, 1); assert.match(r.stderr, /e-mail address/); });
test('the leak scan fails on a denylisted word by its hash', () => { const words = path.join(fs.mkdtempSync(path.join(os.tmpdir(), 'w-')), 'w.txt'); fs.writeFileSync(words, 'zebrafishmarker\n'); const r = spawnSync(process.execPath, [path.resolve('scripts/leak-scan.mjs')], { encoding: 'utf8', env: { ...process.env, LEAK_ROOT: tree('docs/a.md', 'a zebrafishmarker here\n'), LEAK_DENYLIST: words } }); assert.equal(r.status, 1); assert.match(r.stderr, /denylisted word/); });
test('the leak scan fails on both spellings of each filing reference', () => {
  for (const [a, b] of [['2607', '132'], ['2608', '127']]) for (const text of [`${a}${b}.4`, `GB${a}${b}.3`]) { const r = leak(tree('docs/a.md', `see ${text}\n`)); assert.equal(r.status, 1, text); assert.match(r.stderr, /denylisted word/); }
});
