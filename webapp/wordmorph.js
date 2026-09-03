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

/* Top Words — Word Morph (synonym / antonym guessing) page logic.
 * Requires shared.js.
 *
 * Data source: this game deliberately does NOT use the CEFR data/words*.js
 * files as its WORD_SETS. Instead it reads three standalone globals
 * populated by data/synanten.js (window.SYN_ANT_EN), data/synantde.js
 * (window.SYN_ANT_DE), and data/synantfr.js (window.SYN_ANT_FR). Each
 * entry is { word, level, category, definition, example, synonyms,
 * antonyms } -- `level` (A1..C2) and `category` (topical domain) are
 * assigned by scripts/synant_metadata.py (frequency-rank sextile split +
 * the shared classify_word_categories.py keyword classifier), see each
 * data file's header for details. The CEFR words*.js files are still
 * loaded on this page (see wordmorph.html) only so the shared header/
 * language-switch infrastructure in shared.js has *some* non-empty word
 * data to bootstrap with; Word Morph reads `currentLang` from that shared
 * state, but its own pool is built from SYN_ANT_* only -- NOT from
 * WORD_SETS/WORDS. It DOES, however, respect the shared header's Level and
 * Category selectors as filters over that SYN_ANT_* pool (see
 * wmLevelCategoryFilteredPool() below), same as Flashcards/Quiz/etc.
 * filter WORD_SETS.
 */
"use strict";

var WM_TYPES = ["synonyms", "antonyms"];
var WM_TYPE_LABEL = {
  synonyms: "Synonym",
  antonyms: "Antonym",
};
var WM_TYPE_ARTICLE = {
  synonyms: "a",
  antonyms: "an",
};
var WM_LANG_LABEL = {
  en: "Synonyms & Antonyms (English)",
  de: "Synonyme & Antonyme (German)",
  fr: "Synonymes & Antonymes (French)",
};

// Word Morph's own data (SYN_ANT_EN/DE/FR) only ever carries a plain CEFR
// level (A1..C2, assigned by scripts/synant_metadata.py's frequency-rank
// split -- see data/synant*.js headers). The shared header Level dropdown
// has MANY more values per language (English: PV/TOEFL; German: telc
// sub-levels like "A1.1"/"A1.2" and gode-prefixed "GA1".."GC2"; both:
// "MIX"). This maps any of those down to a clean A1..C2 bucket, or null
// when the selected level has no CEFR-bucket meaning for this dataset (in
// which case Word Morph simply shows ALL levels, same as picking "Mix").
var WM_TELC_TO_CEFR = { "A1.1": "A1", "A1.2": "A1", "A2.1": "A2", "A2.2": "A2", "B1.1": "B1", "B1.2": "B1" };
var WM_GODE_TO_CEFR = { GA1: "A1", GA2: "A2", GB1: "B1", GB2: "B2", GC1: "C1", GC2: "C2" };
function wmNormalizeLevel(level) {
  if (WM_GODE_TO_CEFR[level]) return WM_GODE_TO_CEFR[level];
  if (WM_TELC_TO_CEFR[level]) return WM_TELC_TO_CEFR[level];
  if (/^[ABC][12]$/.test(level || "")) return level;
  return null;
}
var WM_ROUND_SIZE = 10;
var WM_WIN_THRESHOLD = 5; // score >= 5 out of 10 = Winner, else Loss

var wmActive = false;
var wmRoundWords = []; // the WM_ROUND_SIZE pool items picked for the current round
var wmRoundIndex = 0; // 0-based index of the current question within the round
var wmRoundScore = 0; // correct answers so far this round
var wmRoundsWon = 0; // session tally of rounds finished with score >= WM_WIN_THRESHOLD
var wmRoundsLost = 0; // session tally of rounds finished with score < WM_WIN_THRESHOLD
var wmItem = null; // { entry, types: { synonyms: [...], antonyms: [...] } }
var wmType = null;
var wmDone = false;
var wmPoolCache = {};
var wmTypeFilter = "synonyms"; // user's Synonym/Antonym choice on the setup screen; every question in a round matches this type

