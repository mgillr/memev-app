// SPDX-License-Identifier: Apache-2.0
// The licence fence: this repository holds no engine and no licence, seat or paid logic.
// The app only reads the core's answers. Exits 1 and names each finding.
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

const root = path.resolve(process.argv[2] || '.');
const ENGINE = /\b(crdt[-_]merge|memev-merge|acfa[-_]rs)\b/i;
const LOGIC = /\b(verifyLicense|signLicense|seatPolicy|engineAllowed|LICENSE_KEYS|limitsFor)\b/;
// Prose counts: a public page can describe the method or state a patent position with no code at all.
const METHOD = /\b(OR-?Set|VectorClock|CausalTrustClock|DeltaTrustLattice|AggregateProofCarryingOperation|AdaptiveVerificationController)\b/i;
const FILING = /\bpatents?\b|\bprovisional (application|filing)\b/i;
const FILING_OK = /^LICENSE$/; // The Apache-2.0 text itself. Nothing else is exempt.
const SELF = /^scripts\/(fence|leak-scan)\.mjs$|^scripts\/leak-denylist\.json$|^test\/fence\.test\.mjs$/;
const files = execFileSync('git', ['ls-files', '-z', '--cached', '--others', '--exclude-standard'], { cwd: root, encoding: 'utf8' }).split('\0').filter(Boolean);
const findings = [];
for (const f of files) {
  if (SELF.test(f)) continue;
  if (/(^|\/)(crdt[-_]merge|memev-merge|acfa[-_]rs)(\/|$)/i.test(f)) findings.push(`${f}: an engine path`);
  let text;
  try { text = fs.readFileSync(path.join(root, f), 'utf8'); } catch { continue; }
  text.split('\n').forEach((line, i) => {
    if (ENGINE.test(line)) findings.push(`${f}:${i + 1}: names the engine`);
    if (LOGIC.test(line)) findings.push(`${f}:${i + 1}: licence or seat logic`);
    if (METHOD.test(line)) findings.push(`${f}:${i + 1}: describes the engine's method`);
    if (FILING.test(line) && !FILING_OK.test(f)) findings.push(`${f}:${i + 1}: patent or filing wording`);
  });
}
if (findings.length) { console.error(findings.join('\n')); process.exit(1); }
console.log(`fence: clean (${files.length} files)`);
