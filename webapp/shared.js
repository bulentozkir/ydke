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

/* Top Words — shared core.
 * Loaded on every page, in this order:
 *   1) data/words*.js          (defines window.WORDS_*)
 *   2) shared.js               (this file — state, config, storage, shared nav)
 *   3) <mode>.js                (page-specific logic, registers via onLevelChange)
 *   4) <script>startApp();</script>   (final inline kickoff at end of <body>)
 *
 * This file intentionally has NO IIFE wrapper: its `var`/`function`
 * declarations are plain globals so every other script on the page (loaded
 * after it) can use them directly, the same way modules on one page used to
 * share state inside the old single-file app.js.
 */
"use strict";

/* ================= language configurations ================= */
var LANGS = {
  en: {
    label: "English",
    title: "English - Top Words",
    description:
      "YDS, YÖKDİL, ÜDS, UDSP ve TOEFL sınavları için İngilizce kelime ezberleme: A1–C2 en sık kullanılan kelimeler ve phrasal verbs (öbek fiiller). Türk öğrenciler için ücretsiz flashcard ve testler.",
    tagline: "İngilizce kelime ezberleme",
    defaultLevel: "B2",
    speakLang: "en-US",
    levels: ["PV", "A1", "A2", "B1", "B2", "C1", "C2", "TOEFL"],
    // English data used to be loaded upfront via static <script> tags in
    // every page's HTML. It no longer is: `files` maps each level to the
    // single data file that defines its global, and only the level the user
    // is actually studying is fetched (see ensureLevelData()).
    sets: {
      PV: "PHRASAL_VERBS_EN",
      A1: "WORDS_A1",
      A2: "WORDS_A2",
      B1: "WORDS_B1",
      B2: "WORDS_B2",
      C1: "WORDS_C1",
      C2: "WORDS_C2",
      TOEFL: "WORDS_TOEFL",
    },
    files: {
      PV: "data/phrasalverbsen.js",
      A1: "data/wordsa1.js",
      A2: "data/wordsa2.js",
      B1: "data/wordsb1.js",
      B2: "data/wordsb2.js",
      C1: "data/wordsc1.js",
      C2: "data/wordsc2.js",
      TOEFL: "data/toefl.js",
    },
    detailsUrl: function (word) {
      return (
        "https://dictionary.cambridge.org/tr/s%C3%B6zl%C3%BCk/ingilizce-t%C3%BCrk%C3%A7e/" +
        encodeURIComponent(word)
      );
    },
    examplesUrl: function (word) {
      return (
        "https://dictionary.cambridge.org/tr/s%C3%B6zl%C3%BCk/ingilizce/" +
        encodeURIComponent(word)
      );
    },
  },
  de: {
    label: "German",
    title: "German - Top Words",
    description:
      "Goethe Institute, Telc ve CEFR sınavları için Almanca kelime ezberleme: A1–C2 en sık kullanılan kelimeler ve ayrılabilen fiiller (Partikelverben). Türk öğrenciler için ücretsiz flashcard ve testler.",
    tagline: "Almanca kelime ezberleme",
    defaultLevel: "GA1",
    speakLang: "de-DE",
    levels: ["PART", "GA1", "GA2", "GB1", "GB2", "GC1", "GC2"],
    // German data is fetched lazily, ONE LEVEL AT A TIME, the first time the
    // user actually opens that level (see ensureLevelData()). `sets` names
    // the global each level's data lives in once loaded; `files` says which
    // file defines it.
    sets: {
      PART: "PARTIKELVERB_DE",
      GA1: "WORDS_GODE_A1",
      GA2: "WORDS_GODE_A2",
      GB1: "WORDS_GODE_B1",
      GB2: "WORDS_GODE_B2",
      GC1: "WORDS_GODE_C1",
      GC2: "WORDS_GODE_C2",
    },
    files: {
      PART: "data/partikelverbde.js",
      GA1: "data/wordsa1gode.js",
      GA2: "data/wordsa2gode.js",
      GB1: "data/wordsb1gode.js",
      GB2: "data/wordsb2gode.js",
      GC1: "data/wordsc1gode.js",
      GC2: "data/wordsc2gode.js",
    },
    detailsUrl: function (word) {
      var bare = word.replace(/^(der|die|das)\s+/i, "");
      return "https://en.pons.com/translate/german-turkish?q=" + encodeURIComponent(bare);
    },
    examplesUrl: function (word) {
      var bare = word.replace(/^(der|die|das)\s+/i, "");
      return (
        "https://dictionary.cambridge.org/dictionary/german-english/" +
        encodeURIComponent(bare)
      );
    },
  },
  fr: {
    label: "French",
    title: "French - Top Words",
    description:
      "DELF, DALF ve CEFR sınavları için Fransızca kelime ezberleme: A1–C1 en sık kullanılan kelimeler ve deyimler. Türk öğrenciler için ücretsiz flashcard ve testler.",
    tagline: "Fransızca kelime ezberleme",
    defaultLevel: "A1",
    speakLang: "fr-FR",
    levels: ["A1", "A2", "B1", "B2", "C1", "C2"],
    // French data is fetched lazily one level at a time -- see the German
    // `sets`/`files` comment above for the full rationale.
    sets: {
      A1: "WORDS_FR_A1",
      A2: "WORDS_FR_A2",
      B1: "WORDS_FR_B1",
      B2: "WORDS_FR_B2",
      C1: "WORDS_FR_C1",
      C2: "WORDS_FR_C2",
    },
    files: {
      A1: "data/wordsa1fr.js",
      A2: "data/wordsa2fr.js",
      B1: "data/wordsb1fr.js",
      B2: "data/wordsb2fr.js",
      C1: "data/wordsc1fr.js",
      C2: "data/wordsc2fr.js",
    },
    detailsUrl: function (word) {
      var bare = word.replace(/^(le|la|les)\s+/i, "").replace(/^l['’]/i, "");
      return (
        "https://www.collinsdictionary.com/dictionary/french-english/" +
        encodeURIComponent(bare)
      );
    },
    examplesUrl: function (word) {
      var bare = word.replace(/^(le|la|les)\s+/i, "").replace(/^l['’]/i, "");
      return (
        "https://context.reverso.net/%C3%A7eviri/frans%C4%B1zca-t%C3%BCrk%C3%A7e/" +
        encodeURIComponent(bare)
      );
    },
  },
  it: {
    label: "Italian",
    title: "Italian - Top Words",
    description:
      "CILS, CELI ve CEFR sınavları için İtalyanca kelime ezberleme: A1–C2 en sık kullanılan kelimeler. Türk öğrenciler için ücretsiz flashcard ve testler.",
    tagline: "İtalyanca kelime ezberleme",
    defaultLevel: "A1",
    speakLang: "it-IT",
    levels: ["A1", "A2", "B1", "B2", "C1", "C2"],
    // Italian data is fetched lazily one level at a time -- see the German
    // `sets`/`files` comment above for the full rationale.
    sets: {
      A1: "WORDS_IT_A1",
      A2: "WORDS_IT_A2",
      B1: "WORDS_IT_B1",
      B2: "WORDS_IT_B2",
      C1: "WORDS_IT_C1",
      C2: "WORDS_IT_C2",
    },
    files: {
      A1: "data/wordsa1it.js",
      A2: "data/wordsa2it.js",
      B1: "data/wordsb1it.js",
      B2: "data/wordsb2it.js",
      C1: "data/wordsc1it.js",
      C2: "data/wordsc2it.js",
    },
    detailsUrl: function (word) {
      var bare = word.replace(/^(il|lo|la|i|gli|le)\s+/i, "").replace(/^l['’]/i, "");
      return (
        "https://www.collinsdictionary.com/dictionary/italian-english/" +
        encodeURIComponent(bare)
      );
    },
    examplesUrl: function (word) {
      var bare = word.replace(/^(il|lo|la|i|gli|le)\s+/i, "").replace(/^l['’]/i, "");
      return (
        "https://context.reverso.net/traduzione/italiano-turco/" +
        encodeURIComponent(bare)
      );
    },
  },
  es: {
    label: "Spanish",
    title: "Spanish - Top Words",
    description:
      "DELE, SIELE ve CEFR sınavları için İspanyolca kelime ezberleme: A1–C2 en sık kullanılan kelimeler. Türk öğrenciler için ücretsiz flashcard ve testler.",
    tagline: "İspanyolca kelime ezberleme",
    defaultLevel: "A1",
    speakLang: "es-ES",
    levels: ["A1", "A2", "B1", "B2", "C1", "C2"],
    // Spanish data is fetched lazily one level at a time -- see the German
    // `sets`/`files` comment above for the full rationale.
    sets: {
      A1: "WORDS_ES_A1",
      A2: "WORDS_ES_A2",
      B1: "WORDS_ES_B1",
      B2: "WORDS_ES_B2",
      C1: "WORDS_ES_C1",
      C2: "WORDS_ES_C2",
    },
    files: {
      A1: "data/wordsa1es.js",
      A2: "data/wordsa2es.js",
      B1: "data/wordsb1es.js",
      B2: "data/wordsb2es.js",
      C1: "data/wordsc1es.js",
      C2: "data/wordsc2es.js",
    },
    detailsUrl: function (word) {
      var bare = word.replace(/^(el|la|los|las)\s+/i, "");
      return (
        "https://www.collinsdictionary.com/dictionary/spanish-english/" +
        encodeURIComponent(bare)
      );
    },
    examplesUrl: function (word) {
      var bare = word.replace(/^(el|la|los|las)\s+/i, "");
      return (
        "https://context.reverso.net/%C3%A7eviri/ispanyolca-t%C3%BCrk%C3%A7e/" +
        encodeURIComponent(bare)
      );
    },
  },
  pt: {
    label: "Portuguese",
    title: "Portuguese - Top Words",
    description:
      "CIPLE, CAPLE ve CEFR sınavları için Portekizce kelime ezberleme: A1–C2 en sık kullanılan kelimeler. Türk öğrenciler için ücretsiz flashcard ve testler.",
    tagline: "Portekizce kelime ezberleme",
    defaultLevel: "A1",
    speakLang: "pt-BR",
    levels: ["A1", "A2", "B1", "B2", "C1", "C2"],
    // Portuguese data is fetched lazily one level at a time -- see the German
    // `sets`/`files` comment above for the full rationale.
    sets: {
      A1: "WORDS_PT_A1",
      A2: "WORDS_PT_A2",
      B1: "WORDS_PT_B1",
      B2: "WORDS_PT_B2",
      C1: "WORDS_PT_C1",
      C2: "WORDS_PT_C2",
    },
    files: {
      A1: "data/wordsa1pt.js",
      A2: "data/wordsa2pt.js",
      B1: "data/wordsb1pt.js",
      B2: "data/wordsb2pt.js",
      C1: "data/wordsc1pt.js",
      C2: "data/wordsc2pt.js",
    },
    detailsUrl: function (word) {
      // Collins has no Portuguese-English dictionary; Cambridge does and was
      // verified to return real entries for these headwords.
      var bare = word.replace(/^(o|a|os|as)\s+/i, "");
      return (
        "https://dictionary.cambridge.org/dictionary/portuguese-english/" +
        encodeURIComponent(bare)
      );
    },
    examplesUrl: function (word) {
      var bare = word.replace(/^(o|a|os|as)\s+/i, "");
      return (
        "https://context.reverso.net/%C3%A7eviri/portekizce-t%C3%BCrk%C3%A7e/" +
        encodeURIComponent(bare)
      );
    },
  },
  nl: {
    label: "Dutch",
    title: "Dutch - Top Words",
    description:
      "NT2, CNaVT ve CEFR sınavları için Hollandaca kelime ezberleme: A1–C2 en sık kullanılan kelimeler. Türk öğrenciler için ücretsiz flashcard ve testler.",
    tagline: "Hollandaca kelime ezberleme",
    defaultLevel: "A1",
    speakLang: "nl-NL",
    levels: ["A1", "A2", "B1", "B2", "C1", "C2"],
    // Dutch data is fetched lazily one level at a time -- see the German
    // `sets`/`files` comment above for the full rationale.
    sets: {
      A1: "WORDS_NL_A1",
      A2: "WORDS_NL_A2",
      B1: "WORDS_NL_B1",
      B2: "WORDS_NL_B2",
      C1: "WORDS_NL_C1",
      C2: "WORDS_NL_C2",
    },
    files: {
      A1: "data/wordsa1nl.js",
      A2: "data/wordsa2nl.js",
      B1: "data/wordsb1nl.js",
      B2: "data/wordsb2nl.js",
      C1: "data/wordsc1nl.js",
      C2: "data/wordsc2nl.js",
    },
    detailsUrl: function (word) {
      // Collins has no Dutch-English dictionary; Cambridge does and was
      // verified to return real entries for these headwords.
      var bare = word.replace(/^(de|het)\s+/i, "");
      return (
        "https://dictionary.cambridge.org/dictionary/dutch-english/" +
        encodeURIComponent(bare)
      );
    },
    examplesUrl: function (word) {
      var bare = word.replace(/^(de|het)\s+/i, "");
      return (
        "https://context.reverso.net/%C3%A7eviri/hollandaca-t%C3%BCrk%C3%A7e/" +
        encodeURIComponent(bare)
      );
    },
  },
};
var LANG_ORDER = ["en", "de", "fr", "it", "es", "pt", "nl"];

// Semantic/topical domain a word's MEANING belongs to (Sports, Agriculture,
// Literature, Technology, etc.) -- completely independent of language/CEFR
// level/which file it came from. Every word entry across data/*.js now
// carries a `category` field with one of these values (assigned by
// scripts/classify_word_categories.py, a best-effort keyword-matching
// heuristic against each entry's English definition text -- see that
// script's docstring + repo memory for the full method and known
// limitations). Keep this list in sync with that script's DOMAIN_ORDER if
// either ever changes. "Mix" is NOT a real domain -- it's the default,
// meaning "no category filter, show every domain" (see setCategory()).
var CATEGORIES = [
  "Mix",
  "Agriculture",
  "Animals",
  "Arts",
  "Business",
  "Clothing",
  "Communication",
  "Construction",
  "Education",
  "Emotions",
  "Family",
  "Food",
  "Forestry",
  "Geography",
  "Government",
  "Household",
  "Law",
  "Literature",
  "Medicine",
  "Military",
  "Nature",
  "Religion",
  "Science",
  "Sports",
  "Technology",
  "Transportation",
  "Travel",
  "Weather",
  "General",
];

function buildWordSets(lang) {
  var cfg = LANGS[lang];
  var sets = {};
  cfg.levels.forEach(function (l) {
    var raw = window[cfg.sets[l]] || [];
    // `currentCategory` filters every level's word list down to just that
    // domain -- "Mix" means no filtering at all. Always rebuilt from the
    // ORIGINAL raw arrays (never filters an already-filtered array), so
    // switching Category back to "Mix", or to a different domain, always
    // starts from the full data again.
    sets[l] =
      currentCategory !== "Mix"
        ? raw.filter(function (w) {
            return w.category === currentCategory;
          })
        : raw.slice();
  });
  sets.MIX = cfg.levels.reduce(function (acc, l) {
    return acc.concat(sets[l]);
  }, []);
  return sets;
}

/* ---------- data loading: one FILE per (language, level), on demand ----------
 *
 * Every page used to hard-code eight <script defer src="data/*.js"> tags for
 * the full English corpus (16.5 MB) before shared.js could even run, and
 * ensureLangData() then fetched a whole other language in one go. Both were
 * far more than any single screen needs: a learner studying English B2 only
 * ever reads WORDS_B2 (968 KB).
 *
 * Now nothing is in the HTML and the unit of loading is a single level's
 * file. loadDataFiles() de-duplicates by src, queues concurrent callers for
 * an in-flight file instead of re-fetching it, and -- important for callers
 * that expect synchronous behaviour -- invokes `callback` immediately when
 * every requested file is already present. A file that fails to load counts
 * as "done" so one bad response can't wedge the app; the level simply ends up
 * empty and each page's existing "not enough words" messaging takes over.
 */
var dataFileState = {}; // src -> "loaded" | "loading"
var dataFileWaiters = {}; // src -> [callback]
var DATA_FILE_TIMEOUT_MS = 15000;

function loadDataFile(src, done) {
  if (dataFileState[src] === "loaded") {
    done();
    return;
  }
  if (dataFileState[src] === "loading") {
    dataFileWaiters[src].push(done);
    return;
  }
  dataFileState[src] = "loading";
  dataFileWaiters[src] = [done];
  var el = document.createElement("script");
  var settled = false;
  el.src = src;
  var timeout = setTimeout(finish, DATA_FILE_TIMEOUT_MS);

  function finish() {
    if (settled) return;
    settled = true;
    clearTimeout(timeout);
    el.onload = null;
    el.onerror = null;
    if (el.parentNode) el.parentNode.removeChild(el);
    dataFileState[src] = "loaded";
    var waiters = dataFileWaiters[src];
    dataFileWaiters[src] = null;
    waiters.forEach(function (cb) {
      cb();
    });
  }

  el.onload = el.onerror = finish;
  document.head.appendChild(el);
}

function loadDataFiles(srcs, callback) {
  var pending = srcs.filter(function (s) {
    return s && dataFileState[s] !== "loaded";
  });
  if (!pending.length) {
    callback();
    return;
  }
  var remaining = pending.length;
  pending.forEach(function (src) {
    loadDataFile(src, function () {
      remaining--;
      if (remaining <= 0) callback();
    });
  });
}

// Which data files back a given (language, level). "MIX" is the one level
// that genuinely spans the whole language, so it -- and only it -- pulls
// every file for that language.
function filesForLevel(lang, level) {
  var cfg = LANGS[lang];
  if (!cfg || !cfg.files) return [];
  if (!level || level === "MIX") {
    return cfg.levels
      .map(function (l) {
        return cfg.files[l];
      })
      .filter(Boolean);
  }
  return cfg.files[level] ? [cfg.files[level]] : [];
}

function ensureLevelData(lang, level, callback) {
  loadDataFiles(filesForLevel(lang, level), callback);
}

// Back-compat shim: loads an ENTIRE language. Only used where that is
// genuinely required (the idle prefetch below); prefer ensureLevelData().
function ensureLangData(lang, callback) {
  ensureLevelData(lang, "MIX", callback);
}

/* Page-scoped datasets that are per-language and only wanted on one page.
 * A page opts in with `data-extra="<group>"` on <body>; shared.js then loads
 * just the current language's file from that group. Word Morph's German
 * synonym set alone is 4.6 MB, so loading all three languages' worth on every
 * visit -- as the static tags did -- was the single largest cost on that page. */
var EXTRA_DATA = {
  synant: {
    en: ["data/synanten.js"],
    de: ["data/synantde.js"],
    fr: ["data/synantfr.js"],
  },
  reading: {
    en: ["data/readingcomp.js", "data/readingcompencefr.js"],
    de: ["data/readingcompde.js"],
  },
};

function extraGroup() {
  return document.body.getAttribute("data-extra") || "";
}

function ensureExtraData(lang, callback) {
  var group = EXTRA_DATA[extraGroup()];
  loadDataFiles((group && group[lang]) || [], callback);
}

/* Warm the rest of the current language in the background once the page is
 * interactive, so later Level switches feel instant without ever blocking
 * first paint. Skipped when the user has asked the browser to conserve data. */
var _prefetched = {};
function runWhenUiIdle(fn) {
  var observer = null;
  var listening = true;

  function ready() {
    return document.visibilityState === "visible" &&
      !(document.body && document.body.classList.contains("is-playing"));
  }
  function cleanup() {
    if (!listening) return;
    listening = false;
    document.removeEventListener("visibilitychange", schedule);
    if (observer) observer.disconnect();
  }
  function run() {
    if (!ready()) {
      runWhenUiIdle(fn);
      return;
    }
    fn();
  }
  function schedule() {
    if (!listening || !ready()) return;
    cleanup();
    if (window.requestIdleCallback) requestIdleCallback(run);
    else setTimeout(run, 1500);
  }

  document.addEventListener("visibilitychange", schedule);
  if (document.body && window.MutationObserver) {
    observer = new MutationObserver(schedule);
    observer.observe(document.body, { attributes: true, attributeFilter: ["class"] });
  }
  schedule();
}

function prefetchLangInBackground(lang) {
  if (_prefetched[lang]) return;
  _prefetched[lang] = true;
  try {
    var conn = navigator.connection;
    if (conn && (conn.saveData || /(^|-)2g$/.test(conn.effectiveType || ""))) return;
  } catch (e) {
    /* no Network Information API -- prefetch anyway */
  }
  var srcs = filesForLevel(lang, "MIX");
  var group = EXTRA_DATA[extraGroup()];
  if (group && group[lang]) srcs = srcs.concat(group[lang]);
  var i = 0;
  // One file per idle slice, so a 4 MB parse never lands in the middle of a
  // card flip. Each step re-queues itself only after the previous file lands.
  function step() {
    while (i < srcs.length && dataFileState[srcs[i]] === "loaded") i++;
    if (i >= srcs.length) return;
    var src = srcs[i++];
    runWhenUiIdle(function () {
      loadDataFile(src, step);
    });
  }
  step();
}

var currentLang = "en";
// Which semantic domain (see `CATEGORIES` above) the word pool is
// currently filtered to -- "Mix" (the default) means no filtering, every
// domain included. Declared BEFORE the first buildWordSets() call below
// so that call already sees the correct default instead of relying on
// hoisting semantics.
var currentCategory = "Mix";
var WORD_SETS = buildWordSets(currentLang);
var LEVELS = LANGS[currentLang].levels.slice();
var DEFAULT_LEVEL = LANGS[currentLang].defaultLevel;
var currentLevel = DEFAULT_LEVEL;
var currentMode = document.body.getAttribute("data-mode") || "flashcards";
var WORDS = WORD_SETS[currentLevel].slice();
var QUESTIONS_PER_EXAM = 20;
function storageKey() {
  return "udsp_best_scores_" + currentLang + "_" + currentLevel + "_v1";
}

/* ---------- persistent state ---------- */
var KNOWN_KEY = "udsp_known_v1";
var FAV_KEY = "udsp_fav_v1";
var SRS_KEY = "udsp_srs_v1";
var MISTAKE_KEY = "udsp_mistakes_v1";
var STATS_KEY = "udsp_stats_v1";
var STREAK_KEY = "udsp_streak_v1";
var RESUME_KEY = "udsp_resume_v2"; // v2: multi-page architecture (lang+level only)
var WELCOME_KEY = "udsp_welcomed_v1"; // set by home.js once the dashboard is shown
var SOURCE_KEY = "udsp_source_v1"; // which pool the study surfaces draw from

function lsGet(key, fallback) {
  try {
    var v = JSON.parse(localStorage.getItem(key));
    return v === null || v === undefined ? fallback : v;
  } catch (e) {
    return fallback;
  }
}
function lsSet(key, val) {
  try {
    localStorage.setItem(key, JSON.stringify(val));
  } catch (e) {
    /* ignore storage errors (private mode) */
  }
}

// First-time visitor -> Home dashboard: a brand-new visitor (no resume state
// saved yet AND never shown the home dashboard) is sent to the orientation
// dashboard home.html the first time they open the app -- it introduces the
// app (auto-playing intro tour), lets them pick a language, and shows
// streak/continue for returning users. Fires on EVERY page that loads
// shared.js (the index.html root plus all the study/game surfaces), so no
// matter which app page a new user lands on first, Home is their default
// landing page. home.html itself loads home.js (not shared.js), so there is
// no redirect loop; the info pages about.html/help.html (and the games/
// listening hubs) load moresheet.js instead and are intentionally never
// redirected. home.js sets WELCOME_KEY on load and startApp() saves a
// RESUME_KEY entry, so this fires at most once per browser -- afterwards
// every page shows its own content normally, with no redirect loop, and
// location.replace() adds no extra back-button history entry.
// Skipped entirely for known search-engine crawler user agents so each
// page's SEO-tuned content still gets indexed as-is -- crawlers don't
// persist localStorage across separate fetches, so without this guard every
// crawl would look "new" and get redirected.
(function () {
  if (lsGet(RESUME_KEY, null)) return;
  if (lsGet(WELCOME_KEY, 0)) return;
  var ua = navigator.userAgent || "";
  if (/bot|crawl|spider|slurp|facebookexternalhit|whatsapp|telegram/i.test(ua)) return;
  location.replace("home.html");
})();

// Returning visitor -> chosen default start page: respect the "Başlangıç
// sayfası · Start page" preference (set on profile.html, "udsp_start_page_v1")
// when the app is opened FRESH at its root (index.html / "/") -- e.g. a new
// tab or a bookmark to the bare domain. Without this, the preference only
// ever took effect via the "Continue" button on home.html, a page most
// returning visitors never see again once WELCOME_KEY is set (see the
// redirect above), so the setting silently appeared to do nothing.
// Guarded by sessionStorage (once per tab) rather than firing on every
// index.html load, so the persistent "🃏 Cards" bottom-nav link present on
// EVERY page (always labeled/expected to open Flashcards) keeps working
// normally for explicit in-app navigation for the rest of that tab's
// session -- only the first, "fresh" load honors the preference.
var START_PAGE_KEY = "udsp_start_page_v1";
var START_REDIRECTED_KEY = "udsp_start_redirected_v1"; // sessionStorage: once per tab
(function () {
  if (!lsGet(WELCOME_KEY, 0)) return; // first-ever visit still goes through home.html
  if (!/^\/(index\.html)?$/.test(location.pathname)) return; // root/index.html only
  try {
    if (sessionStorage.getItem(START_REDIRECTED_KEY)) return;
    sessionStorage.setItem(START_REDIRECTED_KEY, "1");
  } catch (e) {
    return; // private-mode storage failures -- skip silently, no redirect
  }
  var sp = lsGet(START_PAGE_KEY, "index.html");
  if (sp && sp !== "index.html") location.replace(sp);
})();

// Header profile icon (all 17 study/game pages) + "save your progress"
// nudge -- both keyed off PROFILE_LINKED_KEY, a plain localStorage flag set
// by profile.js the moment this device is ever seen signed in. No Firebase
// is loaded on these pages, so this is the only cross-page signal available
// (deliberately last-known, not real-time -- see repo notes for why).
var PROFILE_LINKED_KEY = "udsp_profile_linked_v1";
var PROFILE_REMIND_KEY = "udsp_profile_remind_v1"; // last calendar date shown
(function () {
  var link = document.getElementById("profile-icon-link");
  if (link && lsGet(PROFILE_LINKED_KEY, 0)) link.classList.add("is-linked");
})();
// Called from the "section/set complete" screens in quiz.js, wordmorph.js
// and readingcomprehension.js. No-ops once a profile is linked, and shows at
// most once per calendar day even before that, so it nudges without nagging.
function maybeRemindProfile() {
  if (lsGet(PROFILE_LINKED_KEY, 0)) return;
  var today = new Date().toISOString().slice(0, 10);
  if (lsGet(PROFILE_REMIND_KEY, "") === today) return;
  lsSet(PROFILE_REMIND_KEY, today);
  showPopover(
    '<p class="example">💾 İlerlemeni kaybetme! Ücretsiz bir profil oluşturup bilinen/favori kelimelerini ve serini buluta kaydedebilirsin. · ' +
      "Don\u2019t lose your progress! Create a free profile to save your known/favorite words and streak to the cloud.</p>" +
      '<p style="margin-top:12px; text-align:center"><a class="vocab-link" href="profile.html">👤 Profili Aç · Open Profile</a></p>'
  );
}

// Persistent, ALWAYS-shown "sign in to your profile" banner -- shown at the
// top of EVERY page that loads shared.js (all 17 study/game pages), not
// just the 3 completion screens that call maybeRemindProfile() above.
// Deliberately has NO dismiss button and NO "once per day" gate (unlike
// maybeRemindProfile()'s modal popover, which is unaffected/unchanged) --
// by explicit request, this one must keep showing on every single page
// load until the device actually has a linked cloud profile, so it can
// never be silenced by mistake and then forgotten about.
function renderProfileNudgeBanner() {
  if (lsGet(PROFILE_LINKED_KEY, 0)) return;
  var main = document.querySelector("main");
  if (!main) return;
  var bar = document.createElement("div");
  bar.className = "profile-nudge-banner";
  bar.innerHTML =
    '<span class="profile-nudge-text">☁️ Profilin yok · No profile yet</span>' +
    '<span class="profile-nudge-actions">' +
    '<a class="profile-nudge-link" href="profile.html" data-tip="' +
    "İlerlemeni kaybetme! Ücretsiz bir profil oluşturup buluta kaydet. · " +
    "Don\u2019t lose your progress! Create a free profile to save it to the cloud." +
    '">👤 Profil Oluştur · Create Profile</a>' +
    "</span>";
  main.insertBefore(bar, main.firstChild);
}
renderProfileNudgeBanner();

// ---------- "💡" info modal for the .seo-content prose -------------------
// Every study/game page ends with a long .seo-content block (heading, three
// paragraphs, a feature list and a related-pages nav). That block exists for
// crawlers, but stacked directly under the actual game it is several screens
// of text nobody reads mid-session -- on a phone it was the single biggest
// reason the app shell's one scrolling surface (.container) had to scroll at
// all, and on desktop it pushed the footer far below the fold. styles.css
// hides it at EVERY width and this injects the small 💡 button that opens it
// in a modal instead.
//
// The prose is MOVED into the modal, never cloned -- a clone would duplicate
// every id inside it and show the same crawler-visible copy twice. On close it
// is appended back to .seo-content, so the DOM returns to its original shape.
//
// moresheet.js carries a copy of this for games.html; the two scripts never
// load together, the same deliberate duplication as the profile-dot and
// service-worker blocks over there.
function wireSeoInfoModal() {
  var section = document.querySelector(".seo-content");
  var actions = document.querySelector(".brand-actions");
  if (!section || !actions) return;
  var inner = section.querySelector(".seo-inner");
  if (!inner || actions.querySelector(".seo-info-btn")) return;

  var btn = document.createElement("button");
  btn.type = "button";
  // .about-btn = the same pill styling as the ℹ️/❓/📊 header icons next to it.
  btn.className = "about-btn seo-info-btn";
  btn.textContent = "💡";
  btn.setAttribute("aria-haspopup", "dialog");
  btn.setAttribute("aria-expanded", "false");
  btn.setAttribute("aria-label", "Bu sayfa hakkında · About this page");
  btn.setAttribute("data-tip", "Bu sayfa hakkında · About this page");
  actions.insertBefore(btn, actions.firstElementChild);
  // The hide rule in styles.css keys off this class, so it can never fire on
  // a page that didn't get a 💡 button to reopen the text.
  section.classList.add("is-collapsed");

  var modal = document.createElement("div");
  modal.className = "seo-modal";
  modal.hidden = true;
  modal.innerHTML =
    '<div class="seo-modal-card" role="dialog" aria-modal="true" ' +
    'aria-label="Bu sayfa hakkında · About this page">' +
    '<div class="seo-modal-head">' +
    '<span class="seo-modal-title">💡 Bu sayfa hakkında · About this page</span>' +
    '<button type="button" class="seo-modal-close" aria-label="Kapat · Close">✕</button>' +
    "</div>" +
    '<div class="seo-modal-body"></div>' +
    "</div>";
  document.body.appendChild(modal);
  var modalBody = modal.querySelector(".seo-modal-body");

  function openSeoModal() {
    modalBody.appendChild(inner);
    modal.hidden = false;
    btn.setAttribute("aria-expanded", "true");
    modalBody.scrollTop = 0;
    modal.querySelector(".seo-modal-close").focus();
  }
  function closeSeoModal() {
    modal.hidden = true;
    section.appendChild(inner); // the one real copy goes back where it was
    btn.setAttribute("aria-expanded", "false");
    btn.focus();
  }
  btn.addEventListener("click", openSeoModal);
  modal.addEventListener("click", function (e) {
    // Backdrop tap or the ✕ (which may be clicked on its own text node).
    if (e.target === modal || (e.target.closest && e.target.closest(".seo-modal-close")))
      closeSeoModal();
  });
  document.addEventListener("keydown", function (e) {
    if (e.key === "Escape" && !modal.hidden) closeSeoModal();
  });
}
wireSeoInfoModal();

// ---------- Activity history (for history.html + the Firebase profile) ----
// A capped, append-only local log of study events -- read by history.html
// (which never loads shared.js, so it has its own tiny copy of these two
// constants, same duplication precedent as every other localStorage key in
// this project) and merged into the Firebase profile document by
// firebase-client.js (see mergeHistory() there for why a plain array field,
// synced from multiple devices, needs a merge instead of a blind overwrite).
var HISTORY_KEY = "udsp_history_v1";
var HISTORY_MAX = 300; // oldest entries roll off -- keeps the profile doc small
function logHistory(entry) {
  try {
    var list = lsGet(HISTORY_KEY, []);
    entry.t = Date.now();
    list.push(entry);
    if (list.length > HISTORY_MAX) list = list.slice(list.length - HISTORY_MAX);
    lsSet(HISTORY_KEY, list);
  } catch (e) {
    /* ignore storage errors (private mode) */
  }
}

var known = lsGet(KNOWN_KEY, {});
var fav = lsGet(FAV_KEY, {});
var srs = lsGet(SRS_KEY, {});
// Words answered wrong, so "review my mistakes" is possible at all. Keyed the
// same way as `known`/`fav`/`srs` (level|word); `n` is how many times the word
// has been missed and doubles as the sort weight, `last` when it last went
// wrong. A word leaves the map once it has been answered correctly as many
// times as it was missed, so a single lucky guess doesn't erase a word the
// learner has got wrong five times.
var mistakes = lsGet(MISTAKE_KEY, {});
var stats = lsGet(STATS_KEY, { answered: 0, correct: 0, exams: 0, reviews: 0 });
var streak = lsGet(STREAK_KEY, { current: 0, longest: 0, last: "" });
// Daily study goal (habit-loop mechanic on the study surfaces). `count` is
// how many study reps (a revealed flashcard, an answered quiz question) have
// happened on `date`; the ring on the study-status panel fills toward `goal`.
// Resets automatically when the calendar day rolls over (compared against
// todayStr(), same y-(m+1)-d format used by the streak).
var GOAL_KEY = "udsp_daily_v1";
var DEFAULT_DAILY_GOAL = 20;
var daily = lsGet(GOAL_KEY, { date: "", count: 0, goal: DEFAULT_DAILY_GOAL });

function levelLabel(l) {
  if (l === "MIX") return "Mix";
  if (l === "PV") return "Phrasal Verbs";
  if (l === "PART") return "Partikelverb";
  var gode = { GA1: "A1", GA2: "A2", GB1: "B1", GB2: "B2", GC1: "C1", GC2: "C2" };
  if (gode[l]) return gode[l];
  return l;
}
function wordKey(w) {
  return (w.level || currentLevel) + "|" + w.word;
}

/* ---------- speech: pick a voice that actually matches the target language ---------- */
var voiceCache = [];
var speechKeepAliveTimer = null;
var speechKeepAliveStopTimer = null;

function stopSpeechKeepAlive() {
  if (speechKeepAliveTimer) clearInterval(speechKeepAliveTimer);
  if (speechKeepAliveStopTimer) clearTimeout(speechKeepAliveStopTimer);
  speechKeepAliveTimer = null;
  speechKeepAliveStopTimer = null;
}

function armSpeechKeepAlive() {
  if (document.visibilityState === "hidden") return;
  if (!speechKeepAliveTimer) {
    speechKeepAliveTimer = setInterval(function () {
      try {
        if (!window.speechSynthesis.speaking) window.speechSynthesis.resume();
      } catch (e) {
        /* ignore */
      }
    }, 8000);
  }
  if (speechKeepAliveStopTimer) clearTimeout(speechKeepAliveStopTimer);
  speechKeepAliveStopTimer = setTimeout(stopSpeechKeepAlive, 120000);
}

function loadVoices() {
  if (!("speechSynthesis" in window)) return;
  try {
    voiceCache = window.speechSynthesis.getVoices() || [];
  } catch (e) {
    voiceCache = [];
  }
}
if ("speechSynthesis" in window) {
  loadVoices();
  if (typeof window.speechSynthesis.addEventListener === "function") {
    window.speechSynthesis.addEventListener("voiceschanged", loadVoices);
  } else {
    window.speechSynthesis.onvoiceschanged = loadVoices;
  }
  // iOS Safari has a well-documented quirk where its cached
  // SpeechSynthesisVoice objects/engine state can go stale after the page
  // is backgrounded (user switches app/tab and comes back) -- speak() then
  // silently produces no audio until the voice list is refreshed. Reload
  // the voice cache every time the page becomes visible again, cheap
  // insurance against that specific mobile scenario.
  if (typeof document.addEventListener === "function") {
    document.addEventListener("visibilitychange", function () {
      if (document.visibilityState === "visible") loadVoices();
      else stopSpeechKeepAlive();
    });
  }
}
function pickVoice(langCode) {
  if (!voiceCache.length) loadVoices();
  var lc = langCode.toLowerCase();
  var prefix = lc.split("-")[0];
  var exact = null;
  var prefixMatch = null;
  for (var i = 0; i < voiceCache.length; i++) {
    var v = voiceCache[i];
    var vl = (v.lang || "").toLowerCase().replace("_", "-");
    if (vl === lc && !exact) exact = v;
    if (vl.split("-")[0] === prefix && !prefixMatch) prefixMatch = v;
  }
  return exact || prefixMatch || null;
}

// If the device has no installed voice at all for the requested language,
// speechSynthesis silently falls back to whatever its system default voice
// is (almost always an English one) and reads the text with English
// pronunciation rules -- audible as "the French/German Listen button
// sounds like English". This can't be fixed in code (there's no French/
// German voice to select), so instead we surface a one-time, dismissible
// explanation the first time it happens, via the existing info popover.
var VOICE_HINT_KEY = "udsp_voice_hint_seen_v1";
function maybeShowVoiceHint(langCode) {
  if (lsGet(VOICE_HINT_KEY, false)) return;
  lsSet(VOICE_HINT_KEY, true);
  showPopover(
    '<p class="example">🔊 Bu cihazda "' +
      escapeHtml(langCode) +
      '" için bir metin okuma (TTS) sesi yüklü değil, bu yüzden telaffuz İngilizce aksanına benzeyebilir. ' +
      "Daha doğru bir telaffuz için: Windows'ta Ayarlar → Saat ve Dil → Dil ve Bölge → Dil ekle (sesli okuma dahil); " +
      "macOS/iOS'ta Ayarlar → Erişilebilirlik → Konuşulan İçerik → Sesler. — " +
      "No text-to-speech voice for this language is installed on this device, so pronunciation may sound like an " +
      "English accent instead. Add a voice for this language in your system's settings for accurate pronunciation." +
      "</p>"
  );
}

function speak(text) {
  if (!("speechSynthesis" in window)) return;
  var t = (text == null ? "" : String(text)).trim();
  if (!t) return;
  try {
    var synth = window.speechSynthesis;
    // Cancel UNCONDITIONALLY (not just when synth.speaking/pending report
    // true) before every speak(). iOS Safari has a long-documented bug
    // where its internal "speaking" flag can get stuck (or the utterance
    // queue gets wedged) without ever reporting speaking/pending as true
    // again -- if that's happened, a conditional cancel() never fires and
    // every future speak() call silently does nothing forever. cancel() on
    // an already-idle engine is a harmless no-op on every platform, so
    // there's no downside to always calling it first.
    synth.cancel();
    // Wake the speech service up in case it fell asleep (see the keepalive
    // interval above) -- a harmless no-op when it's already active.
    synth.resume();
    armSpeechKeepAlive();
    // Refresh the voice cache if it wasn't populated yet at page-load time --
    // otherwise the very first speak() after opening the page sometimes
    // fires with no voice attached and Chromium silently drops it (classic
    // "first utterance never plays" bug). This is especially common on
    // mobile, where the OS voice list often loads asynchronously and isn't
    // ready by the time the very first Listen tap fires.
    var voicesWereEmpty = !voiceCache.length;
    if (voicesWereEmpty) loadVoices();
    var u = new SpeechSynthesisUtterance(t);
    var langCode =
      (LANGS[currentLang] && LANGS[currentLang].speakLang) || "en-US";
    u.lang = langCode;
    var v = pickVoice(langCode);
    if (v) {
      u.voice = v;
    } else if (currentLang !== "en") {
      maybeShowVoiceHint(langCode);
    }
    u.rate = 0.9;
    // Set explicitly rather than relying on the spec default -- cheap
    // insurance against mobile browser builds that don't always apply a
    // sane default for an unset volume/pitch.
    u.volume = 1;
    u.pitch = 1;
    // IMPORTANT: speak() must be called synchronously, in the same call
    // stack as the user gesture (the click handler) that triggered it --
    // Safari/iOS silently refuses to produce any audio if speak() is
    // invoked from inside a setTimeout/Promise callback instead of directly
    // inside the gesture. Do not defer this call.
    synth.speak(u);
    // If the voice list was still empty at the moment we tried to speak
    // (very common on the very first tap on a mobile browser, since the
    // OS/browser hasn't finished loading its voice list yet), automatically
    // retry once as soon as voices actually arrive -- turns a silent first
    // tap into "plays a beat late" instead of "never plays at all" on
    // devices where voices simply load slowly.
    if (voicesWereEmpty && "speechSynthesis" in window) {
      var retryOnce = function () {
        window.speechSynthesis.removeEventListener("voiceschanged", retryOnce);
        if (voiceCache.length) return; // already retried via a fresh speak() call
        loadVoices();
        if (voiceCache.length) speak(t);
      };
      window.speechSynthesis.addEventListener("voiceschanged", retryOnce);
    }
  } catch (e) {
    /* ignore */
  }
}

function todayStr() {
  var d = new Date();
  return d.getFullYear() + "-" + (d.getMonth() + 1) + "-" + d.getDate();
}
function touchStreak() {
  var t = todayStr();
  if (streak.last === t) return;
  var y = new Date();
  y.setDate(y.getDate() - 1);
  var ystr = y.getFullYear() + "-" + (y.getMonth() + 1) + "-" + y.getDate();
  streak.current = streak.last === ystr ? (streak.current || 0) + 1 : 1;
  streak.last = t;
  streak.longest = Math.max(streak.longest || 0, streak.current);
  lsSet(STREAK_KEY, streak);
  renderStreak();
}
function renderStreak() {
  var el = $("streak-count");
  if (el) el.textContent = streak.current || 0;
}

/* ---------- study-surface progress: daily goal + level mastery ---------- */
// Shown on the main study surfaces (Flashcards / Quiz) via the
// #study-status panel. Both render functions no-op on pages that don't have
// the panel, so they're safe to call from shared code.
function dailyGoalValue() {
  return daily.goal && daily.goal > 0 ? daily.goal : DEFAULT_DAILY_GOAL;
}
function dailyCountToday() {
  return daily.date === todayStr() ? daily.count || 0 : 0;
}
// Count one study rep toward today's goal (flashcards.js on reveal,
// quiz.js on answer). Rolls the counter over to a fresh 0 on a new day.
function bumpGoal() {
  var t = todayStr();
  if (daily.date !== t) {
    daily.date = t;
    daily.count = 0;
  }
  daily.goal = dailyGoalValue();
  daily.count = (daily.count || 0) + 1;
  lsSet(GOAL_KEY, daily);
  renderDailyGoal();
}
function renderDailyGoal() {
  var wrap = $("daily-goal");
  if (!wrap) return;
  var count = dailyCountToday();
  var goal = dailyGoalValue();
  var done = count >= goal;
  var pct = goal ? Math.max(0, Math.min(1, count / goal)) : 0;
  var ring = $("daily-goal-ring");
  if (ring) {
    // Circumference of the r=19 circle. Values MUST carry a px unit: a
    // unitless number assigned via .style is invalid CSS for stroke-* and
    // gets silently dropped (the SVG presentation-attribute form accepts
    // bare numbers, but the CSSOM style setter does not).
    var C = 2 * Math.PI * 19;
    ring.style.strokeDasharray = C.toFixed(2) + "px";
    ring.style.strokeDashoffset = (C * (1 - pct)).toFixed(2) + "px";
  }
  setText("daily-goal-count", count);
  wrap.classList.toggle("is-done", done);
  var msg = $("daily-goal-msg");
  if (msg) {
    msg.textContent = done
      ? "Goal reached! 🎉 (" + count + "/" + goal + ")"
      : count + " / " + goal + " cards · " + (goal - count) + " to go";
  }
}
// "Known X / Y at this level" — mastery of the CURRENT filtered word pool
// (respects the active Level + Category). Reflects the `known` map, which is
// now fed by the Again/Good/Easy grade on Flashcards + Review (Good/Easy mark
// a word known; Again clears it).
function knownInWords() {
  var n = 0;
  for (var i = 0; i < WORDS.length; i++) {
    if (known[wordKey(WORDS[i])]) n++;
  }
  return n;
}
function renderMastery() {
  if (!$("mastery-fill") && !$("mastery-known")) return;
  var tot = WORDS.length;
  var kn = knownInWords();
  var pct = tot ? Math.round((kn / tot) * 100) : 0;
  var fill = $("mastery-fill");
  if (fill) fill.style.width = pct + "%";
  setText("mastery-known", kn);
  setText("mastery-total", tot);
  setText("mastery-pct", pct + "%");
}
// Renders both study-surface widgets; called on every level/category/
// language change (from setLevel) and, individually, from the study
// surfaces when their own state changes.
function renderStudyStatus() {
  renderDailyGoal();
  renderMastery();
}

/* ---------- spaced repetition: one shared grading model ---------- */
// Recall is scheduled from objective right/wrong answers in the quiz and the
// games (see answeredWord below) -- there is no self-reported Again/Good/Easy
// gesture. `srs[key] = { box, due, last }`.
var SRS_INTERVALS = [0, 1, 3, 7, 16, 30]; // days per box (0 = due now)
function srsGrade(key, grade) {
  var s = srs[key] || { box: 0 };
  var box = s.box || 0;
  if (grade === "again") box = 1; // forgot -> back to the short interval
  else if (grade === "easy") box = Math.min(box + 2, 5); // skip ahead
  else box = Math.min(box + 1, 5); // good -> next box
  s.box = box;
  s.due = Date.now() + (SRS_INTERVALS[box] || 1) * 24 * 3600 * 1000;
  s.last = Date.now();
  srs[key] = s;
  lsSet(SRS_KEY, srs);
  return s;
}

/* ---------- mistakes: the other half of the recall signal ---------- */
// Flashcard grading is self-reported; the quiz and the games produce an
// objective right/wrong per word. Both feed the same scheduler, but only the
// objective ones feed the mistake list -- "words I actually got wrong" is a
// far more useful review pool than "words I said were hard".
function recordMistake(w) {
  if (!w || !w.word) return;
  var key = wordKey(w);
  var m = mistakes[key] || { n: 0 };
  m.n = (m.n || 0) + 1;
  m.last = Date.now();
  mistakes[key] = m;
  lsSet(MISTAKE_KEY, mistakes);
  srsGrade(key, "again");
}
function clearMistake(w) {
  if (!w || !w.word) return;
  var key = wordKey(w);
  var m = mistakes[key];
  if (!m) return;
  m.n = (m.n || 1) - 1;
  if (m.n <= 0) delete mistakes[key];
  else mistakes[key] = m;
  lsSet(MISTAKE_KEY, mistakes);
}
// One call for "the learner answered this word": schedules it and keeps the
// mistake list honest, without touching `known` (a 1-in-4 multiple-choice
// guess is not evidence that a word is known -- only an explicit Good/Easy
// on the flashcard is).
function answeredWord(w, isCorrect) {
  if (!w || !w.word) return;
  if (isCorrect) {
    srsGrade(wordKey(w), "good");
    clearMistake(w);
  } else {
    recordMistake(w);
  }
}

/* ---------- study source: which slice of the level the user studies ---------- */
// Level and Category answer "which words exist"; this answers "which of them
// do I need today". Without it the SRS schedule, the mistake list and the
// ★ favourites were all write-only -- data went in and nothing ever read it
// back out into an actual study session.
//
// Deliberately scoped to pages that render the picker (#source-nav, currently
// Flashcards and Quiz): a Hangman round drawn from three due words would be a
// worse experience than one drawn from the full level, and silently narrowing
// every game because of a choice made two pages ago would be baffling.
var STUDY_SOURCES = ["all", "due", "mistakes", "starred"];
var SOURCE_LABELS = {
  all: "All words",
  due: "Due for review",
  mistakes: "My mistakes",
  starred: "★ Starred",
};
var studySource = lsGet(SOURCE_KEY, "all");
if (STUDY_SOURCES.indexOf(studySource) === -1) studySource = "all";

// A word is due when it has been graded before and its interval has elapsed.
// Never-studied words are NOT due -- otherwise "Due" would just be "All" and
// the option would be meaningless on day one.
function isDue(key) {
  var s = srs[key];
  return !!s && (s.due || 0) <= Date.now();
}
function matchesSource(w, source) {
  var key = wordKey(w);
  if (source === "due") return isDue(key);
  if (source === "mistakes") return !!mistakes[key];
  if (source === "starred") return !!fav[key];
  return true;
}
function sourceIsActive() {
  return !!$("source-nav") && studySource !== "all";
}
// Applies the active source to a level's word list. "Due" and "Mistakes" come
// back most-urgent-first rather than shuffled, because the order IS the
// recommendation; the page's own Shuffle button can still override it.
function applyStudySource(list) {
  if (!$("source-nav") || studySource === "all") return list.slice();
  var out = list.filter(function (w) {
    return matchesSource(w, studySource);
  });
  if (studySource === "due") {
    out.sort(function (a, b) {
      return (srs[wordKey(a)].due || 0) - (srs[wordKey(b)].due || 0);
    });
  } else if (studySource === "mistakes") {
    out.sort(function (a, b) {
      return (mistakes[wordKey(b)].n || 0) - (mistakes[wordKey(a)].n || 0);
    });
  }
  return out;
}
// Live counts for the picker, so the user can see there is nothing due before
// selecting it rather than after.
function sourceCounts() {
  var pool = WORD_SETS[currentLevel] || [];
  var c = { all: pool.length, due: 0, mistakes: 0, starred: 0 };
  for (var i = 0; i < pool.length; i++) {
    var key = wordKey(pool[i]);
    if (isDue(key)) c.due++;
    if (mistakes[key]) c.mistakes++;
    if (fav[key]) c.starred++;
  }
  return c;
}
function renderSourceOptions() {
  var sel = $("source-nav");
  if (!sel) return;
  var counts = sourceCounts();
  sel.innerHTML = "";
  STUDY_SOURCES.forEach(function (s) {
    var opt = document.createElement("option");
    opt.value = s;
    opt.textContent = SOURCE_LABELS[s] + " (" + counts[s] + ")";
    sel.appendChild(opt);
  });
  sel.value = studySource;
  sizeSelect(sel);
}
function setStudySource(source) {
  if (STUDY_SOURCES.indexOf(source) === -1) return;
  studySource = source;
  lsSet(SOURCE_KEY, source);
  setLevel(currentLevel); // re-derives WORDS and re-fires every page's reset
}
// Explains an empty pool in the user's own terms instead of leaving the page
// showing "1 / 0". Returns "" when there is nothing to explain.
function sourceEmptyMessage() {
  if (!sourceIsActive() || WORDS.length) return "";
  if (studySource === "due")
    return "Nothing is due at this level yet — study some cards and they'll come back here on schedule.";
  if (studySource === "mistakes")
    return "No mistakes recorded at this level. Take a quiz or play a game and any words you miss will collect here.";
  return "No starred words at this level yet — tap ★ on a card to add one.";
}

// Games (Hangman, Cloze Test, Word Scramble, Matching Pairs, Speed Round)
// collapse the language/level/mode nav rows while a level is actively being
// played, leaving more room for the game board — call setPlayHeader(true)
// when entering gameplay and setPlayHeader(false) when back at the level
// picker for that game.
function setPlayHeader(on) {
  document.body.classList.toggle("is-playing", !!on);
}

/* ---------- dom helpers ---------- */
function $(id) {
  return document.getElementById(id);
}
// Guarded helpers: no-op instead of throwing when an element isn't on the
// current page (each page now only contains ONE mode's markup).
function on(id, evt, handler) {
  var el = $(id);
  if (el) el.addEventListener(evt, handler);
  return el;
}
function setText(id, text) {
  var el = $(id);
  if (el) el.textContent = text;
}

/* ---------- bilingual field rendering ----------
 * Every `definition` and `example` in data/*.js packs both languages into one
 * string separated by a literal " - ":
 *     "Ill-fated, fateful. - Uğursuz, kara."
 * Printing that raw puts the separator immediately next to any hyphen the
 * words themselves contain, so the line looks like it has two dashes and it
 * is not obvious where the Turkish starts. Hyphenated headwords are common
 * (662 of them: ill-fated, level-headed, peut-être, ...), so this is not a
 * rare case. Split the string instead and render the halves as separate
 * elements, which removes the separator from the screen entirely.
 */
function splitBilingual(text) {
  var s = String(text == null ? "" : text);
  var idx = s.indexOf(" - ");
  if (idx === -1) return [s.trim(), ""];
  return [s.slice(0, idx).trim(), s.slice(idx + 3).trim()];
}
function bilingualHtml(text) {
  var parts = splitBilingual(text);
  if (!parts[0] && !parts[1]) return "";
  var html = '<span class="bi-native">' + escapeHtml(parts[0]) + "</span>";
  if (parts[1]) {
    html += '<span class="bi-tr" lang="tr">' + escapeHtml(parts[1]) + "</span>";
  }
  return html;
}
function setBilingual(id, text) {
  var el = $(id);
  if (!el) return;
  el.innerHTML = bilingualHtml(text);
}

/* ---------- definition quality guards ----------
 * Two known content defects reach the definition slot and are worth handling
 * in the UI rather than pretending they aren't there (see CONTENT-QUALITY.md):
 *
 *  - 2,769 entries (mostly data/synantde.js) hold a SYNONYM LIST where the
 *    definition should be -- "Ähnlich wie: groß, riesig". Rendering that in
 *    the definition slot passes it off as an explanation of the word. Label
 *    it instead, so the learner knows no real definition was available.
 *  - 330 entries run past 160 characters of native text (raw Wiktionary
 *    prose) and overflow the card on a phone. Clamp them with a control to
 *    expand, rather than letting them push the rest of the card off screen.
 */
var STUB_DEFINITION = /^\s*(Similar to|Ähnlich wie|Semblable à|Simile a|Similar a|Benzer|Şuna benzer|Benzeri)\s*:/i;
var DEF_CLAMP_CHARS = 160;
function isStubDefinition(def) {
  if (!def) return false;
  return STUB_DEFINITION.test(splitBilingual(def)[0]);
}
// Drop-in replacement for setBilingual() on the definition slot.
function setDefinition(id, text) {
  var el = $(id);
  if (!el) return;
  var raw = String(text == null ? "" : text);
  el.classList.remove("is-stub", "is-clamped", "is-expanded");
  el.innerHTML = bilingualHtml(raw);
  if (!raw) return;
  if (isStubDefinition(raw)) {
    el.classList.add("is-stub");
    el.insertAdjacentHTML(
      "afterbegin",
      '<span class="def-note">Sözlük tanımı yok · No dictionary definition</span>'
    );
  } else if (splitBilingual(raw)[0].length > DEF_CLAMP_CHARS) {
    el.classList.add("is-clamped");
    el.insertAdjacentHTML(
      "beforeend",
      '<button class="def-more" type="button">Devamı · Show more</button>'
    );
  }
}
// One delegated listener rather than one per render: the definition element is
// rewritten on every card, so per-render listeners would leak. Registered in
// the CAPTURE phase because the flashcard's own click-to-flip handler sits on
// an ANCESTOR (#flashcard) -- a bubble-phase listener on document runs after
// that handler, far too late for stopPropagation() to keep the card face up.
document.addEventListener(
  "click",
  function (e) {
    var btn = e.target.closest && e.target.closest(".def-more");
    if (!btn) return;
    e.stopPropagation();
    var el = btn.parentNode;
    var expanded = el.classList.toggle("is-expanded");
    el.classList.toggle("is-clamped", !expanded);
    btn.textContent = expanded ? "Daha az · Show less" : "Devamı · Show more";
  },
  true
);
function setHidden(id, hidden) {
  var el = $(id);
  if (el) el.hidden = hidden;
}
function vocabDetailsUrl(word) {
  return LANGS[currentLang].detailsUrl(word);
}
function vocabExamplesUrl(word) {
  return LANGS[currentLang].examplesUrl(word);
}
function vocabLinkHtml(word) {
  return (
    '<span class="vocab-links">' +
    '<a class="vocab-link" href="' +
    vocabDetailsUrl(word) +
    '" target="_blank" rel="noopener noreferrer" data-tip="Detaylar">Details ↗</a>' +
    '<a class="vocab-link" href="' +
    vocabExamplesUrl(word) +
    '" target="_blank" rel="noopener noreferrer" data-tip="Örnekler">Examples ↗</a>' +
    "</span>"
  );
}

// On-demand clue reveal: a 💡 button sits next to any clue masked with
// .clue-gated (see styles.css); clicking it toggles the reveal, same as
// hovering. stopImmediatePropagation keeps this from also firing a game's
// own delegated click handler on the row the bulb happens to sit inside
// (e.g. Crossword/CodyCross select that clue's word on a plain row click).
document.addEventListener("click", function (e) {
  var bulb = e.target.closest && e.target.closest(".clue-bulb");
  if (!bulb) return;
  var target = document.getElementById(bulb.getAttribute("data-clue-for") || "");
  if (target) target.classList.toggle("is-revealed");
  e.stopImmediatePropagation();
  e.preventDefault();
});

// Details/Examples links open a real tab -- Cambridge (used for English,
// Portuguese and Dutch) sends X-Frame-Options: SAMEORIGIN, so an in-page
// iframe modal would just show a blank frame for those languages. Instead,
// keep the window handle and offer a "close it and return" banner. Skipped
// entirely inside the packaged Windows/Linux/macOS hosts: those already
// intercept external navigation into their own restricted native viewer
// (see MainWindow.xaml.cs's OpenExternalLink / ExternalViewerWindow), which
// this would only fight with.
var externalReturnWin = null;
function showExternalReturnBanner(win, label) {
  externalReturnWin = win;
  var banner = document.getElementById("external-return-banner");
  if (!banner) {
    banner = document.createElement("div");
    banner.id = "external-return-banner";
    banner.className = "external-return-banner";
    banner.hidden = true;
    banner.innerHTML =
      '<span class="external-return-text"></span>' +
      '<button type="button" id="external-return-close">✕ Kapat ve dön · Close & return</button>';
    document.body.appendChild(banner);
    banner.querySelector("#external-return-close").addEventListener("click", function () {
      try {
        if (externalReturnWin && !externalReturnWin.closed) externalReturnWin.close();
      } catch (e) {
        /* ignore -- the tab may already be gone or cross-origin-detached */
      }
      banner.hidden = true;
      externalReturnWin = null;
    });
  }
  var textEl = banner.querySelector(".external-return-text");
  if (textEl) {
    textEl.textContent =
      "🔗 " + (label || "Harici sayfa") + " başka bir sekmede açıldı. · Opened in another tab.";
  }
  banner.hidden = false;
}
// A plain focus event (not a poll) -- if the visitor closed the external tab
// themselves and simply switched back, the banner clears itself for free.
window.addEventListener("focus", function () {
  if (externalReturnWin && externalReturnWin.closed) {
    var banner = document.getElementById("external-return-banner");
    if (banner) banner.hidden = true;
    externalReturnWin = null;
  }
});
document.addEventListener("click", function (e) {
  if (window.TWApp && window.TWApp.isPackaged && window.TWApp.isPackaged()) return;
  var link = e.target.closest && e.target.closest("a.vocab-link");
  if (!link) return;
  var win = window.open(link.href, "udspExternal");
  if (!win) return; // popup blocked -- let the native target="_blank" behavior proceed
  e.preventDefault();
  win.focus();
  showExternalReturnBanner(win, link.textContent.replace("↗", "").trim());
});
function escapeHtml(s) {
  return String(s)
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

// Some data files fall back to a generic, content-free example sentence when
// no real example could be sourced for a word (e.g. `I am learning the word
// "X".` / `Ich lerne das Wort "X".` / `J'apprends le mot «X».`, or the newer
// honest "No example sentence available..." / "Kein Beispielsatz..."
// fallback used when even a real Tatoeba-sourced sentence couldn't be found
// -- see scripts/fix_placeholder_examples.py). That's an acceptable
// last-resort for plain display, but any game that builds its QUESTION out
// of the example sentence itself breaks down with it -- e.g. Cloze Test's
// fill-in-the-blank becomes "I am learning the word "_______"." which gives
// zero context clue (the blank could be any word), and Sentence Scramble's
// word-reorder puzzle teaches nothing (or, for the honest fallback, would
// scramble a sentence totally unrelated to the word). Games that construct
// a question from `example` should exclude these via this helper.
function isPlaceholderExample(example) {
  if (!example) return false;
  return /^(I am learning the word|Ich lerne das Wort|J['’]apprends le mot|No example sentence available|Kein Beispielsatz|Aucune phrase d'exemple|No hay frase de ejemplo|Nessuna frase di esempio|Nenhuma frase de exemplo)\b/.test(
    String(example).trim()
  );
}

// Case/diacritic-insensitive text fold, shared by typing-based games (Word
// Race, Listening Dictation) for answer checking.
function foldText(s) {
  if (!s) return "";
  var t = String(s).replace(/\u00df/g, "ss");
  t = t.normalize ? t.normalize("NFD").replace(/[\u0300-\u036f]/g, "") : t;
  return t.toLowerCase().trim();
}
// True if a/b differ by at most one insert/delete/substitute (cheap
// two-pointer approximation of edit-distance-<=1, not a full DP).
function editDistanceLE1(a, b) {
  if (a === b) return true;
  var la = a.length,
    lb = b.length;
  if (Math.abs(la - lb) > 1) return false;
  var i = 0,
    j = 0,
    edits = 0;
  while (i < la && j < lb) {
    if (a[i] === b[j]) {
      i++;
      j++;
      continue;
    }
    edits++;
    if (edits > 1) return false;
    if (la === lb) {
      i++;
      j++;
    } else if (la > lb) {
      i++;
    } else {
      j++;
    }
  }
  if (i < la || j < lb) edits++;
  return edits <= 1;
}
// Small-tolerance fuzzy match for typed answers: exact match always passes;
// a single-character typo is forgiven only for words of 5+ letters (short
// words need exact spelling, since a 1-edit tolerance would accept too many
// unrelated words).
function fuzzyMatch(input, target) {
  var a = foldText(input);
  var b = foldText(target);
  if (!a || !b) return false;
  if (a === b) return true;
  if (b.length >= 5) return editDistanceLE1(a, b);
  return false;
}

function shuffle(arr) {
  var a = arr.slice();
  for (var i = a.length - 1; i > 0; i--) {
    var j = Math.floor(Math.random() * (i + 1));
    var t = a[i];
    a[i] = a[j];
    a[j] = t;
  }
  return a;
}
function loadBest() {
  try {
    return JSON.parse(localStorage.getItem(storageKey())) || {};
  } catch (e) {
    return {};
  }
}
function saveBest(obj) {
  try {
    localStorage.setItem(storageKey(), JSON.stringify(obj));
  } catch (e) {
    /* ignore storage errors (private mode) */
  }
}

// Show/hide a revealable example/hint paragraph for a card or question.
// The paragraph itself is never shown inline any more (always stays
// hidden) -- its text is just the data source for the floating popover
// (see showPopover() below); this keeps every existing call site's markup
// and IDs unchanged.
function resetExample(btnId, exampleId, example, showButton) {
  var btn = $(btnId);
  var ex = $(exampleId);
  if (!btn || !ex) return;
  // 20,240 entries (17% of the corpus) carry an honest "No example sentence
  // available" placeholder. Offering a button that reveals THAT is worse than
  // offering no button, so treat a placeholder as no example at all.
  var usable = !!example && !isPlaceholderExample(example);
  // Store the RAW bilingual string; wireExample() splits it at display time
  // so the " - " separator never reaches the screen.
  ex.textContent = usable ? example : "";
  ex.hidden = true;
  btn.hidden = !usable || !showButton;
}
function wireExample(btnId, exampleId) {
  on(btnId, "click", function (e) {
    e.stopPropagation();
    var ex = $(exampleId);
    if (ex && ex.textContent) {
      var parts = splitBilingual(ex.textContent);
      var html =
        '<p class="example"><span class="bi-native">“' +
        escapeHtml(parts[0]) +
        "”</span>";
      if (parts[1]) {
        html += '<span class="bi-tr" lang="tr">' + escapeHtml(parts[1]) + "</span>";
      }
      showPopover(html + "</p>");
    }
  });
}

/* ---------- floating info popover (Hint / Example reveal box) ----------
 * Every "Hint"/"Example" style reveal button across the app shows its
 * extra content in this one small, rounded, closeable overlay instead of
 * inline in the page flow -- positioned on top of the current card/question
 * so it works the same on every screen (quiz, review, any game). This is
 * deliberately NOT a browser popup window (window.open()) -- those can be
 * blocked by the browser -- just a positioned <div> with a backdrop, closed
 * via its own × button, the Escape key, or clicking the backdrop.
 */
function ensurePopover() {
  var existing = $("info-popover");
  if (existing) return existing;
  var backdrop = document.createElement("div");
  backdrop.className = "info-popover-backdrop";
  backdrop.id = "info-popover-backdrop";
  backdrop.hidden = true;
  var box = document.createElement("div");
  box.className = "info-popover";
  box.id = "info-popover";
  box.hidden = true;
  box.setAttribute("role", "dialog");
  box.setAttribute("aria-modal", "false");
  box.innerHTML =
    '<button type="button" class="info-popover-close" id="info-popover-close" aria-label="Close">×</button>' +
    '<div class="info-popover-body" id="info-popover-body"></div>';
  document.body.appendChild(backdrop);
  document.body.appendChild(box);
  backdrop.addEventListener("click", hidePopover);
  box.querySelector(".info-popover-close").addEventListener("click", hidePopover);
  document.addEventListener("keydown", function (e) {
    if (e.key === "Escape") hidePopover();
  });
  return box;
}
function showPopover(html) {
  ensurePopover();
  var body = $("info-popover-body");
  if (body) body.innerHTML = html;
  setHidden("info-popover-backdrop", false);
  setHidden("info-popover", false);
}
function hidePopover() {
  setHidden("info-popover-backdrop", true);
  setHidden("info-popover", true);
}

/* ---------- "Report this word" ----------
 * Lets learners flag a bad/wrong/inappropriate word. Works with no backend:
 * every report is logged locally (localStorage) so nothing is lost, and — if
 * the site owner sets REPORT_EMAIL below — a pre-filled email is also opened.
 * OWNER: put your contact address in REPORT_EMAIL (e.g. "you@example.com") to
 * receive reports by email; leave it "" to only collect them locally. */
var REPORT_EMAIL = "bulentozkir@gmail.com";
var REPORTS_KEY = "udsp_word_reports_v1";
var _pendingReport = null;

function reportWord(word, level) {
  if (!word) return;
  _pendingReport = { word: word, level: level || currentLevel, lang: currentLang };
  var reasons = ["Wrong meaning", "Wrong level", "Inappropriate", "Other"];
  var chips = reasons
    .map(function (r) {
      return (
        '<button type="button" class="vocab-link report-reason" data-reason="' +
        escapeHtml(r) +
        '">' +
        escapeHtml(r) +
        "</button>"
      );
    })
    .join(" ");
  showPopover(
    '<p class="example">⚑ Report “<strong>' +
      escapeHtml(word) +
      "</strong>” (" +
      escapeHtml(_pendingReport.level) +
      ")</p>" +
      "<p>What is wrong with this word?</p>" +
      '<div class="vocab-links">' +
      chips +
      "</div>"
  );
  var body = $("info-popover-body");
  if (body) {
    var btns = body.querySelectorAll(".report-reason");
    Array.prototype.forEach.call(btns, function (btn) {
      btn.addEventListener("click", function () {
        submitWordReport(btn.getAttribute("data-reason"));
      });
    });
  }
}

function submitWordReport(reason) {
  if (!_pendingReport) return;
  var rep = {
    word: _pendingReport.word,
    level: _pendingReport.level,
    lang: _pendingReport.lang,
    reason: reason,
    t: Date.now(),
  };
  try {
    var all = lsGet(REPORTS_KEY, []);
    all.push(rep);
    lsSet(REPORTS_KEY, all);
  } catch (e) {}
  if (REPORT_EMAIL) {
    var subject = "Top Words word report: " + rep.word;
    var linesBody =
      "Word: " + rep.word +
      "\nLevel: " + rep.level +
      "\nLanguage: " + rep.lang +
      "\nIssue: " + reason +
      "\n\nAny extra details:\n";
    try {
      window.location.href =
        "mailto:" +
        REPORT_EMAIL +
        "?subject=" +
        encodeURIComponent(subject) +
        "&body=" +
        encodeURIComponent(linesBody);
    } catch (e) {}
  }
  showPopover(
    '<p class="example">✅ Thanks!</p><p>Your report about “<strong>' +
      escapeHtml(rep.word) +
      "</strong>” was noted. Reports help us keep the word lists accurate and appropriate.</p>"
  );
  _pendingReport = null;
}

/* ---------- change hooks ----------
 * Mode scripts call onLevelChange(fn) once at load time to register their
 * own re-render function; shared.js calls fireLevelChange() whenever the
 * language or level changes (including the very first render, triggered by
 * startApp() at the bottom of the page). */
var _changeHooks = [];
function onLevelChange(fn) {
  _changeHooks.push(fn);
}
function fireLevelChange() {
  _changeHooks.forEach(function (fn) {
    try {
      fn();
    } catch (e) {
      // eslint-disable-next-line no-console
      console.error(e);
    }
  });
}

/* ================= language + level + category switching ================= */
// Language, Level and Category are native <select> combo boxes -- not
// spread-out button rows -- to keep the header compact (German alone has
// 13 levels). Study-mode navigation lives in the main toolbar on the
// header's first row.
var levelsNav = $("levels-nav");
var langsNav = $("langs-nav");
var categoryBtn = $("category-btn");
var sourceNav = $("source-nav"); // only on the study surfaces (Flashcards, Quiz)

// The Category picker is a "Filter" button that opens a chip-grid popover
// (built below), NOT a 29-item native <select> -- it was hard to scan and
// ~82% of words are the catch-all "General" domain. renderCategoryButtons()
// (kept plural for its existing call sites in setLang/startApp) now just
// refreshes that button's label to the active category.
function renderCategoryButtons() {
  var btn = $("category-btn");
  if (!btn) return;
  var label = currentCategory === "Mix" ? "All" : currentCategory;
  btn.textContent = "🗂 " + label;
  btn.setAttribute("aria-label", "Filter by category — current: " + label);
}

/* ---------- Category filter: counts + chip-grid popover ---------- */
// Raw (un-category-filtered) word list for a level, so the chip counts are
// stable regardless of which domain is currently active. "MIX" concatenates
// every level for the current language.
//
// Pages whose Category filter runs on a DIFFERENT dataset than the shared
// CEFR WORD_SETS (Word Morph filters its own synonym/antonym set) register a
// provider via setCategoryWordsProvider() so the chip counts match what the
// page actually filters. When none is set, the CEFR WORD_SETS are used.
var _categoryWordsProvider = null;
function setCategoryWordsProvider(fn) {
  _categoryWordsProvider = fn;
}
// Same idea for the footer's "N words" count: Word Morph studies its own
// synonym/antonym pool, so reporting WORDS.length there would advertise a CEFR
// total the page can't actually draw from. Pages with their own pool register
// a provider and call refreshPoolCount() whenever that pool changes.
var _poolCountProvider = null;
function setPoolCountProvider(fn) {
  _poolCountProvider = fn;
}
function refreshPoolCount() {
  setText("word-total", _poolCountProvider ? _poolCountProvider() : WORDS.length);
}
function rawWordsForLevel(level) {
  if (_categoryWordsProvider) return _categoryWordsProvider(level) || [];
  var cfg = LANGS[currentLang];
  if (level === "MIX") {
    return cfg.levels.reduce(function (acc, l) {
      return acc.concat(window[cfg.sets[l]] || []);
    }, []);
  }
  return (window[cfg.sets[level]] || []).slice();
}
// { counts: {domain: n}, total } for the CURRENT level.
function categoryCounts() {
  var raw = rawWordsForLevel(currentLevel);
  var counts = {};
  for (var i = 0; i < raw.length; i++) {
    var c = raw[i].category || "General";
    counts[c] = (counts[c] || 0) + 1;
  }
  return { counts: counts, total: raw.length };
}
function ensureCategoryPopover() {
  if ($("category-popover")) return;
  var backdrop = document.createElement("div");
  backdrop.id = "category-popover-backdrop";
  backdrop.hidden = true;
  var pop = document.createElement("div");
  pop.id = "category-popover";
  pop.setAttribute("role", "dialog");
  pop.setAttribute("aria-label", "Filter by category");
  pop.hidden = true;
  pop.innerHTML =
    '<div class="cat-pop-head">' +
    '<span class="cat-pop-title">Filter by category</span>' +
    '<button class="cat-pop-close" type="button" aria-label="Close">×</button>' +
    "</div>" +
    '<div class="cat-pop-grid" id="category-popover-grid"></div>';
  document.body.appendChild(backdrop);
  document.body.appendChild(pop);
  backdrop.addEventListener("click", closeCategoryFilter);
  pop.querySelector(".cat-pop-close").addEventListener("click", closeCategoryFilter);
  document.addEventListener("keydown", function (e) {
    if (e.key === "Escape" && !pop.hidden) closeCategoryFilter();
  });
  window.addEventListener("resize", function () {
    if (!pop.hidden) positionCategoryPopover();
  });
}
function positionCategoryPopover() {
  var btn = $("category-btn");
  var pop = $("category-popover");
  if (!btn || !pop) return;
  var r = btn.getBoundingClientRect();
  var left = Math.min(r.left, window.innerWidth - pop.offsetWidth - 8);
  pop.style.left = Math.max(8, left) + "px";
  pop.style.top = r.bottom + 8 + "px";
}
function addCategoryChip(grid, value, label, count) {
  var b = document.createElement("button");
  b.type = "button";
  b.className = "cat-chip" + (value === currentCategory ? " is-active" : "");
  b.innerHTML =
    '<span class="cat-chip-name">' +
    escapeHtml(label) +
    '</span><span class="cat-chip-count">· ' +
    count +
    "</span>";
  b.addEventListener("click", function () {
    closeCategoryFilter();
    setCategory(value);
  });
  grid.appendChild(b);
}
function renderCategoryChips() {
  var grid = $("category-popover-grid");
  if (!grid) return;
  var data = categoryCounts();
  grid.innerHTML = "";
  addCategoryChip(grid, "Mix", "All", data.total); // "All" = no filter
  // Only domains that actually have words at this level (plus the active one,
  // even if it's 0, so it stays visible/deselectable), most-populous first.
  var domains = CATEGORIES.filter(function (c) {
    return c !== "Mix" && ((data.counts[c] || 0) > 0 || c === currentCategory);
  });
  domains.sort(function (a, b) {
    return (data.counts[b] || 0) - (data.counts[a] || 0);
  });
  domains.forEach(function (c) {
    addCategoryChip(grid, c, c, data.counts[c] || 0);
  });
}
function openCategoryFilter() {
  ensureCategoryPopover();
  renderCategoryChips();
  $("category-popover-backdrop").hidden = false;
  $("category-popover").hidden = false;
  positionCategoryPopover();
  var btn = $("category-btn");
  if (btn) btn.setAttribute("aria-expanded", "true");
}
function closeCategoryFilter() {
  var pop = $("category-popover");
  var bd = $("category-popover-backdrop");
  if (pop) pop.hidden = true;
  if (bd) bd.hidden = true;
  var btn = $("category-btn");
  if (btn) btn.setAttribute("aria-expanded", "false");
}
function toggleCategoryFilter() {
  var pop = $("category-popover");
  if (pop && !pop.hidden) closeCategoryFilter();
  else openCategoryFilter();
}

function renderLevelButtons() {
  if (!levelsNav) return;
  levelsNav.innerHTML = "";
  LEVELS.forEach(function (l) {
    var opt = document.createElement("option");
    opt.value = l;
    opt.textContent = levelLabel(l);
    levelsNav.appendChild(opt);
  });
  var mix = document.createElement("option");
  mix.value = "MIX";
  mix.textContent = "Mix";
  levelsNav.appendChild(mix);
}

// Captured once at load time (before any language switch ever mutates it),
// so applyLang() can prefix the CURRENT language onto the page's own
// hand-crafted SEO <title> instead of replacing it outright. Overwriting
// document.title wholesale on every load used to be a real bug (see the
// "SEO optimization pass" section of the repo notes) -- this only ever
// prepends to the original static title, never destroys it.
var BASE_TITLE = document.title;

function applyLang() {
  var cfg = LANGS[currentLang];
  document.documentElement.lang = currentLang;
  document.title = cfg.label + " — " + BASE_TITLE;
  if (langsNav) langsNav.value = currentLang;
}

function applyLevelLabels() {
  document
    .querySelectorAll("#footer-level, #picker-level, #picker-level-2")
    .forEach(function (el) {
      el.textContent = levelLabel(currentLevel);
    });
}

// The compact combo boxes are deliberately small, but a native <select>
// auto-sizes to its WIDEST option, which forced the current selection to
// ellipsize ("Phrasal Verbs", "Transportation", "Word Morph" got cut off).
// Instead, size each pill to fit its CURRENTLY selected value: short values
// (B2, Mix, English) stay tiny while long ones expand just enough to stay
// fully legible (the selectors row wraps if a line gets too wide).
var _selMeasure = null;
function sizeSelect(sel) {
  if (!sel || !sel.options || !sel.options.length) return;
  if (!_selMeasure) {
    _selMeasure = document.createElement("span");
    _selMeasure.setAttribute("aria-hidden", "true");
    _selMeasure.style.cssText =
      "position:absolute;top:-9999px;left:-9999px;visibility:hidden;white-space:pre;";
    document.body.appendChild(_selMeasure);
  }
  var opt = sel.options[sel.selectedIndex] || sel.options[0];
  var cs = window.getComputedStyle(sel);
  _selMeasure.style.fontFamily = cs.fontFamily;
  _selMeasure.style.fontSize = cs.fontSize;
  _selMeasure.style.fontWeight = cs.fontWeight;
  _selMeasure.style.letterSpacing = cs.letterSpacing;
  _selMeasure.textContent = opt ? opt.textContent : sel.value;
  // border-box: width = text + horizontal padding (which reserves room for
  // the dropdown arrow) + borders + a few px of breathing room.
  var w =
    _selMeasure.offsetWidth +
    parseFloat(cs.paddingLeft || 0) +
    parseFloat(cs.paddingRight || 0) +
    parseFloat(cs.borderLeftWidth || 0) +
    parseFloat(cs.borderRightWidth || 0) +
    4;
  sel.style.width = Math.max(44, Math.min(180, Math.round(w))) + "px";
}
function sizeAllSelects() {
  sizeSelect($("langs-nav"));
  sizeSelect($("levels-nav"));
  sizeSelect($("source-nav"));
}

function setLevel(level) {
  // Only checks the level KEY exists (a real level for this language) --
  // NOT its length, since a legitimate level can have 0 words once a
  // Category filter is applied (e.g. "Forestry" within English B2). In
  // that case WORDS correctly becomes an empty array and every page's own
  // "not enough words" messaging (already keyed off WORD_SETS[level]/
  // WORDS.length) takes over, same as it already does for any other
  // low-word-count level.
  if (level !== "MIX" && !LANGS[currentLang].sets[level]) return;
  // The level's data file may not be fetched yet (see ensureLevelData()).
  // When it already is, this callback runs synchronously, so the common
  // path behaves exactly as it did when every file was in the HTML.
  var lang = currentLang;
  setLevelBusy(true);
  ensureLevelData(lang, level, function () {
    if (lang !== currentLang) return; // language switched while we waited
    setLevelBusy(false);
    // Rebuild from the raw globals: the file we just loaded defines one.
    WORD_SETS = buildWordSets(currentLang);
    if (!WORD_SETS[level]) return;
    currentLevel = level;
    WORDS = applyStudySource(WORD_SETS[level]);

    if (levelsNav) levelsNav.value = level;
    applyLevelLabels();
    // The footer count reports the pool actually being studied, so a "Due (7)"
    // session doesn't claim 2,905 words.
    refreshPoolCount();
    renderSourceOptions();
    saveResume();
    fireLevelChange();
    renderStudyStatus();
    sizeAllSelects();
  });
}

// Disables the Language/Level pickers while a data file is in flight, so a
// second switch can't race the first. Purely cosmetic when the file is
// already cached (the callback runs synchronously and this never paints).
function setLevelBusy(busy) {
  if (levelsNav) levelsNav.disabled = busy;
  if (langsNav) langsNav.disabled = busy;
  if (sourceNav) sourceNav.disabled = busy;
  document.body.classList.toggle("data-loading", !!busy);
}

// Switches which semantic domain the word pool is filtered to. Unlike
// setLang()/setLevel(), this does NOT change the level/language -- it
// rebuilds WORD_SETS from the raw data with the new domain filter applied
// (buildWordSets() reads `currentCategory`) and re-applies the SAME
// current level, so e.g. switching from Mix to "Sports" while on English
// B2 stays on B2, just narrowed down to B2's Sports-tagged words.
function setCategory(category) {
  if (CATEGORIES.indexOf(category) === -1) return;
  if (category === currentCategory) return;
  currentCategory = category;
  WORD_SETS = buildWordSets(currentLang);
  renderCategoryButtons();
  setLevel(currentLevel);
}

function setLang(lang) {
  if (!LANGS[lang] || lang === currentLang) return;
  setLevelBusy(true);
  var target = LANGS[lang].defaultLevel;
  // Only the level we are about to show is fetched -- picking German no
  // longer downloads all seven German files, let alone the English ones.
  ensureLevelData(lang, target, function () {
    ensureExtraData(lang, function () {
      setLevelBusy(false);
      currentLang = lang;
      WORD_SETS = buildWordSets(lang);
      LEVELS = LANGS[lang].levels.slice();
      DEFAULT_LEVEL = target;
      applyLang();
      renderCategoryButtons();
      renderLevelButtons();
      setLevel(DEFAULT_LEVEL);
      prefetchLangInBackground(lang);
    });
  });
}

if (langsNav) {
  langsNav.addEventListener("change", function () {
    setLang(langsNav.value);
  });
}
if (levelsNav) {
  levelsNav.addEventListener("change", function () {
    setLevel(levelsNav.value);
  });
}
if (categoryBtn) {
  categoryBtn.addEventListener("click", toggleCategoryFilter);
}
if (sourceNav) {
  sourceNav.addEventListener("change", function () {
    setStudySource(sourceNav.value);
  });
}

function saveResume() {
  // Category is intentionally NOT persisted: it always defaults to "All"
  // (Mix) on every page load. Only language + level resume across pages.
  lsSet(RESUME_KEY, { lang: currentLang, level: currentLevel });
}

/* ================= mobile bottom nav "More" sheet ================= */
(function wireMoreSheet() {
  var moreBtn = $("more-btn");
  var sheet = $("more-sheet");
  var closeBtn = $("more-close");
  if (!moreBtn || !sheet) return;
  function openSheet() {
    sheet.hidden = false;
    document.body.classList.add("more-open");
  }
  function closeSheet() {
    sheet.hidden = true;
    document.body.classList.remove("more-open");
  }
  moreBtn.addEventListener("click", openSheet);
  if (closeBtn) closeBtn.addEventListener("click", closeSheet);
  sheet.addEventListener("click", function (e) {
    if (e.target === sheet) closeSheet();
  });
  document.addEventListener("keydown", function (e) {
    if (e.key === "Escape" && !sheet.hidden) closeSheet();
  });
})();

/* ================= service worker ================= */
// Registers sw.js, which serves the word lists from the Cache API instead of
// the network and makes the app usable offline. Deliberately deferred to the
// load event: registration competes with the level's data file for bandwidth,
// and the first paint matters more than the second visit.
//
// NOTE: moresheet.js carries an identical copy of this block for the pages
// that never load shared.js (home, about, help, games, listening, history,
// profile). Keep the two in sync.
(function registerServiceWorker() {
  if (!("serviceWorker" in navigator)) return;
  // file:// has no service worker scope and localhost is exempt from the
  // secure-origin rule, so this covers local testing as well as production.
  if (location.protocol !== "https:" && location.hostname !== "localhost" && location.hostname !== "127.0.0.1")
    return;
  window.addEventListener("load", function () {
    navigator.serviceWorker.register("/sw.js").catch(function () {
      /* an unavailable worker is never fatal -- the app just goes back to
         hitting the network for everything */
    });
  });
})();

/* ================= bootstrap (called once, at end of <body>) ================= */
function startApp() {
  var resume = lsGet(RESUME_KEY, null);
  // Fetch exactly ONE level's data file -- the one the visitor is about to
  // see -- plus this page's own extra dataset if it declares one. Everything
  // else for the language is warmed afterwards, off the critical path.
  var initialLang = resume && LANGS[resume.lang] ? resume.lang : currentLang;
  var initialLevel =
    resume && resume.level && (resume.level === "MIX" || LANGS[initialLang].sets[resume.level])
      ? resume.level
      : LANGS[initialLang].defaultLevel;
  ensureLevelData(initialLang, initialLevel, function () {
    ensureExtraData(initialLang, function () {
      currentLang = initialLang;
      LEVELS = LANGS[currentLang].levels.slice();
      DEFAULT_LEVEL = LANGS[currentLang].defaultLevel;
      // Category always defaults to "All" (Mix) on every page load -- it is
      // intentionally NOT restored from resume state (only lang + level are),
      // so filtering starts unfiltered on every page.
      // WORD_SETS depends on currentCategory (see buildWordSets()), so it's
      // (re)built AFTER restoring the language, not before.
      WORD_SETS = buildWordSets(currentLang);
      if (!(WORD_SETS[initialLevel] || []).length && !(WORD_SETS.MIX || []).length) {
        var c = document.querySelector(".container");
        if (c) {
          c.innerHTML =
            '<p class="empty">No words loaded. Check the data/words*.js files.</p>';
        }
        return;
      }
      renderStreak();
      applyLang();
      renderCategoryButtons();
      renderLevelButtons();
      setLevel(initialLevel);
      prefetchLangInBackground(currentLang);
    });
  });
}
