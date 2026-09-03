/*! Top Words (udsp) — Copyright 2026 Bulent Ozkir, Ahmet Arda Ozkir, Halit Eren Ozkir
 * Licensed under the PolyForm Noncommercial License 1.0.0 — NONCOMMERCIAL USE ONLY.
 * <https://polyformproject.org/licenses/noncommercial/1.0.0>
 *
 * Any commercial use requires prior written permission from the copyright
 * holders. Written permission from any ONE of bulentozkir@hotmail.com,
 * bulentozkir@gmail.com, ahmetardaozkir@gmail.com or haliterenozkir@gmail.com
 * is sufficient and binding on all of them.
 *
 * Required Notice: Copyright 2026 Bulent Ozkir, Ahmet Arda Ozkir, Halit Eren
 * Ozkir (https://udsp.vercel.app)
 * Full terms: see LICENSE and NOTICE in this repository.
 */

/**
 * Reads the JSON dump from _diag_wordquality.js and prints a language x level
 * matrix of word / definition / example health, so a deck can be judged on its
 * own terms instead of being hidden inside a corpus-wide total.
 *
 * Usage:
 *   node scripts/_diag_wordquality.js --json scripts/_wq.json
 *   node scripts/_diag_quality_matrix.js [scripts/_wq.json]
 */
'use strict';
const fs = require('fs');
const path = require('path');

const src = process.argv[2] || path.join(__dirname, '_wq.json');
const J = JSON.parse(fs.readFileSync(src, 'utf8'));

const GROUP = r => (r[0] === 'W' ? 'word' : r[0] === 'D' ? 'def' : r[0] === 'E' ? 'ex' : 'meta');

// file -> group -> count
const cells = {};
for (const [rule, per] of Object.entries(J.byRuleFile))
  for (const [file, n] of Object.entries(per)) {
    const c = (cells[file] = cells[file] || { word: 0, def: 0, ex: 0, meta: 0, top: {} });
    c[GROUP(rule)] += n;
    c.top[rule] = (c.top[rule] || 0) + n;
  }

const LEVEL_ORDER = ['A1', 'A2', 'B1', 'B2', 'C1', 'C2', 'TOEFL', 'PHRV', 'PART', 'SYN'];
const rows = Object.entries(J.perFile).sort((a, b) =>
  a[1].lang.localeCompare(b[1].lang) ||
  LEVEL_ORDER.indexOf(a[1].level) - LEVEL_ORDER.indexOf(b[1].level));

const pct = (n, d) => (d ? (100 * n / d).toFixed(0) : '0').padStart(3) + '%';
const worstRule = c => {
  const e = Object.entries(c.top).sort((a, b) => b[1] - a[1])[0];
  return e ? `${e[0]} x${e[1]}` : '-';
};

console.log('lang lvl    entries   word    def     ex    clean  dominant defect');
console.log('-'.repeat(78));
let prev = null;
for (const [file, v] of rows) {
  if (prev && prev !== v.lang) console.log('');
  prev = v.lang;
  const c = cells[file] || { word: 0, def: 0, ex: 0, meta: 0, top: {} };
  const clean = v.entries - v.dirtyEntries;
  console.log(
    `${v.lang.padEnd(4)} ${v.level.padEnd(5)} ${String(v.entries).padStart(6)} ` +
    `${String(c.word).padStart(6)} ${String(c.def).padStart(6)} ${String(c.ex).padStart(6)}  ` +
    `${pct(clean, v.entries)}  ${worstRule(c)}`);
}

// A deck can look fine on defect counts yet still be too small to study from,
// so surface size separately.
console.log('\n== deck size by language');
const byLang = {};
for (const v of Object.values(J.perFile)) {
  const b = (byLang[v.lang] = byLang[v.lang] || { entries: 0, dirty: 0, files: 0 });
  b.entries += v.entries; b.dirty += v.dirtyEntries; b.files++;
}
for (const [l, b] of Object.entries(byLang).sort((a, b) => b[1].entries - a[1].entries))
  console.log(`   ${l}  ${String(b.entries).padStart(6)} entries in ${b.files} files, ` +
    `${pct(b.entries - b.dirty, b.entries)} clean`);
