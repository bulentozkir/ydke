#!/usr/bin/env node
'use strict';

/**
 * Move complete English TOEFL entries into B2/C1/C2 without modifying any
 * existing destination entry.
 *
 * Placement precedence:
 *   1. CEFR-J (clamped to B2 because this migration targets B2+ only).
 *   2. hermitdave/FrequencyWords rank: <=35,000 B2; <=70,000 C1; else C2.
 *
 * Entries already present in any English A1-C2 file are removed from TOEFL
 * but never appended. Entries without a real bilingual definition and example
 * remain in TOEFL. The script is dry-run by default and updates manifest.json
 * only with --apply.
 *
 * Usage:
 *   node scripts/migrate-toefl-to-cefr.js \
 *     --frequency-file <hermitdave en_full.txt> \
 *     --cefrj-file <olp-en-cefrj csv> [--apply]
 */

const crypto = require('node:crypto');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
const dataDirectory = path.join(root, 'data');
const apply = process.argv.includes('--apply');
function argument(name) {
  const index = process.argv.indexOf(name);
  return index >= 0 ? process.argv[index + 1] : undefined;
}
const frequencyPath = argument('--frequency-file') || process.env.YDKE_EN_FREQUENCY_FILE;
const cefrPath = argument('--cefrj-file') || process.env.YDKE_CEFRJ_FILE;
const frequencyUrl = 'https://raw.githubusercontent.com/hermitdave/FrequencyWords/master/content/2018/en/en_full.txt';
const cefrUrl = 'https://raw.githubusercontent.com/openlanguageprofiles/olp-en-cefrj/master/cefrj-vocabulary-profile-1.5.csv';
const levelFiles = {
  A1: 'wordsa1.js', A2: 'wordsa2.js', B1: 'wordsb1.js',
  B2: 'wordsb2.js', C1: 'wordsc1.js', C2: 'wordsc2.js',
};
const destinations = ['B2', 'C1', 'C2'];
const fallback = /No example sentence available|No dictionary definition available|No definition available|Kein Beispielsatz|Aucune phrase d'exemple|Bu kelime için (örnek cümle|sözlük tanımı)|Örnek cümle (bulunmuyor|mevcut değil)|Tanım (bulunmuyor|mevcut değil)/i;
const cefrOrder = { A1: 1, A2: 2, B1: 3, B2: 4, C1: 5, C2: 6 };

