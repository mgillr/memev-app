// Isolated install, status, update, and uninstall on this Linux machine.
// A failing fake node, npm, or git is first on PATH. Success means the bundled copies were used.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync, spawnSync } from 'node:child_process';
import fs from 'node:fs';
import net from 'node:net';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const proofPath = path.join(here, 'PROOF.md');

function freePort() {
  return new Promise((resolve, reject) => {
    const server = net.createServer();
    server.once('error', reject);
    server.listen(0, '127.0.0.1', () => {
      const port = server.address().port;
      server.close(() => resolve(port));
    });
  });
}

function build() {
  const r = spawnSync(process.execPath, ['build.mjs'], { cwd: here, encoding: 'utf8', maxBuffer: 8 * 1024 * 1024 });
  if (r.status !== 0) {
    throw new Error(`build failed\n${r.stdout}\n${r.stderr}`);
  }
  const lines = r.stdout.trim().split('\n');
  return { artifact: lines[lines.length - 2], sha256: lines[lines.length - 1] };
}

function fakeTools(dir, log) {
  fs.mkdirSync(dir, { recursive: true });
  for (const name of ['node', 'npm', 'npx', 'git', 'curl', 'wget', 'sudo']) {
    fs.writeFileSync(path.join(dir, name), `#!/bin/sh\necho FAKE_${name} >> "$MEMEV_FAKE_LOG"\nexit 99\n`);
    fs.chmodSync(path.join(dir, name), 0o755);
  }
  fs.writeFileSync(log, '');
}

function childEnv(home, extra = {}) {
  return {
    HOME: home,
    PATH: extra.PATH,
    TMPDIR: os.tmpdir(),
    SHELL: '/bin/bash',
    LANG: 'C',
    LC_ALL: 'C',
    MEMEV_CONFIG_DIR: path.join(home, '.config', 'memev'),
    MEMEV_FAKE_LOG: extra.MEMEV_FAKE_LOG,
  };
}