function wmFold(s) {
  if (!s) return "";
  var t = String(s).replace(/\u00df/g, "ss");
  t = t.normalize ? t.normalize("NFD").replace(/[\u0300-\u036f]/g, "") : t;
  return t.toLowerCase().trim();
}

// Split a semicolon-separated field into a clean, deduplicated list.
function wmSplitField(str) {
  if (!str) return [];
  var seen = {};
  var out = [];
  String(str)
    .split(";")
    .forEach(function (part) {
      var v = part.trim();
      if (!v) return;
      var f = wmFold(v);
      if (seen[f]) return;
      seen[f] = true;
      out.push(v);
    });
  return out;
}

// Word Morph's own per-language source lists (level/category filtering is
// applied on top, see wmLevelCategoryFilteredPool() below), pre-parsed
// once per language and cached.
function wmSourceData() {
  if (currentLang === "de") return window.SYN_ANT_DE || [];
  if (currentLang === "fr") return window.SYN_ANT_FR || [];
  return window.SYN_ANT_EN || [];
}

function wmBuildPool() {
  var cached = wmPoolCache[currentLang];
  if (cached) return cached;
  var set = wmSourceData();
  var pool = [];
  set.forEach(function (w) {
    if (!w || !w.word) return;
    var types = {};
    var any = false;
    WM_TYPES.forEach(function (t) {
      var list = wmSplitField(w[t]);
      if (list.length) {
        types[t] = list;
        any = true;
      }
    });
    if (!any) return;
    pool.push({ entry: w, types: types });
  });
  wmPoolCache[currentLang] = pool;
  return pool;
}

// Subset of wmBuildPool() restricted to the header's CURRENTLY selected
// Level + Category, exactly like Flashcards/Quiz/etc. filter WORD_SETS --
// a level with no CEFR meaning for this dataset (Mix, TOEFL, Phrasal
// Verbs, Partikelverb, German telc sub-levels...) is treated as "no level
// filter", same as picking Mix.
function wmLevelCategoryFilteredPool() {
  var pool = wmBuildPool();
  var lvl = wmNormalizeLevel(currentLevel);
  var cat = currentCategory;
  return pool.filter(function (item) {
    if (lvl && item.entry.level !== lvl) return false;
    if (cat && cat !== "Mix" && item.entry.category !== cat) return false;
    return true;
  });
}

// Subset of wmLevelCategoryFilteredPool() restricted to items that have
// the CURRENTLY selected Synonym/Antonym type (wmTypeFilter), so every
// question drawn from it is guaranteed to match the user's choice.
function wmFilteredPool() {
  return wmLevelCategoryFilteredPool().filter(function (item) {
    return !!item.types[wmTypeFilter];
  });
}

// Backs the header Category filter's chip counts on THIS page. Word Morph
// filters its own synonym/antonym set (not the shared CEFR WORD_SETS), so the
// shared count logic reads from here instead: the pool restricted to the
// normalized Level + the active Synonym/Antonym type (but NOT category -- the
// chips break the pool down BY category), returned as the raw entries so their
// `.category` can be tallied. This makes "Sports · N" match exactly what a
// round would draw when Sports is picked.
function wmCategoryCountWords() {
  var lvl = wmNormalizeLevel(currentLevel);
  return wmBuildPool()
    .filter(function (item) {
      if (lvl && item.entry.level !== lvl) return false;
      return !!item.types[wmTypeFilter];
    })
    .map(function (item) {
      return item.entry;
    });
}
setCategoryWordsProvider(wmCategoryCountWords);
// The footer count must report THIS page's pool, not the CEFR WORD_SETS total.
setPoolCountProvider(function () {
  return wmFilteredPool().length;
});

