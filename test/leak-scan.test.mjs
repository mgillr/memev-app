// SPDX-License-Identifier: Apache-2.0
// The leak scan's one allowance in commit messages: GitHub's own merge subject, which names the repository owner.
// A made-up word stands in for a denylisted one, so this file holds no real denylisted word.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

const scan = path.resolve('scripts/leak-scan.mjs');
function repo(messages, extraFile) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'leak-'));
  const git = (...a) => execFileSync('git', ['-c', 'user.name=t', '-c', 'user.email=t@example.invalid', ...a], { cwd: dir });
  git('init', '-q');
  fs.writeFileSync(path.join(dir, 'a.txt'), 'x\n');
  git('add', 'a.txt');
  if (extraFile) { fs.mkdirSync(path.dirname(path.join(dir, extraFile)), { recursive: true }); fs.writeFileSync(path.join(dir, extraFile), 'x\n'); git('add', extraFile); }
  for (const m of messages) git('commit', '-q', '--allow-empty', '-m', m);
  const words = path.join(dir, '..', `${path.basename(dir)}.words`);
  fs.writeFileSync(words, 'zorbquux\n');
  return spawnSync(process.execPath, [scan, '--history'], { encoding: 'utf8', env: { ...process.env, LEAK_ROOT: dir, LEAK_DENYLIST: words } });
}

test('GitHub\'s merge subject naming the owner passes', () => assert.equal(repo(['Merge pull request #3 from zorbquux/native/linux-overview\n\nAdd a window']).status, 0));
test('the owner word in the body of a merge commit is still found', () => assert.equal(repo(['Merge pull request #3 from zorbquux/x\n\nzorbquux was here']).status, 1));
test('the owner word on a first line of another form is still found', () => assert.equal(repo(['Fix the window for zorbquux']).status, 1));
test('a merge subject with more after the branch is still found', () => assert.equal(repo(['Merge pull request #3 from zorbquux/x and zorbquux']).status, 1));
test('a tracked file path of the merge-subject form is still found', () => assert.equal(repo(['Add a note'], 'Merge pull request #3 from zorbquux/note.txt').status, 1));
