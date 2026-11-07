// SPDX-License-Identifier: Apache-2.0
// The licence fence: this repository holds no engine and no licence, seat or paid logic.
// The app only reads the core's answers. Exits 1 and names each finding.
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

const root = path.resolve(process.argv[2] || '.');
const ENGINE = /crdt[-_ ]?merge|memev[-_ ]?merge|\bacfa(?:[-_][a-z0-9]+)?\b/i;
const E4 = /\bE4\b/; // Case-sensitive: the engine's trust layer. A colour or byte in lower case does not trip it.
const LOGIC = /verify[-_]?licen[cs]e|sign[-_]?licen[cs]e|seat[-_]?policy|engine[-_]?allowed|licen[cs]e[-_]?keys|limits[-_]?for/i; // Any language's spelling.
// Prose counts: a public page can describe the method or state a patent position with no code at all.
const METHOD = /\bor-?set\b|observed[- ]remove|vector[- ]?clock|causal[- ]?trust|delta[- ]?trust|proof[- ]carrying|adaptive[- ]?verification|\blww[- ]?(map|register|set)\b|\bmerkle\b|\btwo[- ]layer\b/i;
const FILING = /\bpatents?\b|\bprovisional (application|filing)\b/i;
const FILING_OK = /^LICENSE$/; // The Apache-2.0 text itself. No other whole file is exempt.
// The SIL Open Font License ships verbatim beside each bundled font, and its disclaimer names the word once. Only that one line,
// in those licence files, is exempt; any other line in them is still scanned.
const OFL_FILE = /^native\/macos\/fonts\/OFL-[A-Za-z]+\.txt$/;
const OFL_LINE = 'OF COPYRIGHT, PATENT, TRADEMARK, OR OTHER RIGHT. IN NO EVENT SHALL THE';
const SELF = /^scripts\/(fence|leak-scan)\.mjs$|^scripts\/leak-denylist\.json$|^test\/fence\.test\.mjs$/;
const files = execFileSync('git', ['ls-files', '-z', '--cached', '--others', '--exclude-standard'], { cwd: root, encoding: 'utf8' }).split('\0').filter(Boolean);
const findings = [];
for (const f of files) {
  if (SELF.test(f)) continue;
  if (ENGINE.test(f)) findings.push(`${f}: an engine path`); // A packaged engine counts, not only a folder of that name.
  let text;
  try { text = fs.readFileSync(path.join(root, f), 'utf8'); } catch { continue; }
  text.split('\n').forEach((line, i) => {
    if (ENGINE.test(line) || E4.test(line)) findings.push(`${f}:${i + 1}: names the engine`);
    if (LOGIC.test(line)) findings.push(`${f}:${i + 1}: licence or seat logic`);
    if (METHOD.test(line)) findings.push(`${f}:${i + 1}: describes the engine's method`);
    if (FILING.test(line) && !FILING_OK.test(f) && !(OFL_FILE.test(f) && line.trimEnd() === OFL_LINE)) findings.push(`${f}:${i + 1}: patent or filing wording`);
  });
}
if (findings.length) { console.error(findings.join('\n')); process.exit(1); }
console.log(`fence: clean (${files.length} files)`);