function refreshWordMorphStart() {
  var pool = wmFilteredPool();
  refreshPoolCount();
  var ok = pool.length >= WM_ROUND_SIZE;
  setText("wordmorph-start-level", WM_LANG_LABEL[currentLang] || WM_LANG_LABEL.en);
  setText(
    "wordmorph-start-count",
    ok
      ? pool.length +
          (pool.length === 1 ? " word" : " words") +
          " · " +
          WM_TYPE_LABEL[wmTypeFilter] +
          " · " +
          WM_ROUND_SIZE +
          " per round"
      : ""
  );
  setHidden("wordmorph-start-warning", ok);
  if (!ok) {
    setText(
      "wordmorph-start-warning",
      "Not enough " +
        WM_TYPE_LABEL[wmTypeFilter].toLowerCase() +
        " words here — try Mix level or category."
    );
  }
  var btn = $("wordmorph-start-btn");
  if (btn) btn.disabled = !ok;
}

function setWordMorphTypeFilter(type) {
  if (type !== "synonyms" && type !== "antonyms") return;
  wmTypeFilter = type;
  var wrap = $("wordmorph-type-picker");
  if (wrap) {
    wrap.querySelectorAll(".wm-type-btn").forEach(function (btn) {
      btn.classList.toggle("is-active", btn.getAttribute("data-type") === type);
    });
  }
  refreshWordMorphStart();
}

function showWordMorphSetup() {
  wmActive = false;
  setPlayHeader(false);
  refreshWordMorphStart();
  setHidden("wordmorph-game", true);
  setHidden("wordmorph-round-result", true);
  setHidden("wordmorph-setup", false);
}

function resetWordMorph() {
  wmActive = false;
  wmRoundsWon = 0;
  wmRoundsLost = 0;
  showWordMorphSetup();
}

function enterWordMorph() {
  if (!wmActive) showWordMorphSetup();
}

function wmPickType(item) {
  // Rounds are built from wmFilteredPool(), so every item is guaranteed to
  // have the user's selected type -- no need to pick randomly among types.
  if (item.types[wmTypeFilter]) return wmTypeFilter;
  var keys = Object.keys(item.types);
  return keys.length ? keys[Math.floor(Math.random() * keys.length)] : null;
}

// Distractors are drawn from ANY relation-type value belonging to OTHER
// words in the same pool (not just the same type) so that words with a
// short synonym/antonym list still reliably produce 3 plausible wrong
// answers.
function wmBuildOptions(item, type) {
  var correctList = item.types[type];
  if (!correctList || !correctList.length) return null;
  var correct = correctList[Math.floor(Math.random() * correctList.length)];
  var seen = {};
  seen[wmFold(correct)] = true;
  var bag = [];
  wmBuildPool().forEach(function (p) {
    if (p.entry === item.entry) return;
    WM_TYPES.forEach(function (t) {
      (p.types[t] || []).forEach(function (v) {
        var f = wmFold(v);
        if (seen[f]) return;
        seen[f] = true;
        bag.push(v);
      });
    });
  });
  if (bag.length < 3) return null;
  var distractors = shuffle(bag).slice(0, 3);
  return shuffle(
    [{ value: correct, isCorrect: true }].concat(
      distractors.map(function (d) {
        return { value: d, isCorrect: false };
      })
    )
  );
}

var WM_LANG_BADGE = { en: "EN", de: "DE", fr: "FR" };

function startWordMorph() {
  wmActive = true;
  setPlayHeader(true);
  setHidden("wordmorph-setup", true);
  setText("wordmorph-level-badge", WM_LANG_BADGE[currentLang] || "EN");
  startNewRound();
}

// Picks WM_ROUND_SIZE distinct words for a fresh round and loads question 1.
function startNewRound() {
  var pool = wmFilteredPool();
  wmRoundWords = shuffle(pool).slice(0, WM_ROUND_SIZE);
  wmRoundIndex = 0;
  wmRoundScore = 0;
  setHidden("wordmorph-round-result", true);
  setHidden("wordmorph-game", false);
  loadRoundQuestion();
}

