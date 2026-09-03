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

/* Top Words — Reading Comprehension game page logic. Requires shared.js.
 *
 * Data sources (all ORIGINAL -- not scraped/copied from any third-party
 * site):
 * - English by grade: window.READING_PASSAGES (data/readingcomp.js),
 *   passages keyed by school `grade` (1-12). This is the original track.
 * - English by CEFR level: window.READING_PASSAGES_EN_CEFR
 *   (data/readingcompencefr.js), passages keyed by CEFR `level` (A1-C2) --
 *   test-english.com/reading/ was consulted only for structural
 *   inspiration; its Terms of Use explicitly forbid copying/republishing
 *   its actual texts/questions, so those were NOT used as a source.
 * - German by CEFR level: window.READING_PASSAGES_DE
 *   (data/readingcompde.js), passages keyed by CEFR `level` (A1-C2) --
 *   lingua.com / german.net were consulted the same way, same restriction.
 * English has TWO parallel tracks (grade vs CEFR level), toggled via the
 * "By Grade" / "By CEFR Level" pills (#rc-mode-toggle, English only);
 * German only has the CEFR track. Like Word Morph, this game deliberately
 * does NOT use the CEFR data/words*.js files or WORD_SETS/currentLevel at
 * all; those scripts are still loaded on this page only so shared.js's
 * startApp() bootstrap (which requires at least one non-empty
 * window.WORDS_* global) has something to work with. The header's
 * language toggle (#langs-nav) IS used here though, to switch between the
 * English/German passage sets; only the CEFR word-level nav (#levels-nav)
 * stays hidden.
 */
"use strict";

var RC_ROUND_PASS_RATIO = 0.6; // >= 60% correct = "Great job!", else "Keep practicing"
var RC_CEFR_ORDER = ["A1", "A2", "B1", "B2", "C1", "C2"]; // canonical CEFR display order

var rcActive = false;
var rcEnMode = "grade"; // English only: "grade" (1-12) or "cefr" (A1-C2)
var rcGradeEn = 1; // selected English grade (1-12)
var rcLevelEnCefr = "A1"; // selected English CEFR level (A1-C2)
var rcLevelDe = "A1"; // selected German CEFR level (A1-C2)
var rcPassage = null; // the passage object currently being read
var rcUsedPassages = {}; // "sourceKey|key" -> { passageIndex: true } already seen this session
var rcQuestionIndex = 0;
var rcRoundSize = 5; // number of questions in the current passage's round (set per passage)
var rcScore = 0;
var rcDone = false;
var rcHintUsed = false; // whether the Hint button has been used for the current question
var rcIndex = {}; // cache: rcIndex["en-grade"] = { 1: [passage,...] }, rcIndex["en-cefr"] = {...}, rcIndex.de = {...}

// Which data array/key field is active depends on the header's language
// toggle plus (English only) the Grade/CEFR mode toggle.
function rcSourceKey() {
  if (currentLang === "de") return "de";
  return "en-" + rcEnMode;
}
function rcIsCefrMode() {
  return currentLang === "de" || rcEnMode === "cefr";
}
function rcSourceArray() {
  if (currentLang === "de") return window.READING_PASSAGES_DE || [];
  if (rcEnMode === "cefr") return window.READING_PASSAGES_EN_CEFR || [];
  return window.READING_PASSAGES || [];
}
function rcKeyField() {
  return rcIsCefrMode() ? "level" : "grade";
}
function rcCurrentKey() {
  if (currentLang === "de") return rcLevelDe;
  return rcEnMode === "cefr" ? rcLevelEnCefr : rcGradeEn;
}
function rcSetCurrentKey(k) {
  if (currentLang === "de") rcLevelDe = k;
  else if (rcEnMode === "cefr") rcLevelEnCefr = k;
  else rcGradeEn = k;
}
function rcKeyLabel(k) {
  return rcIsCefrMode() ? String(k) : "Grade " + k;
}

function rcBuildIndex() {
  var key = rcSourceKey();
  if (rcIndex[key]) return;
  var idx = {};
  var field = rcKeyField();
  rcSourceArray().forEach(function (p) {
    if (!p || !p[field]) return;
    if (!idx[p[field]]) idx[p[field]] = [];
    idx[p[field]].push(p);
  });
  rcIndex[key] = idx;
}

function rcKeysAvailable() {
  rcBuildIndex();
  var idx = rcIndex[rcSourceKey()] || {};
  if (rcIsCefrMode()) {
    return RC_CEFR_ORDER.filter(function (lvl) {
      return idx[lvl] && idx[lvl].length;
    });
  }
  return Object.keys(idx)
    .map(Number)
    .sort(function (a, b) {
      return a - b;
    });
}

