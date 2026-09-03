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
 * Comprehensive word/definition/example quality audit across every vocabulary
 * data file (all 6 languages, all levels, plus TOEFL / phrasal-verb /
 * Partikelverb / synonym-antonym sets).
 *
 * Usage:
 *   node scripts/_diag_wordquality.js            -> summary + per-rule counts
 *   node scripts/_diag_wordquality.js --samples  -> add example hits per rule
 *   node scripts/_diag_wordquality.js --rule R_CIRCULAR --samples
 *   node scripts/_diag_wordquality.js --lang de
 *   node scripts/_diag_wordquality.js --json out.json
 */
'use strict';
const path = require('path');
const fs = require('fs');

const ROOT = path.join(__dirname, '..');
global.window = {};

const FILES = [
  // lang, level, file, globalName, separator
  ['en', 'A1', 'wordsa1.js', 'WORDS_A1', ' - '],
  ['en', 'A2', 'wordsa2.js', 'WORDS_A2', ' - '],
  ['en', 'B1', 'wordsb1.js', 'WORDS_B1', ' - '],
  ['en', 'B2', 'wordsb2.js', 'WORDS_B2', ' - '],
  ['en', 'C1', 'wordsc1.js', 'WORDS_C1', ' - '],
  ['en', 'C2', 'wordsc2.js', 'WORDS_C2', ' - '],
  ['en', 'TOEFL', 'toefl.js', 'WORDS_TOEFL', ' - '],
  ['en', 'PHRV', 'phrasalverbsen.js', 'PHRASAL_VERBS_EN', ' - '],
  ['en', 'SYN', 'synanten.js', 'SYN_ANT_EN', ';'],
  ['de', 'A1', 'wordsa1gode.js', 'WORDS_GODE_A1', ' - '],
  ['de', 'A2', 'wordsa2gode.js', 'WORDS_GODE_A2', ' - '],
  ['de', 'B1', 'wordsb1gode.js', 'WORDS_GODE_B1', ' - '],
  ['de', 'B2', 'wordsb2gode.js', 'WORDS_GODE_B2', ' - '],
  ['de', 'C1', 'wordsc1gode.js', 'WORDS_GODE_C1', ' - '],
  ['de', 'C2', 'wordsc2gode.js', 'WORDS_GODE_C2', ' - '],
  ['de', 'PART', 'partikelverbde.js', 'PARTIKELVERB_DE', ' - '],
  ['de', 'SYN', 'synantde.js', 'SYN_ANT_DE', ';'],
  ['fr', 'A1', 'wordsa1fr.js', 'WORDS_FR_A1', ' - '],
  ['fr', 'A2', 'wordsa2fr.js', 'WORDS_FR_A2', ' - '],
  ['fr', 'B1', 'wordsb1fr.js', 'WORDS_FR_B1', ' - '],
  ['fr', 'B2', 'wordsb2fr.js', 'WORDS_FR_B2', ' - '],
  ['fr', 'C1', 'wordsc1fr.js', 'WORDS_FR_C1', ' - '],
  ['fr', 'C2', 'wordsc2fr.js', 'WORDS_FR_C2', ' - '],
  ['fr', 'PHRV', 'phrasalverbsfr.js', 'PHRASAL_VERBS_FR', ' - '],
  ['fr', 'SYN', 'synantfr.js', 'SYN_ANT_FR', ';'],
  ['it', 'A1', 'wordsa1it.js', 'WORDS_IT_A1', ' - '],
  ['it', 'A2', 'wordsa2it.js', 'WORDS_IT_A2', ' - '],
  ['it', 'B1', 'wordsb1it.js', 'WORDS_IT_B1', ' - '],
  ['it', 'B2', 'wordsb2it.js', 'WORDS_IT_B2', ' - '],
  ['it', 'C1', 'wordsc1it.js', 'WORDS_IT_C1', ' - '],
  ['it', 'C2', 'wordsc2it.js', 'WORDS_IT_C2', ' - '],
  ['es', 'A1', 'wordsa1es.js', 'WORDS_ES_A1', ' - '],
  ['es', 'A2', 'wordsa2es.js', 'WORDS_ES_A2', ' - '],
  ['es', 'B1', 'wordsb1es.js', 'WORDS_ES_B1', ' - '],
  ['es', 'B2', 'wordsb2es.js', 'WORDS_ES_B2', ' - '],
  ['es', 'C1', 'wordsc1es.js', 'WORDS_ES_C1', ' - '],
  ['es', 'C2', 'wordsc2es.js', 'WORDS_ES_C2', ' - '],
  ['pt', 'A1', 'wordsa1pt.js', 'WORDS_PT_A1', ' - '],
  ['pt', 'A2', 'wordsa2pt.js', 'WORDS_PT_A2', ' - '],
  ['pt', 'B1', 'wordsb1pt.js', 'WORDS_PT_B1', ' - '],
  ['pt', 'B2', 'wordsb2pt.js', 'WORDS_PT_B2', ' - '],
  ['pt', 'C1', 'wordsc1pt.js', 'WORDS_PT_C1', ' - '],
  ['pt', 'C2', 'wordsc2pt.js', 'WORDS_PT_C2', ' - '],
  ['nl', 'A1', 'wordsa1nl.js', 'WORDS_NL_A1', ' - '],
  ['nl', 'A2', 'wordsa2nl.js', 'WORDS_NL_A2', ' - '],
  ['nl', 'B1', 'wordsb1nl.js', 'WORDS_NL_B1', ' - '],
  ['nl', 'B2', 'wordsb2nl.js', 'WORDS_NL_B2', ' - '],
  ['nl', 'C1', 'wordsc1nl.js', 'WORDS_NL_C1', ' - '],
  ['nl', 'C2', 'wordsc2nl.js', 'WORDS_NL_C2', ' - '],
];