function fail(message) { throw new Error(message); }
function normalizeWord(value) {
  return String(value ?? '').normalize('NFKC')
    .replace(/[’‘]/g, "'").replace(/[‐‑‒–—]/g, '-')
    .trim().toLocaleLowerCase('en-US').replace(/\s+/g, ' ');
}
function decodeJsString(value) {
  return value.replace(/\\(?:u\{([0-9a-fA-F]+)\}|u([0-9a-fA-F]{4})|x([0-9a-fA-F]{2})|([0btnrfv0'"\\]))/g,
    (match, codePoint, unicode, hex, simple) => {
      if (codePoint) return String.fromCodePoint(Number.parseInt(codePoint, 16));
      if (unicode) return String.fromCharCode(Number.parseInt(unicode, 16));
      if (hex) return String.fromCharCode(Number.parseInt(hex, 16));
      return ({ b: '\b', t: '\t', n: '\n', r: '\r', f: '\f', v: '\v', 0: '\0', "'": "'", '"': '"', '\\': '\\' })[simple] ?? simple;
    });
}
function field(raw, name) {
  const expression = new RegExp(`(?:^|\\n)\\s*${name}:\\s*"((?:\\\\.|[^"\\\\])*)"`, 'm');
  const match = expression.exec(raw);
  return match ? decodeJsString(match[1]) : null;
}
function objectSpans(source, arrayStart) {
  const spans = [];
  let depth = 0;
  let objectStart = -1;
  let quote = null;
  let escaped = false;
  for (let index = arrayStart + 1; index < source.length; index++) {
    const character = source[index];
    if (quote) {
      if (escaped) escaped = false;
      else if (character === '\\') escaped = true;
      else if (character === quote) quote = null;
      continue;
    }
    if (character === '"' || character === "'") { quote = character; continue; }
    if (source.startsWith('//', index)) {
      const end = source.indexOf('\n', index + 2);
      index = end < 0 ? source.length : end;
      continue;
    }
    if (source.startsWith('/*', index)) {
      const end = source.indexOf('*/', index + 2);
      if (end < 0) fail('Unclosed block comment.');
      index = end + 1;
      continue;
    }
    if (character === '{') {
      if (depth === 0) objectStart = index;
      depth++;
    } else if (character === '}') {
      depth--;
      if (depth < 0) fail('Object depth became negative.');
      if (depth === 0) spans.push([objectStart, index + 1]);
    } else if (character === ']' && depth === 0) {
      return { spans, arrayEnd: index };
    }
  }
  fail('Dataset array has no closing bracket.');
}
function parseDataset(fileName, expectedLevel, requireContent = false) {
  const fullPath = path.join(dataDirectory, fileName);
  const source = fs.readFileSync(fullPath, 'utf8');
  const marker = /window\.([A-Za-z0-9_]+)\s*=\s*\[/.exec(source);
  if (!marker) fail(`${fileName}: missing window array assignment.`);
  const arrayStart = marker.index + marker[0].lastIndexOf('[');
  const { spans, arrayEnd } = objectSpans(source, arrayStart);
  const entries = spans.map(([start, end], index) => {
    const raw = source.slice(start, end);
    const entry = {
      fileName, index, raw,
      word: field(raw, 'word'), level: field(raw, 'level'),
      definition: field(raw, 'definition'), example: field(raw, 'example'),
    };
    if (!entry.word || !entry.level || (requireContent && (entry.definition === null || entry.example === null))) {
      const required = requireContent ? ['word', 'level', 'definition', 'example'] : ['word', 'level'];
      const missing = required.filter(name => entry[name] === null || entry[name] === undefined || (name === 'word' && !entry[name]));
      fail(`${fileName}: record ${index + 1} is missing ${missing.join(', ')}: ${raw.slice(0, 240).replace(/\s+/g, ' ')}`);
    }
    if (expectedLevel && entry.level !== expectedLevel)
      fail(`${fileName}: ${entry.word} has level ${entry.level}; expected ${expectedLevel}.`);
    return entry;
  });
  const declaredWords = [...source.matchAll(/(?:^|\n)\s*word:\s*"/g)].length;
  if (entries.length !== declaredWords) fail(`${fileName}: parsed ${entries.length}; found ${declaredWords} word fields.`);
  return { fileName, fullPath, source, entries, arrayStart, arrayEnd };
}
function bilingual(value) {
  const separator = value.lastIndexOf(' - ');
  return separator > 0 && value.slice(0, separator).trim().length > 0 && value.slice(separator + 3).trim().length > 0;
}
function eligible(entry) {
  return entry.definition.trim() && entry.example.trim() &&
    !fallback.test(entry.definition) && !fallback.test(entry.example) &&
    bilingual(entry.definition) && bilingual(entry.example);
}
function csvRow(line) {
  const fields = [];
  let value = '';
  let quoted = false;
  for (let index = 0; index < line.length; index++) {
    const character = line[index];
    if (character === '"') {
      if (quoted && line[index + 1] === '"') { value += '"'; index++; }
      else quoted = !quoted;
    } else if (character === ',' && !quoted) { fields.push(value); value = ''; }
    else value += character;
  }
  fields.push(value);
  return fields;
}
function requireReference(filePath, option, url) {
  if (!filePath || !fs.existsSync(filePath) || fs.statSync(filePath).size <= 1000)
    fail(`Pass ${option} with the downloaded reference file (${url}).`);
}
function loadFrequency() {
  const rank = new Map();
  let position = 0;
  for (const line of fs.readFileSync(frequencyPath, 'utf8').split(/\r?\n/)) {
    const token = line.trim().split(/\s+/)[0];
    if (!token) continue;
    position++;
    const key = normalizeWord(token);
    if (!rank.has(key)) rank.set(key, position);
  }
  return rank;
}
function loadCefr() {
  const result = new Map();
  for (const line of fs.readFileSync(cefrPath, 'utf8').split(/\r?\n/).slice(1)) {
    if (!line) continue;
    const row = csvRow(line);
    const level = row[2]?.trim().toUpperCase();
    if (!(level in cefrOrder)) continue;
    for (const part of row[0].split(/[/,]/).map(normalizeWord).filter(Boolean)) {
      const previous = result.get(part);
      if (!previous || cefrOrder[level] < cefrOrder[previous]) result.set(part, level);
    }
  }
  return result;
}
function rewriteLevel(entry, level) {
  const expression = /(^|\n)(\s*level:\s*")TOEFL(",?\s*(?:\n|$))/g;
  let replacements = 0;
  const raw = entry.raw.replace(expression, (match, start, prefix, end) => {
    replacements++;
    return `${start}${prefix}${level}${end}`;
  });
  if (replacements !== 1) fail(`Expected one TOEFL level field for ${entry.word}; changed ${replacements}.`);
  return raw;
}
function indentBlock(raw) { return `  ${raw}`; }
function appendBlocks(dataset, blocks, label) {
  if (!blocks.length) return dataset.source;
  const before = dataset.source.slice(0, dataset.arrayEnd);
  const after = dataset.source.slice(dataset.arrayEnd);
  const separator = before.endsWith('\n') ? '' : '\n';
  const comment = `  // ${blocks.length.toLocaleString('en-US')} complete English entries moved from toefl.js (${label}).\n`;
  return before + separator + comment + blocks.map(indentBlock).join(',\n') + ',\n' + after;
}
function rewriteSource(dataset, retained) {
  const prefix = dataset.source.slice(0, dataset.arrayStart + 1);
  const suffix = dataset.source.slice(dataset.arrayEnd);
  if (!retained.length) return `${prefix}\n${suffix}`;
  return prefix + '\n' + retained.map(entry => indentBlock(entry.raw)).join(',\n') + ',\n' + suffix;
}
function updateDestinationHeader(text, level, count) {
  const placement = level === 'B2'
    ? '// Includes complete TOEFL entries assigned by CEFR-J or frequency rank <= 35,000.'
    : level === 'C1'
      ? '// Includes complete TOEFL entries assigned by CEFR-J or frequency ranks 35,001-70,000.'
      : '// Includes complete TOEFL entries assigned by CEFR-J C2, rank > 70,000, or no rank.';
  text = text.replace(new RegExp(`// Top ${level} / UDSP English exam vocabulary — full word list \\([^\\n]*\\)\\.`),
    `// Top ${level} / UDSP English exam vocabulary — full word list (${count.toLocaleString('en-US')} words).`);
  if (!text.includes(placement)) text = text.replace(/(\/\/ Source word list:[^\n]*\n)/, `$1${placement}\n`);
  return text;
}
function validateOutput(fileName, text, expectedLevel, expectedCount) {
  const marker = /window\.([A-Za-z0-9_]+)\s*=\s*\[/.exec(text);
  if (!marker) fail(`${fileName}: generated output lost its array marker.`);
  const arrayStart = marker.index + marker[0].lastIndexOf('[');
  const { spans } = objectSpans(text, arrayStart);
  if (spans.length !== expectedCount) fail(`${fileName}: generated ${spans.length}; expected ${expectedCount}.`);
  for (const [index, [start, end]] of spans.entries()) {
    const raw = text.slice(start, end);
    const word = field(raw, 'word');
    const level = field(raw, 'level');
    if (!word || level !== expectedLevel) fail(`${fileName}: generated record ${index + 1} has invalid word/level.`);
  }
}
function manifestFor(outputs) {
  const files = fs.readdirSync(dataDirectory).filter(name => name.endsWith('.js')).sort();
  const rows = files.map(name => {
    const buffer = outputs.has(name) ? Buffer.from(outputs.get(name), 'utf8') : fs.readFileSync(path.join(dataDirectory, name));
    return { name, bytes: buffer.length, sha256: crypto.createHash('sha256').update(buffer).digest('hex') };
  });
  return JSON.stringify({
    schema: 1,
    source: 'ydke-local-import',
    sourcePath: 'data/',
    generated: new Date().toISOString().replace(/\.\d{3}Z$/, 'Z'),
    fileCount: rows.length,
    totalBytes: rows.reduce((sum, row) => sum + row.bytes, 0),
    files: rows,
  }, null, 2) + '\n';
}
function atomicWrite(outputs) {
  const originals = new Map();
  const temporary = [];
  try {
    for (const [name, text] of outputs) {
      const target = path.join(dataDirectory, name);
      originals.set(name, fs.existsSync(target) ? fs.readFileSync(target) : null);
      const temp = `${target}.${process.pid}.${crypto.randomUUID()}.tmp`;
      fs.writeFileSync(temp, text, 'utf8');
      temporary.push([temp, target]);
    }
    for (const [temp, target] of temporary) fs.renameSync(temp, target);
  } catch (error) {
    for (const [name, buffer] of originals) {
      const target = path.join(dataDirectory, name);
      if (buffer === null) fs.rmSync(target, { force: true });
      else fs.writeFileSync(target, buffer);
    }
    for (const [temp] of temporary) fs.rmSync(temp, { force: true });
    throw error;
  }
}

async function main() {
  requireReference(frequencyPath, '--frequency-file', frequencyUrl);
  requireReference(cefrPath, '--cefrj-file', cefrUrl);
  const frequency = loadFrequency();
  const cefr = loadCefr();
  const datasets = Object.fromEntries(Object.entries(levelFiles).map(([level, fileName]) => [level, parseDataset(fileName, level)]));
  const toefl = parseDataset('toefl.js', 'TOEFL', true);

  const existing = new Map();
  for (const [level, dataset] of Object.entries(datasets)) {
    for (const entry of dataset.entries) {
      const key = normalizeWord(entry.word);
      if (!existing.has(key)) existing.set(key, []);
      existing.get(key).push({ level, entry });
    }
  }
  const sourceKeys = new Set();
  for (const entry of toefl.entries) {
    const key = normalizeWord(entry.word);
    if (!key || sourceKeys.has(key)) fail(`Duplicate/empty TOEFL headword: ${entry.word}`);
    sourceKeys.add(key);
  }

  function targetLevel(entry) {
    const key = normalizeWord(entry.word);
    const reference = cefr.get(key);
    if (reference) return cefrOrder[reference] <= cefrOrder.B2 ? 'B2' : reference;
    const rank = frequency.get(key);
    if (rank !== undefined && rank <= 35000) return 'B2';
    if (rank !== undefined && rank <= 70000) return 'C1';
    return 'C2';
  }

  const moved = { B2: [], C1: [], C2: [] };
  const retained = [];
  const overlaps = [];
  let cefrClassified = 0;
  let frequencyClassified = 0;
  let unrankedC2 = 0;
  let ineligible = 0;
  for (const entry of toefl.entries) {
    const key = normalizeWord(entry.word);
    if (existing.has(key)) { overlaps.push(entry); continue; }
    if (!eligible(entry)) { retained.push(entry); ineligible++; continue; }
    const level = targetLevel(entry);
    moved[level].push(rewriteLevel(entry, level));
    if (cefr.has(key)) cefrClassified++;
    else if (frequency.has(key)) frequencyClassified++;
    else unrankedC2++;
    existing.set(key, [{ level, entry }]);
  }

  const outputs = new Map();
  outputs.set('toefl.js', rewriteSource(toefl, retained));
  for (const level of destinations) {
    const appended = appendBlocks(datasets[level], moved[level],
      level === 'B2' ? 'CEFR-J / frequency rank ≤ 35,000' : level === 'C1' ? 'CEFR-J / frequency rank 35,001-70,000' : 'CEFR-J C2 / rank > 70,000');
    outputs.set(levelFiles[level], updateDestinationHeader(appended, level, datasets[level].entries.length + moved[level].length));
  }
  for (const level of destinations) {
    const originalArray = datasets[level].source.slice(datasets[level].arrayStart, datasets[level].arrayEnd);
    if (!outputs.get(levelFiles[level]).includes(originalArray)) fail(`${level}: an existing destination array byte changed.`);
    validateOutput(levelFiles[level], outputs.get(levelFiles[level]), level, datasets[level].entries.length + moved[level].length);
  }
  validateOutput('toefl.js', outputs.get('toefl.js'), 'TOEFL', retained.length);

  const allFinalKeys = new Set();
  let introducedOverlaps = 0;
  for (const level of Object.keys(levelFiles)) {
    const entries = level === 'B2' || level === 'C1' || level === 'C2'
      ? parseGenerated(outputs.get(levelFiles[level]), levelFiles[level])
      : datasets[level].entries;
    for (const entry of entries) {
      const key = normalizeWord(entry.word);
      if (allFinalKeys.has(key) && !existingOriginalDuplicate(key, datasets)) introducedOverlaps++;
      allFinalKeys.add(key);
    }
  }
  if (introducedOverlaps) fail(`Migration introduced ${introducedOverlaps} new CEFR overlaps.`);

  const report = {
    mode: apply ? 'apply' : 'dry-run',
    sourceBefore: toefl.entries.length,
    sourceAfter: retained.length,
    moved: { B2: moved.B2.length, C1: moved.C1.length, C2: moved.C2.length, total: destinations.reduce((sum, level) => sum + moved[level].length, 0) },
    removedBecauseAlreadyInA1ToC2: overlaps.length,
    retainedWithoutRealBilingualDefinitionAndExample: ineligible,
    classifiedBy: { cefrJ: cefrClassified, frequency: frequencyClassified, unrankedC2 },
    destinationBefore: Object.fromEntries(destinations.map(level => [level, datasets[level].entries.length])),
    destinationAfter: Object.fromEntries(destinations.map(level => [level, datasets[level].entries.length + moved[level].length])),
    existingDestinationEntriesChanged: 0,
    newOverlapsIntroduced: 0,
  };
  if (report.sourceAfter + report.moved.total + report.removedBecauseAlreadyInA1ToC2 !== report.sourceBefore)
    fail('Source conservation invariant failed.');

  if (apply) {
    outputs.set('manifest.json', manifestFor(outputs));
    atomicWrite(outputs);
  }
  console.log(JSON.stringify(report, null, 2));
}

function parseGenerated(source, fileName) {
  const marker = /window\.([A-Za-z0-9_]+)\s*=\s*\[/.exec(source);
  const arrayStart = marker.index + marker[0].lastIndexOf('[');
  const { spans } = objectSpans(source, arrayStart);
  return spans.map(([start, end], index) => ({ fileName, index, word: field(source.slice(start, end), 'word') }));
}
function existingOriginalDuplicate(key, datasets) {
  let count = 0;
  for (const dataset of Object.values(datasets)) count += dataset.entries.filter(entry => normalizeWord(entry.word) === key).length;
  return count > 1;
}

main().catch(error => {
  console.error(error.stack || error.message);
  process.exitCode = 1;
});