// English-only "By Grade" / "By CEFR Level" mode toggle. Hidden entirely
// for German, which only has one (CEFR) track.
function rcRenderModeToggle() {
  var wrap = $("rc-mode-toggle");
  if (!wrap) return;
  setHidden("rc-mode-toggle", currentLang !== "en");
  var gradeBtn = $("rc-mode-grade");
  var cefrBtn = $("rc-mode-cefr");
  if (gradeBtn) gradeBtn.classList.toggle("is-on", rcEnMode === "grade");
  if (cefrBtn) cefrBtn.classList.toggle("is-on", rcEnMode === "cefr");
}
function rcSetEnMode(mode) {
  if (currentLang !== "en" || rcEnMode === mode) return;
  rcEnMode = mode;
  rcRenderModeToggle();
  rcRenderGradePicker();
  refreshRcStart();
}

function rcRenderGradePicker() {
  var nav = $("rc-grade-picker");
  if (!nav) return;
  var keys = rcKeysAvailable();
  var cur = rcCurrentKey();
  nav.innerHTML = "";
  keys.forEach(function (k) {
    var btn = document.createElement("button");
    btn.type = "button";
    btn.className = "level-btn" + (k === cur ? " is-active" : "");
    btn.textContent = rcKeyLabel(k);
    btn.addEventListener("click", function () {
      rcSetCurrentKey(k);
      rcRenderGradePicker();
      refreshRcStart();
    });
    nav.appendChild(btn);
  });
}

function refreshRcStart() {
  rcBuildIndex();
  var cur = rcCurrentKey();
  var idx = rcIndex[rcSourceKey()] || {};
  var list = idx[cur] || [];
  var ok = list.length > 0;
  setText("rc-start-grade", rcKeyLabel(cur));
  setText(
    "rc-start-count",
    ok ? list.length + (list.length === 1 ? " passage" : " passages") + " available" : ""
  );
  setHidden("rc-start-warning", ok);
  if (!ok) {
    setText("rc-start-warning", "No passages at this level yet.");
  }
  var btn = $("rc-start-btn");
  if (btn) btn.disabled = !ok;
}

function showRcSetup() {
  rcActive = false;
  setPlayHeader(false);
  rcRenderModeToggle();
  rcRenderGradePicker();
  refreshRcStart();
  setHidden("rc-game", true);
  setHidden("rc-round-result", true);
  setHidden("rc-setup", false);
}

function resetRc() {
  rcActive = false;
  showRcSetup();
}

function enterRc() {
  if (!rcActive) showRcSetup();
}

// Picks a passage for the current grade/level that hasn't been read yet
// this session; once every passage for that grade/level has been seen, the
// "seen" set resets so passages can repeat rather than the game getting stuck.
function rcPickPassage() {
  rcBuildIndex();
  var cur = rcCurrentKey();
  var idx = rcIndex[rcSourceKey()] || {};
  var list = idx[cur] || [];
  if (!list.length) return null;
  var usedKey = rcSourceKey() + "|" + cur;
  var used = rcUsedPassages[usedKey] || (rcUsedPassages[usedKey] = {});
  var remaining = list.filter(function (_, i) {
    return !used[i];
  });
  if (!remaining.length) {
    used = rcUsedPassages[usedKey] = {};
    remaining = list.slice();
  }
  var chosen = remaining[Math.floor(Math.random() * remaining.length)];
  used[list.indexOf(chosen)] = true;
  return chosen;
}

function startRc() {
  rcActive = true;
  setPlayHeader(true);
  setHidden("rc-setup", true);
  setText("rc-grade-badge", rcKeyLabel(rcCurrentKey()));
  startRcPassage();
}

function startRcPassage() {
  rcPassage = rcPickPassage();
  if (!rcPassage) {
    showRcSetup();
    return;
  }
  rcQuestionIndex = 0;
  rcRoundSize = rcPassage.questions.length;
  rcScore = 0;
  setHidden("rc-round-result", true);
  setHidden("rc-game", false);
  setText("rc-passage-title", rcPassage.title);
  setText("rc-passage-text", rcPassage.text);
  loadRcQuestion();
}

function loadRcQuestion() {
  var q = rcPassage.questions[rcQuestionIndex];
  rcDone = false;
  setText("rc-qnum", rcQuestionIndex + 1);
  setText("rc-score", rcScore);
  setText("rc-question", q.q);

  var fb = $("rc-feedback");
  if (fb) {
    fb.textContent = "";
    fb.className = "feedback";
  }
  setHidden("rc-result", true);

  rcHintUsed = false;
  setHidden("rc-hint-text", true);
  setText("rc-hint-text", "");
  setHidden("rc-explain-text", true);
  setText("rc-explain-text", "");
  var hintBtn = $("rc-hint-btn");
  if (hintBtn) hintBtn.disabled = !q.hint;
  var explainBtn = $("rc-explain-btn");
  if (explainBtn) explainBtn.disabled = true;

  var letters = ["A", "B", "C", "D"];
  var wrap = $("rc-options");
  if (!wrap) return;
  wrap.innerHTML = "";
  q.options.forEach(function (opt, idx) {
    var btn = document.createElement("button");
    btn.type = "button";
    btn.className = "option";
    btn.innerHTML =
      '<span class="key">' + letters[idx] + "</span><span>" + escapeHtml(opt) + "</span>";
    btn.addEventListener("click", function () {
      answerRc(idx, btn);
    });
    wrap.appendChild(btn);
  });
}

