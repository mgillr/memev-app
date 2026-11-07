// Build the unsigned Linux x64 command-line installer.
// Downloads are checked against pins.json. Nothing is published.
import { spawn } from 'node:child_process';
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const pins = JSON.parse(fs.readFileSync(path.join(here, 'pins.json'), 'utf8'));
const cache = path.join(here, '.cache');
const dist = path.join(here, 'dist');
const topName = `memev-cli-${pins.version}-linux-x64`;

const IMPORT_RE = /(?:from|import|require\s*\(|import\s*\()\s*['"`]?\s*(?:@[\w.-]+\/)?crdt[-_]merge/i;
const BSL_RE = /Business Source License|BUSL-1\.1|SPDX-License-Identifier:\s*BUSL/i;
const BAD_PATH_RE = /(^|\/)(vendor\/(converge|crdt[-_]merge[^/]*)|crdt[-_]merge[^/]*|converge)(\/|$)/i;

function run(cmd, args, opts = {}) {
  const r = execFileSync(cmd, args, { stdio: ['ignore', 'pipe', 'pipe'], encoding: 'utf8', maxBuffer: 64 * 1024 * 1024, ...opts });
  return r;
}

function sha256File(file) {
  const h = createHash('sha256');
  h.update(fs.readFileSync(file));
  return h.digest('hex');
}

function download(url, name, expect) {
  const dest = path.join(cache, 'downloads', name);
  fs.mkdirSync(path.dirname(dest), { recursive: true });
  if (fs.existsSync(dest) && sha256File(dest) === expect) return dest;
  const partial = dest + '.partial';
  execFileSync('curl', ['--proto', '=https', '--tlsv1.2', '-fsSL', '--retry', '3', url, '-o', partial], { stdio: 'inherit' });
  const got = sha256File(partial);
  if (got !== expect) {
    fs.rmSync(partial, { force: true });
    throw new Error(`checksum mismatch for ${url}\nexpected ${expect}\ngot      ${got}`);
  }
  fs.renameSync(partial, dest);
  return dest;
}

function ensureSource() {
  const dir = process.env.MEMEV_SOURCE || path.join(cache, 'memev');
  if (!fs.existsSync(path.join(dir, '.git'))) {
    fs.mkdirSync(path.dirname(dir), { recursive: true });
    execFileSync('git', ['clone', '--depth', '1', pins.memev_repository, dir], { stdio: 'inherit' });
  }
  let head = execFileSync('git', ['-C', dir, 'rev-parse', 'HEAD'], { encoding: 'utf8' }).trim();
  if (head !== pins.memev_commit) {
    execFileSync('git', ['-C', dir, 'fetch', '--depth', '1', 'origin', pins.memev_commit], { stdio: 'inherit' });
    execFileSync('git', ['-C', dir, 'checkout', '--detach', 'FETCH_HEAD'], { stdio: 'inherit' });
    head = execFileSync('git', ['-C', dir, 'rev-parse', 'HEAD'], { encoding: 'utf8' }).trim();
  }
  if (head !== pins.memev_commit) throw new Error(`memev source is ${head}, pin is ${pins.memev_commit}`);
  const dirty = execFileSync('git', ['-C', dir, 'status', '--porcelain'], { encoding: 'utf8' }).trim();
  if (dirty) throw new Error('memev source has uncommitted changes');
  return dir;
}

function stable(value) {
  if (Array.isArray(value)) return value.map(stable);
  if (value && typeof value === 'object') {
    return Object.fromEntries(Object.keys(value).sort().map((key) => [key, stable(value[key])]));
  }
  return value;
}

function writeJson(file, value) {
  fs.writeFileSync(file, JSON.stringify(stable(value), null, 2) + '\n');
}

function packTarGz(stageDir, name, dest, epoch) {
  return new Promise((resolve, reject) => {
    const tar = spawn('tar', ['--sort=name', `--mtime=@${epoch}`, '--owner=0', '--group=0', '--numeric-owner', '-cf', '-', '-C', stageDir, name]);
    const gzip = spawn('gzip', ['-n']);
    const out = fs.createWriteStream(dest);
    let failed = false;
    const fail = (err) => { if (!failed) { failed = true; reject(err); } };
    tar.stdout.pipe(gzip.stdin);
    gzip.stdout.pipe(out);
    tar.stderr.on('data', (d) => process.stderr.write(d));
    gzip.stderr.on('data', (d) => process.stderr.write(d));
    tar.on('error', fail);
    gzip.on('error', fail);
    out.on('error', fail);
    let tarCode = null;
    let gzipCode = null;
    let outDone = false;
    const check = () => {
      if (failed || tarCode === null || gzipCode === null || !outDone) return;
      if (tarCode !== 0 || gzipCode !== 0) fail(new Error(`tar exited ${tarCode}, gzip exited ${gzipCode}`));
      else resolve();
    };
    tar.on('close', (code) => { tarCode = code; check(); });
    gzip.on('close', (code) => { gzipCode = code; check(); });
    out.on('finish', () => { outDone = true; check(); });
  });
}

function walk(dir, prefix, out) {
  for (const name of fs.readdirSync(dir)) {
    const abs = path.join(dir, name);
    const rel = prefix ? `${prefix}/${name}` : name;
    const st = fs.lstatSync(abs);
    if (st.isDirectory()) walk(abs, rel, out);
    else out.push(rel);
  }
}

function assertCoreBoundary(packageDir) {
  const files = [];
  walk(packageDir, '', files);
  const problems = [];
  for (const rel of files) {
    if (BAD_PATH_RE.test(rel)) problems.push(`${rel}: merge-engine path in the package`);
    const code = rel.startsWith('bin/') || rel.startsWith('src/') || rel.startsWith('vendor/') || rel.startsWith('templates/') || rel === 'package.json';
    if (!code) continue;
    const text = fs.readFileSync(path.join(packageDir, rel), 'utf8');
    if (IMPORT_RE.test(text)) problems.push(`${rel}: imports the external merge engine`);
    if (BSL_RE.test(text)) problems.push(`${rel}: carries a Business Source License header`);
    if (rel === 'package.json' && /crdt[-_]merge/i.test(text)) problems.push('package.json names the external merge engine');
  }
  if (problems.length) throw new Error('Apache core boundary failed:\n' + problems.join('\n'));
}

function copyLibs(fromRoot, destLib) {
  const dir = path.join(fromRoot, 'usr/lib/x86_64-linux-gnu');
  fs.mkdirSync(destLib, { recursive: true });
  for (const name of fs.readdirSync(dir)) {
    if (name.startsWith('libpcre2-8.so') || name.startsWith('libz.so')) {
      execFileSync('cp', ['-a', path.join(dir, name), path.join(destLib, name)]);
    }
  }
}

async function main() {
  execFileSync('dash', ['-n', path.join(here, 'payload', 'install.sh')]);
  const source = ensureSource();
  const runtime = JSON.parse(fs.readFileSync(path.join(source, 'scripts/node-runtime.json'), 'utf8'));
  const linux = runtime.platforms?.['linux-x64'];
  if (runtime.version !== pins.node.version || linux?.url !== pins.node.url || linux?.sha256 !== pins.node.sha256) {
    throw new Error('pins.json node entry does not match memev scripts/node-runtime.json at the pinned commit');
  }
  const pkg = JSON.parse(fs.readFileSync(path.join(source, 'package.json'), 'utf8'));
  if (pkg.version !== pins.version) throw new Error(`package version is ${pkg.version}, pin is ${pins.version}`);
  const epoch = execFileSync('git', ['-C', source, 'log', '-1', '--format=%ct'], { encoding: 'utf8' }).trim();

  const nodeTar = download(pins.node.url, 'node.tar.gz', pins.node.sha256);
  download(pins.git.source_url, 'git.dsc', pins.git.source_sha256);
  const debs = pins.git.packages.map((item) => download(item.url, `${item.name}.deb`, item.sha256));

  const stage = fs.mkdtempSync(path.join(os.tmpdir(), 'memev-cli-stage-'));
  const root = path.join(stage, topName);
  const payload = path.join(root, 'payload');
  fs.mkdirSync(payload, { recursive: true });

  const nodeExtract = path.join(cache, 'node-extract');
  fs.rmSync(nodeExtract, { recursive: true, force: true });
  fs.mkdirSync(nodeExtract, { recursive: true });
  execFileSync('tar', ['-xzf', nodeTar, '-C', nodeExtract]);
  const nodeTop = path.join(nodeExtract, `node-v${pins.node.version}-linux-x64`);
  fs.mkdirSync(path.join(payload, 'node/bin'), { recursive: true });
  execFileSync('cp', ['-a', path.join(nodeTop, 'bin/node'), path.join(payload, 'node/bin/node')]);
  fs.chmodSync(path.join(payload, 'node/bin/node'), 0o755);
  fs.copyFileSync(path.join(nodeTop, 'LICENSE'), path.join(payload, 'node/LICENSE'));

  const debRoot = path.join(cache, 'git-extract');
  fs.rmSync(debRoot, { recursive: true, force: true });
  fs.mkdirSync(debRoot, { recursive: true });
  for (const deb of debs) execFileSync('dpkg-deb', ['-x', deb, debRoot]);
  fs.mkdirSync(path.join(payload, 'git/bin'), { recursive: true });
  fs.mkdirSync(path.join(payload, 'git/libexec'), { recursive: true });
  execFileSync('cp', ['-a', path.join(debRoot, 'usr/bin/git'), path.join(payload, 'git/bin/git')]);
  execFileSync('cp', ['-a', path.join(debRoot, 'usr/lib/git-core'), path.join(payload, 'git/libexec/git-core')]);
  copyLibs(debRoot, path.join(payload, 'git/lib'));
  for (const doc of ['git', 'libpcre2-8-0', 'zlib1g']) {
    const src = path.join(debRoot, 'usr/share/doc', doc, 'copyright');
    if (!fs.existsSync(src)) throw new Error(`missing copyright file for ${doc}`);
    const dest = path.join(payload, 'git/share/doc', doc, 'copyright');
    fs.mkdirSync(path.dirname(dest), { recursive: true });
    fs.copyFileSync(src, dest);
  }

  const packJson = run('npm', ['pack', '--dry-run', '--json', '--ignore-scripts'], { cwd: source });
  const packed = JSON.parse(packJson.slice(packJson.indexOf('[')));
  const entries = packed[0].files;
  if (!entries?.length) throw new Error('npm pack listed no files');
  const packRoot = path.join(stage, 'pack');
  const packageDir = path.join(packRoot, 'package');
  for (const entry of entries) {
    const dest = path.join(packageDir, entry.path);
    fs.mkdirSync(path.dirname(dest), { recursive: true });
    fs.copyFileSync(path.join(source, entry.path), dest);
    fs.chmodSync(dest, entry.mode);
  }
  assertCoreBoundary(packageDir);
  const tgz = path.join(payload, 'memev.tgz');
  await packTarGz(packRoot, 'package', tgz, epoch);
  fs.writeFileSync(tgz + '.sha256', `${sha256File(tgz)}  memev.tgz\n`);
  fs.rmSync(path.join(payload, 'package'), { recursive: true, force: true });
  execFileSync('tar', ['-xzf', tgz, '-C', payload]);

  const checks = [
    ['node/bin/node', path.join(payload, 'node/bin/node')],
    ['git/bin/git', path.join(payload, 'git/bin/git')],
    ['memev.tgz', tgz],
  ];
  const lines = checks.map(([rel, abs]) => `${sha256File(abs)}  ${rel}`);
  fs.writeFileSync(path.join(payload, 'checksums.txt'), lines.join('\n') + '\n');

  const recorded = {};
  for (const [name, item] of Object.entries(runtime.platforms)) {
    recorded[name] = { url: item.url, sha256: item.sha256, built: name === 'linux-x64' };
  }
  const manifest = {
    schema: 1,
    name: 'memev-cli',
    version: pins.version,
    memev_commit: pins.memev_commit,
    platform: 'linux-x64',
    unsigned: true,
    signed: false,
    npm_published: false,
    github_release_published: false,
    intended_release_asset: `${topName}.tar.gz`,
    intended_release_url: `https://github.com/mgillr/memev/releases/download/v${pins.version}/${topName}.tar.gz`,
    glibc_min: pins.glibc_min,
    node: { version: pins.node.version, url: pins.node.url, sha256: pins.node.sha256, binary_sha256: sha256File(path.join(payload, 'node/bin/node')) },
    git: {
      version: pins.git.version,
      license: pins.git.license,
      source_url: pins.git.source_url,
      source_sha256: pins.git.source_sha256,
      binary_sha256: sha256File(path.join(payload, 'git/bin/git')),
      packages: pins.git.packages,
    },
    package_sha256: sha256File(tgz),
    node_runtime_platforms: recorded,
    not_built: pins.not_built,
    notes: [
      'Unsigned. No operating-system signature and no release signature.',
      'Not published to GitHub Releases and not published to npm.',
      'The file set inside memev.tgz is the npm pack file list from the pinned memev commit.',
    ],
  };
  writeJson(path.join(root, 'MANIFEST.json'), manifest);
  fs.copyFileSync(path.join(here, 'payload', 'install.sh'), path.join(root, 'install'));
  fs.chmodSync(path.join(root, 'install'), 0o755);
  fs.writeFileSync(path.join(root, 'THIRD-PARTY-NOTICES.txt'), thirdParty());

  fs.mkdirSync(dist, { recursive: true });
  const artifact = path.join(dist, `${topName}.tar.gz`);
  await packTarGz(stage, topName, artifact, epoch);
  const artifactHash = sha256File(artifact);
  fs.writeFileSync(artifact + '.sha256', `${artifactHash}  ${topName}.tar.gz\n`);
  fs.rmSync(stage, { recursive: true, force: true });
  process.stdout.write(`${artifact}\n${artifactHash}\n`);
}

function thirdParty() {
  const pkgs = pins.git.packages.map((item) => `${item.name} ${item.version}\n  ${item.url}\n  sha256 ${item.sha256}`).join('\n');
  return `Third-party programs included in this installer
==============================================

Node.js ${pins.node.version}
  License: the Node.js license shipped at payload/node/LICENSE
  ${pins.node.url}
  sha256 ${pins.node.sha256}
  Only the node program is included. npm and npx are not included.

Git ${pins.git.version} (Ubuntu package git ${pins.git.packages[0].version})
  License: GPL-2.0-only. The copyright file is at payload/git/share/doc/git/copyright.
  Source package: ${pins.git.source_url}
  sha256 ${pins.git.source_sha256}
  This installer does not change Git's license. Memev itself stays Apache-2.0.

Libraries needed to run that Git program
${pkgs}
  Their copyright files are under payload/git/share/doc/.

Memev ${pins.version} from ${pins.memev_repository} commit ${pins.memev_commit}
  License: Apache-2.0, inside the Memev package.
`;
}

main().catch((err) => {
  console.error(err.stack || err.message);
  process.exit(1);
});
