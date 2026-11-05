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
test('the licence text may use the word', () => assert.equal(run(tree('LICENSE', 'Grant of Patent License\n')).status, 0));
for (const [name, file, body] of [
  ['importing the engine', 'app/a.py', 'import crdt_merge\n'],
  ['naming the package', 'app/a.swift', 'let p = "memev-merge"\n'],
  ['a vendored engine folder', 'vendor/crdt-merge/x.txt', 'x\n'],
  ['licence logic', 'app/a.cs', 'verifyLicense(l);\n'],
  ['seat logic', 'app/a.c', 'seatPolicy(l);\n'],
  ['a page describing the method', 'docs/how.md', 'Entries live in an ORSet ordered by a clock.\n'],
  ['a page naming the engine', 'docs/how.md', 'Merging is done by crdt-merge.\n'],
  ['patent wording in a page', 'NOTICE', 'This covers a patent of ours.\n'],
]) test(`the fence fails on ${name}`, () => assert.equal(run(tree(file, body)).status, 1));