function loadRoundQuestion() {
  var poolItem = wmRoundWords[wmRoundIndex];
  var attempts = 0;
  var type = null;
  var options = null;
  while (poolItem && attempts < 6) {
    attempts++;
    type = wmPickType(poolItem);
    if (!type) continue;
    options = wmBuildOptions(poolItem, type);
    if (options) break;
    options = null;
  }
  if (!poolItem || !type || !options) {
    // Extremely unlikely given the eligibility gating, but fall back to a
    // fresh random word from the SAME level/category filter (rather than
    // getting stuck on this question, or jumping to a totally different
    // level/category mid-round).
    var pool = wmLevelCategoryFilteredPool();
    if (!pool.length) {
      showWordMorphSetup();
      return;
    }
    wmRoundWords[wmRoundIndex] = pool[Math.floor(Math.random() * pool.length)];
    loadRoundQuestion();
    return;
  }
  wmItem = poolItem;
  wmType = type;
  wmItem.options = options;
  wmDone = false;
  setText("wordmorph-qnum", wmRoundIndex + 1);
  setText("wordmorph-score", wmRoundScore);
  renderWordMorphItem();
}

function renderWordMorphItem() {
  var it = wmItem;
  var type = wmType;
  setText("wordmorph-pos", it.entry.pos || "");
  var badge = $("wordmorph-type-badge");
  if (badge) {
    badge.textContent = WM_TYPE_LABEL[type];
    badge.className = "morph-type-badge " + type.slice(0, -1);
  }
  setText(
    "wordmorph-prompt",
    "Which word is " + WM_TYPE_ARTICLE[type] + " " + WM_TYPE_LABEL[type].toLowerCase() + " of:"
  );
  setText("wordmorph-word", it.entry.word);
  // 2,769 German entries hold a synonym list where the definition should be
  // (see CONTENT-QUALITY.md). Label those "Similar words" so the hint doesn't
  // present a list of synonyms as if it explained the word.
  renderWordMorphHint(
    "wordmorph-definition",
    isStubDefinition(it.entry.definition) ? "Similar words" : "Definition",
    it.entry.definition
  );
  renderWordMorphHint("wordmorph-example", "Example", it.entry.example);
  var linkDetails = $("wordmorph-link-details");
  if (linkDetails) linkDetails.href = vocabDetailsUrl(it.entry.word);
  var linkExamples = $("wordmorph-link-examples");
  if (linkExamples) linkExamples.href = vocabExamplesUrl(it.entry.word);

  var defHintBtn = $("wordmorph-defhint-btn");
  if (defHintBtn) {
    // Stay in step with renderWordMorphHint(): a placeholder example is
    // rendered as nothing, so it must not keep the button enabled either --
    // otherwise the button opens an empty popover.
    var hasExample = !!it.entry.example && !isPlaceholderExample(it.entry.example);
    defHintBtn.disabled = !it.entry.definition && !hasExample;
  }

  var fb = $("wordmorph-feedback");
  if (fb) {
    fb.textContent = "";
    fb.className = "feedback";
  }
  setHidden("wordmorph-result", true);

  var wmHintBtn = $("wordmorph-hint-btn");
  if (wmHintBtn) wmHintBtn.disabled = false;

  var letters = ["A", "B", "C", "D"];
  var wrap = $("wordmorph-options");
  if (!wrap) return;
  wrap.innerHTML = "";
  it.options.forEach(function (opt, idx) {
    var btn = document.createElement("button");
    btn.type = "button";
    btn.className = "option";
    btn.innerHTML =
      '<span class="key">' + letters[idx] + "</span><span>" +
      escapeHtml(opt.value) + "</span>";
    btn.addEventListener("click", function () {
      answerWordMorph(idx, btn);
    });
    wrap.appendChild(btn);
  });
}