// ---------------------------------------------------------------- rule set
const RULES = {
  // --- headword ---
  W_EMPTY:       'headword empty or whitespace only',
  W_JUNK:        'headword has digits / odd characters / is a single letter',
  W_SPACE:       'headword has leading or trailing whitespace',
  W_DUPE:        'headword duplicated within the same file (exact)',
  W_DUPE_CASE:   'headword duplicated within the same file ignoring case',
  W_CROSSLEVEL:  'same headword also taught at another CEFR level of this language',
  W_LONG:        'headword longer than 25 characters',
  W_MULTIWORD:   'headword is 4+ words (a phrase/definition fragment, not a lexical item)',
  W_FOREIGN:     'headword contains characters outside the target language script',
  W_MOJIBAKE:    'headword/definition/example contains mojibake or a lost accent (fa?ade)',

  // --- definition ---
  D_NOSEP:       'definition has no bilingual separator (renders raw)',
  D_MULTISEP:    'definition has 2+ separators (splits at the wrong place)',
  D_EMPTYHALF:   'definition native or Turkish half is empty',
  D_FALLBACK:    'definition is an "unavailable" placeholder',
  D_SYNLIST:     'definition is a synonym list, not a definition ("Similar to: x, y")',
  D_FORMOF:      'definition is a Wiktionary form-of stub (plural/participle of ...)',
  D_CIRCULAR:    'short definition restates the headword instead of explaining it',
  D_TOOLONG:     'definition over 160 chars (overflows the card on a phone)',
  D_HUGE:        'definition over 300 chars (encyclopedia dump)',
  D_TOOSHORT:    'definition native half is effectively empty (under 2 letters)',
  D_UNTRANSLATED:'Turkish half is byte-identical to the native half',
  D_SAME_AS_EX:  'definition identical to the example',
  D_REGISTER:    'definition marked obsolete / archaic / rare / dialectal',
  D_MARKUP:      'definition contains HTML or entity markup',
  D_SHARED:      'near-duplicate entries: 5+ headwords in the file share one definition',

  // --- example ---
  E_NOSEP:       'example has no bilingual separator (renders raw)',
  E_MULTISEP:    'example has 2+ separators (splits at the wrong place)',
  E_EMPTYHALF:   'example native or Turkish half is empty',
  E_FALLBACK:    'example is an "unavailable" placeholder',
  E_NOWORD:      'headword never appears in its own example sentence',
  E_TOOLONG:     'example native half over 200 chars',
  E_TOOSHORT:    'example native half under 10 chars',
  E_UNTRANSLATED:'example Turkish half is byte-identical to the native half',
  E_SHARED:      'one example sentence reused verbatim by 5+ different headwords',
  E_MARKUP:      'example contains HTML or entity markup',

  // --- metadata ---
  M_NOPOS:       'pos field missing or empty (words*.js schema only)',
  M_NOCAT:       'category field missing or empty',
};