test('linux cli artifact installs, answers, updates, and uninstalls without system node, npm, or git', { timeout: 600000 }, async () => {
  const first = build();
  const second = build();
  assert.equal(first.sha256, second.sha256, 'two builds produced different checksums');
  assert.equal(fs.readFileSync(first.artifact + '.sha256', 'utf8').split(/\s+/)[0], first.sha256);

  const tree = fs.mkdtempSync(path.join(os.tmpdir(), 'memev-cli-tree-'));
  execFileSync('tar', ['-xzf', first.artifact, '-C', tree]);
  const top = path.join(tree, 'memev-cli-0.1.0-linux-x64');
  const manifest = JSON.parse(fs.readFileSync(path.join(top, 'MANIFEST.json'), 'utf8'));
  assert.equal(manifest.unsigned, true);
  assert.equal(manifest.signed, false);
  assert.equal(manifest.npm_published, false);
  assert.equal(manifest.github_release_published, false);
  assert.equal(manifest.memev_commit, '7544bb3e64e8fc166dc9fbc52c38f6caecd604e3');
  assert.equal(manifest.platform, 'linux-x64');
  assert.equal(manifest.node.version, '22.23.4');
  assert.ok(manifest.not_built.includes('macos-dmg'));
  assert.ok(manifest.not_built.includes('windows-exe'));
  assert.ok(manifest.not_built.includes('win-arm64'));
  execFileSync('dash', ['-n', path.join(top, 'install')]);
  execFileSync('sha256sum', ['-c', 'checksums.txt'], { cwd: path.join(top, 'payload') });

  const badHome = fs.mkdtempSync(path.join(os.tmpdir(), 'memev-cli-bad-'));
  const badTree = fs.mkdtempSync(path.join(os.tmpdir(), 'memev-cli-badtree-'));
  execFileSync('tar', ['-xzf', first.artifact, '-C', badTree]);
  const badTgz = path.join(badTree, 'memev-cli-0.1.0-linux-x64', 'payload', 'memev.tgz');
  const flipped = fs.readFileSync(badTgz);
  flipped[flipped.length - 1] ^= 0xff;
  fs.writeFileSync(badTgz, flipped);
  const badLog = path.join(badHome, 'fake.log');
  fakeTools(path.join(badHome, 'fake'), badLog);
  const bad = spawnSync('dash', [path.join(badTree, 'memev-cli-0.1.0-linux-x64', 'install'), '--root', path.join(badHome, 'notes')], {
    env: childEnv(badHome, { PATH: `${path.join(badHome, 'fake')}:/usr/bin:/bin`, MEMEV_FAKE_LOG: badLog }),
    encoding: 'utf8',
  });
  assert.notEqual(bad.status, 0, bad.stdout + bad.stderr);
  assert.match(`${bad.stdout}${bad.stderr}`, /checksum/);
  assert.equal(fs.existsSync(path.join(badHome, '.config', 'memev', 'cli')), false);
  assert.equal(fs.existsSync(path.join(badHome, 'notes')), false);

  const home = fs.mkdtempSync(path.join(os.tmpdir(), 'memev-cli-home-'));
  const fakeLog = path.join(home, 'fake.log');
  const fake = path.join(home, 'fake');
  fakeTools(fake, fakeLog);
  const env = childEnv(home, { PATH: `${fake}:/usr/bin:/bin`, MEMEV_FAKE_LOG: fakeLog });
  const notes = path.join(home, 'notes');
  const port = await freePort();
  const installed = spawnSync('dash', [path.join(top, 'install'), '--root', notes, '--port', String(port)], {
    env,
    encoding: 'utf8',
    timeout: 180000,
  });
  if (installed.status !== 0) {
    throw new Error(`install failed (${installed.status})\n${installed.stdout}\n${installed.stderr}`);
  }
  assert.equal(fs.readFileSync(fakeLog, 'utf8'), '', 'a system node, npm, or git stand-in was called');
  assert.match(installed.stdout, /one line/);
  assert.match(fs.readFileSync(path.join(home, '.bashrc'), 'utf8'), /# added by memev/);

  const cfg = env.MEMEV_CONFIG_DIR;
  const info = JSON.parse(fs.readFileSync(path.join(notes, '.memev', 'daemon.json'), 'utf8'));
  const exe = fs.readlinkSync(`/proc/${info.pid}/exe`);
  assert.equal(exe, path.join(cfg, 'cli/payload/node/bin/node'));

  const status = spawnSync(path.join(cfg, 'bin/memev-cli'), ['status'], { env, encoding: 'utf8', timeout: 60000 });
  if (status.status !== 0) throw new Error(`status failed\n${status.stdout}\n${status.stderr}`);
  assert.match(status.stdout, /Health: ok/);
  assert.match(status.stdout, /"ok":\s*true/);

  const phrase = 'The office kettle is blue and that colour was chosen on purpose.';
  const remembered = spawnSync(path.join(cfg, 'bin/memev'), ['remember', phrase, '--kind', 'fact', '--force'], { env, encoding: 'utf8', timeout: 60000 });
  if (remembered.status !== 0 || !/"saved":\s*true/.test(remembered.stdout)) {
    throw new Error(`remember failed\n${remembered.stdout}\n${remembered.stderr}`);
  }
  const noteText = execFileSync('grep', ['-R', '-l', '--', phrase, notes], { encoding: 'utf8' }).trim().split('\n')[0];
  assert.ok(noteText);

  const again = spawnSync('dash', [path.join(top, 'install'), '--root', notes, '--port', String(port)], { env, encoding: 'utf8', timeout: 180000 });
  if (again.status !== 0) throw new Error(`update failed\n${again.stdout}\n${again.stderr}`);
  assert.match(again.stdout, /Your notes stay where they are/);
  assert.equal(fs.readFileSync(fakeLog, 'utf8'), '');
  const recalled = spawnSync(path.join(cfg, 'bin/memev'), ['recall', 'kettle', '--json'], { env, encoding: 'utf8', timeout: 60000 });
  if (recalled.status !== 0) throw new Error(`recall failed\n${recalled.stdout}\n${recalled.stderr}`);
  assert.match(recalled.stdout, /office kettle is blue/);

  const removed = spawnSync(path.join(cfg, 'bin/memev-cli'), ['uninstall'], { env, encoding: 'utf8', timeout: 60000 });
  if (removed.status !== 0) throw new Error(`uninstall failed\n${removed.stdout}\n${removed.stderr}`);
  assert.match(removed.stdout, /notes are still/);
  assert.equal(fs.existsSync(noteText), true);
  assert.equal(fs.existsSync(path.join(cfg, 'bin')), false);
  assert.equal(fs.existsSync(path.join(cfg, 'cli')), false);
  assert.equal(fs.existsSync(path.join(cfg, 'runtime')), false);
  assert.equal(fs.existsSync(`/proc/${info.pid}`), false);
  assert.doesNotMatch(fs.readFileSync(path.join(home, '.bashrc'), 'utf8'), /# added by memev/);
  assert.equal(fs.readFileSync(fakeLog, 'utf8'), '');

  const libc = execFileSync('getconf', ['GNU_LIBC_VERSION'], { encoding: 'utf8' }).trim();
  const body = [
    '# Linux command-line installer proof',
    '',
    `Date: 2026-11-07`,
    `Host: ${os.type()} ${os.release()} ${os.arch()}`,
    `System library: ${libc}`,
    `Memev commit: ${manifest.memev_commit}`,
    `Version: ${manifest.version}`,
    `Platform built and tested: linux-x64`,
    `Artifact: ${path.basename(first.artifact)}`,
    `SHA-256: ${first.sha256}`,
    'Two builds produced the same SHA-256.',
    'Unsigned. Not published to GitHub Releases. Not published to npm.',
    '',
    'Checked on this machine, with node, npm, npx, git, curl, wget, and sudo replaced by programs that fail if called:',
    '',
    '- A damaged package was refused and created no notes folder.',
    '- Install started Memev. The running program was the bundled Node binary.',
    `- Health answered on 127.0.0.1:${port}.`,
    '- A note was saved, an update ran, and the same note was still there.',
    '- Uninstall removed the program and left the note.',
    '- No stand-in for node, npm, or git was called.',
    '',
    'Not built and not tested here:',
    '',
    ...manifest.not_built.map((name) => `- ${name}`),
    '',
    'macOS and Windows were not run, including no simulated pass.',
    '',
  ].join('\n');
  fs.writeFileSync(proofPath, body);
});