// 50/50: disable two of the wrong options for the current question.
function wmHint() {
  if (wmDone || !wmItem) return;
  var wrap = $("wordmorph-options");
  if (!wrap) return;
  var wrong = [];
  wrap.querySelectorAll(".option").forEach(function (b, i) {
    if (!b.disabled && wmItem.options[i] && !wmItem.options[i].isCorrect) wrong.push(b);
  });
  shuffle(wrong)
    .slice(0, 2)
    .forEach(function (b) {
      b.disabled = true;
      b.classList.add("is-5050");
    });
  var hintBtn = $("wordmorph-hint-btn");
  if (hintBtn) hintBtn.disabled = true;
}

function answerWordMorph(idx, btn) {
  if (wmDone || !wmItem) return;
  wmDone = true;
  var chosen = wmItem.options[idx];
  var isCorrect = !!chosen.isCorrect;

  var wrap = $("wordmorph-options");
  if (wrap) {
    wrap.querySelectorAll(".option").forEach(function (el, i) {
      el.disabled = true;
      if (wmItem.options[i].isCorrect) el.classList.add("correct");
    });
  }
  if (!isCorrect) btn.classList.add("wrong");

  var fb = $("wordmorph-feedback");
  if (fb) {
    fb.textContent = isCorrect ? "Correct!" : "Not quite.";
    fb.className = "feedback " + (isCorrect ? "ok" : "no");
  }

  if (isCorrect) wmRoundScore++;
  setText("wordmorph-score", wmRoundScore);

  stats.answered = (stats.answered || 0) + 1;
  if (isCorrect) stats.correct = (stats.correct || 0) + 1;
  lsSet(STATS_KEY, stats);
  touchStreak();

  showWordMorphResult(isCorrect);
}

// data/synanten.js and data/synantde.js store definition/example as
// "<native-language text>;<Turkish text>" (see their file headers). Renders
// both halves as one labeled line, e.g. "Definition: <native> — <Turkish>".
// Hides the element entirely when the field is empty.
function renderWordMorphHint(id, label, raw) {
  var el = $(id);
  if (!el) return;
  var value = raw || "";
  if (!value || isPlaceholderExample(value)) {
    el.textContent = "";
    setHidden(id, true);
    return;
  }
  var sepIdx = value.indexOf(";");
  var native = sepIdx === -1 ? value : value.slice(0, sepIdx);
  var turkish = sepIdx === -1 ? "" : value.slice(sepIdx + 1);
  el.innerHTML =
    '<span class="hm-answer-label">' +
    escapeHtml(label) +
    ":</span> " +
    escapeHtml(native) +
    (turkish ? " — " + escapeHtml(turkish) : "");
  // Never shown inline any more -- the Hint button reveals this content in
  // the shared floating popover instead (see wordmorph-defhint-btn below).
  setHidden(id, true);
}

function showWordMorphResult(isCorrect) {
  var it = wmItem;
  var type = wmType;
  var w = it.entry;
  var rt = $("wordmorph-result-text");
  if (rt) {
    rt.textContent = isCorrect ? "🎉 Correct!" : "❌ Not quite";
    rt.className = "hangman-result-text " + (isCorrect ? "win" : "lose");
  }
  var ansEl = $("wordmorph-answer");
  if (ansEl) {
    ansEl.innerHTML =
      '<span class="hm-answer-label">' +
      escapeHtml(WM_TYPE_LABEL[type] + (it.types[type].length > 1 ? "s" : "")) +
      ' of "' +
      escapeHtml(w.word) +
      '":</span> <strong>' +
      escapeHtml(it.types[type].join(", ")) +
      "</strong>";
  }
  var isLastQuestion = wmRoundIndex >= WM_ROUND_SIZE - 1;
  setText("wordmorph-next", isLastQuestion ? "See results →" : "Next word →");
  setHidden("wordmorph-result", false);
  speak(w.word);
}