const FALLBACK = /No example sentence available|No dictionary definition available|No definition available|Kein Beispielsatz|Aucune phrase d'exemple|Bu kelime için (örnek cümle|sözlük tanımı)|Örnek cümle (bulunmuyor|mevcut değil)|Tanım (bulunmuyor|mevcut değil)/i;
const SYNLIST = /^\s*(Similar to|Ähnlich wie|Semblable à|Simile a|Similar a|Benzer|Şuna benzer|Benzeri)\s*:/i;
const FORMOF = new RegExp([
  'present participle of', 'past participle of', 'perfect participle',
  'gerund of', 'inflection of', 'plural of', 'singular of',
  'comparative (degree )?of', 'superlative (degree )?of', 'simple past of',
  'third-person singular of', '-ing form of',
  'Gerundium von', 'Partizip\\b', 'Plural von', 'Singular von',
  'Komparativ von', 'Superlativ von', 'Präteritum von', 'Flexion von',
  'Grundform von', 'substantivierter Infinitiv', 'Infinitiv von',
  'participe (passé|présent) de', 'pluriel de', 'féminin de',
  'plurale di', 'participio (passato|presente) di',
  'plural de', 'participio de',
].join('|'), 'i');
const REGISTER = /\((obsolete|archaic|rare|dialectal|dated|nonstandard|poetic)\)|\b(now obsolete|now archaic|chiefly dialectal)\b/i;
const MARKUP = /<\/?[a-z][^>]*>|&(nbsp|amp|quot|lt|gt|#\d+);/i;
const JUNKWORD = /[0-9_@#\\/]|^.$/;
// JavaScript's \w and \W stay ASCII-only even under the /u flag, so
// [^\W\d_] silently treats every umlaut and accent as a word boundary
// ("Ich heisse" tokenised to ich/hei/e). Match letters explicitly instead.
const TOKEN = /[\p{L}\p{M}]+/gu;

// Scripts we expect per language. Latin-1 accents are allowed everywhere
// because loanwords (résumé, protégé, attaché) are legitimate headwords.
const SCRIPT_OK = {
  en: /^[a-zA-Z\u00C0-\u024F' .\-]+$/,
  de: /^[a-zA-Z\u00C0-\u024F' .\-]+$/,
  fr: /^[a-zA-Z\u00C0-\u024F' .\-]+$/,
  it: /^[a-zA-Z\u00C0-\u024F' .\-]+$/,
  es: /^[a-zA-Z\u00C0-\u024F¡¿' .\-]+$/,
  pt: /^[a-zA-Z\u00C0-\u024F' .\-]+$/,
  nl: /^[a-zA-Z\u00C0-\u024F' .\-]+$/,
};
// Genuine encoding damage: replacement char, or '?' / mojibake pair where an
// accented letter should be (fa?ade, KÃ¤se).
const MOJIBAKE = /\uFFFD|[a-z]\?[a-z]|Ã[\u0080-\u00bf]|Â[\u0080-\u00bf]/i;
const SINGLE_WORD_SET = lvl => !['PHRV', 'PART'].includes(lvl);

// fr/de/it/es/pt store the definite article inside `word` ("le chat", "der
// Hund", "l'ami"). Strip it before matching against the example sentence.
const ARTICLE = {
  fr: /^(l['\u2019]|les?\s+|la\s+|un\s+|une\s+|des\s+|du\s+)/i,
  de: /^(der|die|das|den|dem|ein|eine)\s+/i,
  it: /^(il|lo|la|i|gli|le|l['\u2019]|un|uno|una)\s*/i,
  es: /^(el|la|los|las|un|una)\s+/i,
  pt: /^(o|a|os|as|um|uma)\s+/i,
  nl: /^(de|het|een)\s+/i,
};
const stripArticle = (w, lang) => (ARTICLE[lang] ? w.replace(ARTICLE[lang], '').trim() || w : w);

// --------------------------------------------------------------- utilities
function splitPair(s, sep) {
  if (typeof s !== 'string') return { n: '', t: '', count: 0 };
  const parts = s.split(sep);
  return { n: (parts[0] || '').trim(), t: parts.slice(1).join(sep).trim(), count: parts.length - 1 };
}
function tokens(s) { return (s || '').toLowerCase().match(TOKEN) || []; }
// English irregular forms that a token/prefix match can never reach.
const IRREGULAR = {
  be: ['am', 'is', 'are', 'was', 'were', 'been'], go: ['went', 'gone'], become: ['became'],
  draw: ['drew', 'drawn'], foot: ['feet'], tooth: ['teeth'], goose: ['geese'], mouse: ['mice'],
  get: ['got', 'gotten'], make: ['made'], pay: ['paid'], say: ['said'], see: ['saw', 'seen'],
  do: ['did', 'done'], have: ['had', 'has'], take: ['took', 'taken'], come: ['came'],
  give: ['gave', 'given'], find: ['found'], think: ['thought'], tell: ['told'], leave: ['left'],
  feel: ['felt'], bring: ['brought'], begin: ['began', 'begun'], keep: ['kept'], hold: ['held'],
  write: ['wrote', 'written'], stand: ['stood'], hear: ['heard'], let: ['let'], mean: ['meant'],
  meet: ['met'], run: ['ran'], sit: ['sat'], speak: ['spoke', 'spoken'], lie: ['lay', 'lain'],
  lead: ['led'], read: ['read'], grow: ['grew', 'grown'], lose: ['lost'], fall: ['fell', 'fallen'],
  send: ['sent'], build: ['built'], understand: ['understood'], eat: ['ate', 'eaten'],
  buy: ['bought'], catch: ['caught'], teach: ['taught'], fight: ['fought'], seek: ['sought'],
  choose: ['chose', 'chosen'], drive: ['drove', 'driven'], break: ['broke', 'broken'],
  wear: ['wore', 'worn'], sell: ['sold'], tear: ['tore', 'torn'], sleep: ['slept'],
  drink: ['drank', 'drunk'], swim: ['swam', 'swum'], ring: ['rang', 'rung'], sing: ['sang', 'sung'],
  throw: ['threw', 'thrown'], fly: ['flew', 'flown'], know: ['knew', 'known'], blow: ['blew', 'blown'],
  child: ['children'], person: ['people'], man: ['men'], woman: ['women'], life: ['lives'],
  knife: ['knives'], wife: ['wives'], leaf: ['leaves'], shelf: ['shelves'], half: ['halves'],
  dig: ['dug'], hang: ['hung'], stick: ['stuck'], strike: ['struck'], win: ['won'], shoot: ['shot'],
  spend: ['spent'], lend: ['lent'], bend: ['bent'], feed: ['fed'], bleed: ['bled'], breed: ['bred'],
  hide: ['hid', 'hidden'], bite: ['bit', 'bitten'], ride: ['rode', 'ridden'], rise: ['rose', 'risen'],
  shake: ['shook', 'shaken'], steal: ['stole', 'stolen'], freeze: ['froze', 'frozen'],
  cling: ['clung'], fling: ['flung'], sling: ['slung'], wring: ['wrung'], swing: ['swung'],
  shrink: ['shrank', 'shrunk'], sink: ['sank', 'sunk'], stink: ['stank', 'stunk'],
  spring: ['sprang', 'sprung'], swear: ['swore', 'sworn'], tread: ['trod', 'trodden'],
  lay: ['laid'], slay: ['slew', 'slain'], flee: ['fled'], weave: ['wove', 'woven'],
  weep: ['wept'], sweep: ['swept'], creep: ['crept'], kneel: ['knelt'], deal: ['dealt'],
  cast: ['cast'], cost: ['cost'], burst: ['burst'], thrust: ['thrust'], shed: ['shed'],
  spin: ['spun'], cut: ['cut'], put: ['put'], set: ['set'], hurt: ['hurt'], quit: ['quit'],
  forget: ['forgot', 'forgotten'], forgive: ['forgave', 'forgiven'], forbid: ['forbade'],
  arise: ['arose', 'arisen'], awake: ['awoke', 'awoken'], bear: ['bore', 'borne'],
  beat: ['beat', 'beaten'], bind: ['bound'], swell: ['swollen'],
  strive: ['strove', 'striven'], thrive: ['throve', 'thriven'], wake: ['woke', 'woken'],
  withdraw: ['withdrew', 'withdrawn'], overcome: ['overcame'], undergo: ['underwent'],
};
// Prefixed English verbs inherit their base verb's irregular forms
// (unfreeze -> unfroze, rewrite -> rewrote, outgrow -> outgrew).
const EN_PREFIXES = ['un', 're', 'over', 'under', 'out', 'mis', 'pre', 'dis', 'fore', 'with', 'up'];
// A German separable verb is written apart in a main clause -- "hervorheben"
// surfaces as "hebt ... hervor", so neither a token nor a prefix match can
// ever reach it.
const DE_SEP = [
  'auseinander', 'entgegen', 'gegenüber', 'zusammen', 'zurecht', 'zurück', 'herunter',
  'herüber', 'herauf', 'heraus', 'herein', 'hervor', 'herbei', 'hinauf', 'hinaus',
  'hinein', 'hinüber', 'hinunter', 'voraus', 'vorbei', 'vorher', 'weiter', 'wieder',
  'davon', 'dazu', 'durch', 'empor', 'fest', 'fort', 'frei', 'gegen', 'gleich', 'heim',
  'hoch', 'nach', 'statt', 'über', 'unter', 'voll', 'vorwärts', 'weg', 'zurück',
  'ab', 'an', 'auf', 'aus', 'bei', 'da', 'ein', 'her', 'hin', 'los', 'mit', 'nieder',
  'um', 'vor', 'zu',
];
const DE_SEP_SORTED = DE_SEP.slice().sort((a, b) => b.length - a.length);
const VOWELS = new Set(['a', 'e', 'i', 'o', 'u', 'ä', 'ö', 'ü']);

// Strong verbs change their stem vowel: gelingen/gelungen, überwinden/überwunden.
// Same length, same frame, vowels-only difference -- tight enough not to pair
// unrelated words.
function ablaut(a, b) {
  if (a.length !== b.length || a.length < 5) return false;
  if (a.slice(0, 2) !== b.slice(0, 2) || a.slice(-3) !== b.slice(-3)) return false;
  let diff = 0;
  for (let i = 0; i < a.length; i++) {
    if (a[i] === b[i]) continue;
    if (!VOWELS.has(a[i]) || !VOWELS.has(b[i])) return false;
    if (++diff > 2) return false;
  }
  return diff > 0;
}

// "hingefallen" / "einzuschätzen" -> "hinfallen" / "einschätzen"
function rejoin(tok) {
  const out = [];
  for (const p of DE_SEP_SORTED) {
    if (!tok.startsWith(p)) continue;
    const rest = tok.slice(p.length);
    if (rest.startsWith('ge') || rest.startsWith('zu')) out.push(p + rest.slice(2));
  }
  return out;
}

function germanHas(w, toks) {
  const stripped = toks.map(t => (t.startsWith('ge') && t.length > 4 ? t.slice(2) : t));
  for (const t of toks.concat(stripped)) if (ablaut(w, t)) return true;
  for (const t of toks) for (const r of rejoin(t)) if (r === w || ablaut(w, r)) return true;
  for (const p of DE_SEP_SORTED) {
    if (!w.startsWith(p) || w.length - p.length < 4) continue;
    if (!toks.includes(p)) continue;
    const stem = w.slice(p.length).slice(0, 3);
    for (const t of stripped) if (t.startsWith(stem)) return true;
  }
  return false;
}
// Inflection-tolerant containment. A learner's example legitimately shows an
// inflected form (boot->boots, carry->carried, become->became, day->days), so a
// plain token match produces ~90% false positives on this corpus.
function fuzzyHas(word, text, lang) {
  // "absent-minded" illustrates "absentminded" and "béchamel" illustrates
  // "bechamel"; compare without joiners or diacritics.
  const flat = s => s.toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/[-'’\u00ad]/g, '');
  const w = word.toLowerCase();
  const lowText = (text || '').toLowerCase();
  // literal substring covers hyphen/apostrophe headwords (t-shirt, o'clock)
  // and compounds that the tokenizer would split.
  if (w.length >= 3 && lowText.includes(w)) return true;
  if (w.length >= 5 && flat(lowText).includes(flat(w))) return true;
  const toks = tokens(text);
  if (toks.includes(w)) return true;
  for (const f of IRREGULAR[w] || []) if (toks.includes(f)) return true;
  for (const p of EN_PREFIXES) {
    if (!w.startsWith(p)) continue;
    for (const f of IRREGULAR[w.slice(p.length)] || []) if (toks.includes(p + f)) return true;
  }
  if (lang === 'de' && germanHas(w, toks)) return true;
  const fw = flat(w);
  for (const t of toks) {
    const ft = flat(t);
    if (ft === fw) return true;
    if (ft.startsWith(fw) && ft.length - fw.length <= 3) return true;
    if (fw.startsWith(ft) && fw.length - ft.length <= 3 && ft.length >= 4) return true;
  }
  // stem-prefix threshold: never demand more characters than the word has
  const need = Math.min(w.length, Math.max(3, Math.ceil(w.length * 0.6)));
  for (const t of toks) {
    // regular inflection: example token simply extends the headword (boots, carried)
    if (t.startsWith(w) && t.length - w.length <= 4) return true;
    // headword itself is the longer form (studies -> study)
    if (w.startsWith(t) && w.length - t.length <= 3 && t.length >= 4) return true;
    if (Math.abs(t.length - w.length) > 5) continue;
    let i = 0;
    while (i < t.length && i < w.length && t[i] === w[i]) i++;
    if (i >= need) return true;
  }
  return false;
}

// ------------------------------------------------------------------- audit
const argv = process.argv.slice(2);
const wantSamples = argv.includes('--samples');
const ruleFilter = argv.includes('--rule') ? argv[argv.indexOf('--rule') + 1] : null;
const langFilter = argv.includes('--lang') ? argv[argv.indexOf('--lang') + 1] : null;
const jsonOut = argv.includes('--json') ? argv[argv.indexOf('--json') + 1] : null;
const SAMPLE_MAX = 400;

const byRule = {};          // rule -> count
const byRuleFile = {};      // rule -> file -> count
const samples = {};         // rule -> [strings]
const perFile = {};         // file -> {entries, defects}
const langWords = {};       // lang -> word -> Set(levels)

let CUR = null;   // { file, dirty:Set } for the entry currently being checked
function flag(rule, file, msg) {
  byRule[rule] = (byRule[rule] || 0) + 1;
  (byRuleFile[rule] = byRuleFile[rule] || {})[file] = (byRuleFile[rule]?.[file] || 0) + 1;
  perFile[file].defects++;
  if (CUR && CUR.file === file) CUR.dirty.add(CUR.w);
  if (!samples[rule]) samples[rule] = [];
  if (samples[rule].length < SAMPLE_MAX) samples[rule].push(msg);
}

const loaded = [];
for (const [lang, level, file, glob, sep] of FILES) {
  if (langFilter && lang !== langFilter) continue;
  const p = path.join(ROOT, 'data', file);
  if (!fs.existsSync(p)) { console.error(`MISSING ${file}`); continue; }
  require(p);
  const arr = global.window[glob];
  if (!Array.isArray(arr)) { console.error(`NO GLOBAL ${glob} in ${file}`); continue; }
  loaded.push({ lang, level, file, arr, sep });
  perFile[file] = { lang, level, entries: arr.length, defects: 0, dirtyEntries: 0 };
}

// cross-level index (CEFR levels only, per language)
for (const { lang, level, arr } of loaded) {
  if (!/^[ABC][12]$/.test(level)) continue;
  const m = (langWords[lang] = langWords[lang] || new Map());
  for (const e of arr) {
    const k = String(e.word || '').toLowerCase();
    if (!k) continue;
    if (!m.has(k)) m.set(k, new Set());
    m.get(k).add(level);
  }
}

for (const { lang, level, file, arr, sep } of loaded) {
  const seen = new Map();
  const seenCase = new Map();
  const defCount = new Map();
  const exCount = new Map();
  const isWordsFile = /^words/.test(file);
  CUR = { file, i: -1, w: '', dirty: new Set() };

  for (const e of arr) {
    CUR.i++;
    const raw = e.word;
    const word = String(raw == null ? '' : raw);
    CUR.w = word;
    const where = `${file}: "${word}"`;

    // ---- headword
    if (!word.trim()) { flag('W_EMPTY', file, where); continue; }
    if (word !== word.trim()) flag('W_SPACE', file, `${where} (untrimmed)`);
    if (JUNKWORD.test(word)) flag('W_JUNK', file, where);
    if (word.length > 25) flag('W_LONG', file, `${where} (${word.length} chars)`);
    // Multi-word headwords are legitimate here ("free trade", "Guten Morgen",
    // and de/fr/it/es/pt store the article: "der Hund", "le chat"). Only 4+
    // words indicates a definition fragment leaked into the headword field.
    if (SINGLE_WORD_SET(level) && word.trim().split(/\s+/).length >= 4) flag('W_MULTIWORD', file, where);
    if (SCRIPT_OK[lang] && !SCRIPT_OK[lang].test(word)) flag('W_FOREIGN', file, where);
    if (MOJIBAKE.test(word) || MOJIBAKE.test(String(e.definition || '')) || MOJIBAKE.test(String(e.example || '')))
      flag('W_MOJIBAKE', file, `${where} | ${String(e.definition || '').slice(0, 60)}`);

    const lc = word.toLowerCase();
    if (seen.has(word)) flag('W_DUPE', file, where); else seen.set(word, 1);
    // German legitimately contrasts a capitalised noun with its lowercase verb
    // (Ansehen/ansehen), so case-only pairs are only a defect outside German.
    if (lang !== 'de' && seenCase.has(lc) && seenCase.get(lc) !== word) flag('W_DUPE_CASE', file, `${where} vs "${seenCase.get(lc)}"`);
    else seenCase.set(lc, word);

    if (/^[ABC][12]$/.test(level)) {
      const lv = langWords[lang]?.get(lc);
      if (lv && lv.size > 1) flag('W_CROSSLEVEL', file, `${lang} "${word}" taught at ${[...lv].sort().join(' + ')}`);
    }

    // ---- metadata
    if (isWordsFile && !String(e.pos || '').trim()) flag('M_NOPOS', file, where);
    if (!String(e.category || '').trim()) flag('M_NOCAT', file, where);

    // ---- definition
    const d = String(e.definition == null ? '' : e.definition);
    const dp = splitPair(d, sep);
    if (dp.count === 0) flag('D_NOSEP', file, `${where} -> ${d.slice(0, 90)}`);
    else if (dp.count > 1) flag('D_MULTISEP', file, `${where} -> ${d.slice(0, 90)}`);
    if (dp.count >= 1 && (!dp.n || !dp.t)) flag('D_EMPTYHALF', file, `${where} -> ${d.slice(0, 90)}`);
    if (FALLBACK.test(d)) flag('D_FALLBACK', file, `${where} -> ${d.slice(0, 90)}`);
    if (SYNLIST.test(dp.n) || SYNLIST.test(dp.t)) flag('D_SYNLIST', file, `${where} -> ${d.slice(0, 90)}`);
    if (FORMOF.test(dp.n)) flag('D_FORMOF', file, `${where} -> ${dp.n.slice(0, 90)}`);
    if (REGISTER.test(dp.n)) flag('D_REGISTER', file, `${where} [${level}] -> ${dp.n.slice(0, 90)}`);
    if (MARKUP.test(d)) flag('D_MARKUP', file, `${where} -> ${d.slice(0, 90)}`);
    if (dp.n.length > 300) flag('D_HUGE', file, `${where} (${dp.n.length}) -> ${dp.n.slice(0, 90)}`);
    else if (dp.n.length > 160) flag('D_TOOLONG', file, `${where} (${dp.n.length}) -> ${dp.n.slice(0, 90)}`);
    if (dp.n && dp.n.replace(/[^\p{L}]/gu, '').length < 2) flag('D_TOOSHORT', file, `${where} -> ${d.slice(0, 60)}`);
    if (dp.n && dp.n === dp.t) flag('D_UNTRANSLATED', file, `${where} -> ${dp.n.slice(0, 80)}`);
    // Circular only matters when the definition is too short to say anything
    // else -- "A container made of glass" is fine, "To try to catch fish" is not.
    if (SINGLE_WORD_SET(level) && !/\s/.test(word) && word.length > 3
        && tokens(dp.n).includes(lc) && tokens(dp.n).length <= 6)
      flag('D_CIRCULAR', file, `${where} -> ${dp.n.slice(0, 90)}`);
    if (dp.n) {
      if (!defCount.has(dp.n)) defCount.set(dp.n, []);
      defCount.get(dp.n).push(word);
    }

    // ---- example
    const x = String(e.example == null ? '' : e.example);
    const xp = splitPair(x, sep);
    if (xp.count === 0) flag('E_NOSEP', file, `${where} -> ${x.slice(0, 90)}`);
    else if (xp.count > 1) flag('E_MULTISEP', file, `${where} -> ${x.slice(0, 90)}`);
    if (xp.count >= 1 && (!xp.n || !xp.t)) flag('E_EMPTYHALF', file, `${where} -> ${x.slice(0, 90)}`);
    if (FALLBACK.test(x)) flag('E_FALLBACK', file, `${where}`);
    if (MARKUP.test(x)) flag('E_MARKUP', file, `${where} -> ${x.slice(0, 90)}`);
    if (xp.n.length > 200) flag('E_TOOLONG', file, `${where} (${xp.n.length})`);
    if (xp.n && xp.n.length < 10) flag('E_TOOSHORT', file, `${where} -> ${x.slice(0, 60)}`);
    if (xp.n && xp.n === xp.t) flag('E_UNTRANSLATED', file, `${where} -> ${xp.n.slice(0, 80)}`);
    if (dp.n && dp.n === xp.n) flag('D_SAME_AS_EX', file, `${where} -> ${dp.n.slice(0, 80)}`);
    const bare = stripArticle(word, lang);
    if (xp.n && !FALLBACK.test(x) && !/\s/.test(bare) && !fuzzyHas(bare, xp.n, lang))
      flag('E_NOWORD', file, `${where} -> ${xp.n.slice(0, 90)}`);
    if (xp.n && !FALLBACK.test(x)) {
      if (!exCount.has(xp.n)) exCount.set(xp.n, []);
      exCount.get(xp.n).push(word);
    }
  }

  const groupMsg = (txt, ws) => `${file}: x${ws.length} [${ws.slice(0, 6).join(', ')}${ws.length > 6 ? ', …' : ''}] -> ${txt.slice(0, 70)}`;  for (const [txt, ws] of defCount)
    if (ws.length >= 5) { flag('D_SHARED', file, groupMsg(txt, ws)); ws.forEach(w => CUR.dirty.add(w)); for (let i = 1; i < ws.length; i++) { byRule.D_SHARED++; byRuleFile.D_SHARED[file]++; perFile[file].defects++; } }
  for (const [txt, ws] of exCount)
    if (ws.length >= 5) { flag('E_SHARED', file, groupMsg(txt, ws)); ws.forEach(w => CUR.dirty.add(w)); for (let i = 1; i < ws.length; i++) { byRule.E_SHARED++; byRuleFile.E_SHARED[file]++; perFile[file].defects++; } }
  perFile[file].dirtyEntries = CUR.dirty.size;
  CUR = null;
}

// ----------------------------------------------------------------- reporting
const totalEntries = Object.values(perFile).reduce((s, v) => s + v.entries, 0);
console.log(`\nScanned ${Object.keys(perFile).length} files, ${totalEntries.toLocaleString()} entries\n`);

if (ruleFilter) {
  const per = byRuleFile[ruleFilter] || {};
  console.log(`== ${ruleFilter}  (${RULES[ruleFilter]})   total ${byRule[ruleFilter] || 0}`);
  Object.entries(per).sort((a, b) => b[1] - a[1]).forEach(([f, n]) => console.log(`   ${String(n).padStart(7)}  ${f}`));
  (samples[ruleFilter] || []).forEach(s => console.log(`      · ${s}`));
} else {
  const order = Object.keys(RULES).filter(r => byRule[r]).sort((a, b) => byRule[b] - byRule[a]);
  console.log('RULE            COUNT   %ENTRIES  DESCRIPTION');
  for (const r of order) {
    const pct = (100 * byRule[r] / totalEntries).toFixed(2);
    console.log(`${r.padEnd(15)}${String(byRule[r]).padStart(7)}   ${pct.padStart(6)}%  ${RULES[r]}`);
  }
  const clean = Object.keys(RULES).filter(r => !byRule[r]);
  if (clean.length) console.log(`\nclean (0 hits): ${clean.join(', ')}`);

  console.log('\n== entries with at least one defect (worst 20 files)');
  Object.entries(perFile).sort((a, b) => b[1].dirtyEntries - a[1].dirtyEntries).slice(0, 20)
    .forEach(([f, v]) => console.log(
      `   ${String(v.dirtyEntries).padStart(6)} / ${String(v.entries).padStart(6)} entries` +
      `  (${(100 * v.dirtyEntries / Math.max(v.entries, 1)).toFixed(0).padStart(3)}%)  ${v.lang} ${v.level.padEnd(5)} ${f}`));

  console.log('\n== entries with at least one defect, per language');
  const byLang = {};
  for (const v of Object.values(perFile)) {
    byLang[v.lang] = byLang[v.lang] || { e: 0, d: 0 };
    byLang[v.lang].e += v.entries; byLang[v.lang].d += v.dirtyEntries;
  }
  let ge = 0, gd = 0;
  Object.entries(byLang).sort((a, b) => b[1].d / b[1].e - a[1].d / a[1].e)
    .forEach(([l, v]) => { ge += v.e; gd += v.d; console.log(`   ${l}  ${String(v.d).padStart(7)} / ${String(v.e).padStart(7)}  (${(100 * v.d / v.e).toFixed(0)}%)`); });
  console.log(`   ALL ${String(gd).padStart(8)} / ${String(ge).padStart(7)}  (${(100 * gd / ge).toFixed(1)}%)`);

  if (wantSamples) {
    console.log('\n== samples');
    for (const r of order) {
      console.log(`\n-- ${r}  (${RULES[r]})`);
      (samples[r] || []).slice(0, 6).forEach(s => console.log(`   · ${s}`));
    }
  }
}

if (jsonOut) {
  fs.writeFileSync(path.join(ROOT, jsonOut),
    JSON.stringify({ totalEntries, byRule, byRuleFile, perFile, samples }, null, 1));
  console.log(`\nwrote ${jsonOut}`);
}