function answerRc(idx, btn) {
  if (rcDone || !rcPassage) return;
  rcDone = true;
  var q = rcPassage.questions[rcQuestionIndex];
  var isCorrect = idx === q.correct;

  var wrap = $("rc-options");
  if (wrap) {
    wrap.querySelectorAll(".option").forEach(function (el, i) {
      el.disabled = true;
      if (i === q.correct) el.classList.add("correct");
    });
  }
  if (!isCorrect) btn.classList.add("wrong");

  var fb = $("rc-feedback");
  if (fb) {
    fb.textContent = isCorrect ? "Correct!" : "Not quite.";
    fb.className = "feedback " + (isCorrect ? "ok" : "no");
  }

  if (isCorrect) rcScore++;
  setText("rc-score", rcScore);

  stats.answered = (stats.answered || 0) + 1;
  if (isCorrect) stats.correct = (stats.correct || 0) + 1;
  lsSet(STATS_KEY, stats);
  touchStreak();

  var hintBtn = $("rc-hint-btn");
  if (hintBtn) hintBtn.disabled = true;
  var explainBtn = $("rc-explain-btn");
  if (explainBtn) explainBtn.disabled = !q.explain;

  showRcResult(isCorrect);
}

function showRcResult(isCorrect) {
  var rt = $("rc-result-text");
  if (rt) {
    rt.textContent = isCorrect ? "🎉 Correct!" : "❌ Not quite";
    rt.className = "hangman-result-text " + (isCorrect ? "win" : "lose");
  }
  var isLastQuestion = rcQuestionIndex >= rcRoundSize - 1;
  setText("rc-next", isLastQuestion ? "See results →" : "Next question →");
  setHidden("rc-result", false);
}

// Ends the current passage: shows a score summary and offers another
// passage from the same grade, or a way back to the grade picker.
function finishRcPassage() {
  var win = rcScore >= Math.ceil(rcRoundSize * RC_ROUND_PASS_RATIO);

  setHidden("rc-game", true);
  setHidden("rc-round-result", false);

  var perfect = rcScore === rcRoundSize;
  setText("rc-round-emoji", win ? (perfect ? "🏆" : "🎉") : "📚");
  setText("rc-round-title", win ? "Great job!" : "Keep practicing!");
  setText("rc-round-score", rcScore + " / " + rcRoundSize);
  var barFill = $("rc-round-bar-fill");
  if (barFill) barFill.style.width = (rcScore / rcRoundSize) * 100 + "%";
  setText(
    "rc-round-text",
    'You got ' +
      rcScore +
      " out of " +
      rcRoundSize +
      ' correct on "' +
      rcPassage.title +
      '".' +
      (win ? "" : " Try another passage to practice more.")
  );
  logHistory({ type: "readingcomp", lang: currentLang, title: rcPassage.title, score: rcScore, total: rcRoundSize, win: win });
  maybeRemindProfile();
}

on("rc-start-btn", "click", function () {
  startRc();
});
on("rc-mode-grade", "click", function () {
  rcSetEnMode("grade");
});
on("rc-mode-cefr", "click", function () {
  rcSetEnMode("cefr");
});
on("rc-back", "click", showRcSetup);
on("rc-change", "click", showRcSetup);
on("rc-round-setup", "click", showRcSetup);
on("rc-round-again", "click", startRcPassage);
on("rc-next", "click", function () {
  if (rcQuestionIndex >= rcRoundSize - 1) {
    finishRcPassage();
  } else {
    rcQuestionIndex++;
    loadRcQuestion();
  }
});
on("rc-hint-btn", "click", function () {
  if (!rcPassage || rcHintUsed) return;
  var q = rcPassage.questions[rcQuestionIndex];
  if (!q.hint) return;
  rcHintUsed = true;
  setText("rc-hint-text", q.hint);
  var btn = $("rc-hint-btn");
  if (btn) btn.disabled = true;
  showPopover('<p class="example">💡 ' + escapeHtml(q.hint) + "</p>");
});
on("rc-explain-btn", "click", function () {
  if (!rcPassage || !rcDone) return;
  var q = rcPassage.questions[rcQuestionIndex];
  if (!q.explain) return;
  setText("rc-explain-text", q.explain);
  showPopover('<p class="example">❓ ' + escapeHtml(q.explain) + "</p>");
});
document.addEventListener("keydown", function (e) {
  var g = $("rc-game");
  if (!g || g.hidden || !rcPassage || rcDone) return;
  if (e.ctrlKey || e.metaKey || e.altKey) return;
  var map = { 1: 0, 2: 1, 3: 2, 4: 3, a: 0, b: 1, c: 2, d: 3 };
  var k = (e.key || "").toLowerCase();
  if (k in map) {
    var idx = map[k];
    var wrap = $("rc-options");
    var btns = wrap ? wrap.querySelectorAll(".option") : [];
    if (btns[idx] && !btns[idx].disabled) {
      e.preventDefault();
      btns[idx].click();
    }
  }
});

onLevelChange(resetRc);