// Ends the current round: tallies Winner (score >= WM_WIN_THRESHOLD) vs Loss,
// and shows the round-result screen with a "play the next 10" option.
function finishRound() {
  var win = wmRoundScore >= WM_WIN_THRESHOLD;
  if (win) wmRoundsWon++;
  else wmRoundsLost++;

  setHidden("wordmorph-game", true);
  setHidden("wordmorph-round-result", false);

  var perfect = wmRoundScore === WM_ROUND_SIZE;
  setText("wordmorph-round-emoji", win ? (perfect ? "🏆" : "🎉") : "📚");
  setText("wordmorph-round-title", win ? "You're a Winner! 🎉" : "Loss — try again?");
  setText("wordmorph-round-score", wmRoundScore + " / " + WM_ROUND_SIZE);
  var barFill = $("wordmorph-round-bar-fill");
  if (barFill) barFill.style.width = (wmRoundScore / WM_ROUND_SIZE) * 100 + "%";
  setText(
    "wordmorph-round-text",
    win
      ? "Great job — you got " + wmRoundScore + " out of " + WM_ROUND_SIZE + " correct."
      : "You got " +
          wmRoundScore +
          " out of " +
          WM_ROUND_SIZE +
          " correct. Get at least " +
          WM_WIN_THRESHOLD +
          " correct to win."
  );
  setText(
    "wordmorph-round-tally",
    "Rounds won: " + wmRoundsWon + " · Rounds lost: " + wmRoundsLost
  );
  logHistory({ type: "wordmorph", lang: currentLang, score: wmRoundScore, total: WM_ROUND_SIZE, win: win });
  maybeRemindProfile();
}

function wireWordMorphTypePicker() {
  var wrap = $("wordmorph-type-picker");
  if (!wrap) return;
  wrap.querySelectorAll(".wm-type-btn").forEach(function (btn) {
    btn.addEventListener("click", function () {
      setWordMorphTypeFilter(btn.getAttribute("data-type"));
    });
  });
}
wireWordMorphTypePicker();

on("wordmorph-start-btn", "click", function () {
  startWordMorph();
});
on("wordmorph-hint-btn", "click", wmHint);
on("wordmorph-defhint-btn", "click", function () {
  var defEl = $("wordmorph-definition");
  var exEl = $("wordmorph-example");
  var parts = [];
  if (defEl && defEl.innerHTML) parts.push('<p class="example">' + defEl.innerHTML + "</p>");
  if (exEl && exEl.innerHTML) parts.push('<p class="example">' + exEl.innerHTML + "</p>");
  if (parts.length) showPopover(parts.join(""));
});
on("wordmorph-back", "click", showWordMorphSetup);
on("wordmorph-change", "click", showWordMorphSetup);
on("wordmorph-audio", "click", function () {
  if (wmItem) speak(wmItem.entry.word);
});
on("wordmorph-round-setup", "click", showWordMorphSetup);
on("wordmorph-round-again", "click", startNewRound);
on("wordmorph-next", "click", function () {
  if (wmRoundIndex >= WM_ROUND_SIZE - 1) {
    finishRound();
  } else {
    wmRoundIndex++;
    loadRoundQuestion();
  }
});
document.addEventListener("keydown", function (e) {
  var g = $("wordmorph-game");
  if (!g || g.hidden || !wmItem || wmDone) return;
  if (e.ctrlKey || e.metaKey || e.altKey) return;
  var map = { 1: 0, 2: 1, 3: 2, 4: 3, a: 0, b: 1, c: 2, d: 3 };
  var k = (e.key || "").toLowerCase();
  if (k in map) {
    var idx = map[k];
    var wrap = $("wordmorph-options");
    var btns = wrap ? wrap.querySelectorAll(".option") : [];
    if (btns[idx] && !btns[idx].disabled) {
      e.preventDefault();
      btns[idx].click();
    }
  }
});

onLevelChange(resetWordMorph);
