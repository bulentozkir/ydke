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

/* Top Words — Word Race (typing challenge) page logic. Requires shared.js. */
"use strict";

var WR_DURATION = 60; // seconds
var WR_BONUS = 2; // seconds gained per correct answer
var wordraceActive = false;
var wrLevel = null;
var wrTimeLeft = 0;
var wrTimerInterval = null;
var wrScore = 0;
var wrDone = false;
var wrAnswered = false;
var wrWord = null;
var wrHintCount = 0;

function wrBestKey(level) {
  return "udsp_wordrace_best_" + currentLang + "_" + level + "_v1";
}
function wrLoadBest(level) {
  var v = parseInt(localStorage.getItem(wrBestKey(level)), 10);
  return isNaN(v) ? 0 : v;
}
function wrSaveBest(level, score) {
  try {
    localStorage.setItem(wrBestKey(level), String(score));
  } catch (e) {
    /* ignore storage errors (private mode) */
  }
}
function wrEligible(w) {
  return !!(w && w.word && w.word.trim() && w.definition);
}

function refreshWordRaceStart() {
  var set = (WORD_SETS[currentLevel] || []).filter(wrEligible);
  var ok = set.length >= 5;
  setText("wordrace-start-level", levelLabel(currentLevel));
  setText(
    "wordrace-start-count",
    ok ? set.length + (set.length === 1 ? " word" : " words") + " available" : ""
  );
  setHidden("wordrace-start-warning", ok);
  if (!ok) {
    setText(
      "wordrace-start-warning",
      "Not enough words at this level (needs 5+) — pick another above."
    );
  }
  var btn = $("wordrace-start-btn");
  if (btn) btn.disabled = !ok;
}

function showWordRaceSetup() {
  wordraceActive = false;
  setPlayHeader(false);
  stopWordRaceTimer();
  refreshWordRaceStart();
  setHidden("wordrace-game", true);
  setHidden("wordrace-setup", false);
}

function resetWordRace() {
  wordraceActive = false;
  stopWordRaceTimer();
  showWordRaceSetup();
}

function enterWordRace() {
  if (!wordraceActive) showWordRaceSetup();
}

function startWordRace(level) {
  wrLevel = level;
  wordraceActive = true;
  wrDone = false;
  setPlayHeader(true);
  setHidden("wordrace-setup", true);
  setHidden("wordrace-game", false);
  setHidden("wordrace-result", true);
  setText("wordrace-level-badge", levelLabel(level));
  wrScore = 0;
  wrTimeLeft = WR_DURATION;
  setText("wordrace-score", "0");
  renderWordRaceTimer();
  startWordRaceTimer();
  wrNextWord();
}

function startWordRaceTimer() {
  stopWordRaceTimer();
  wrTimerInterval = setInterval(function () {
    wrTimeLeft--;
    if (wrTimeLeft <= 0) {
      wrTimeLeft = 0;
      renderWordRaceTimer();
      endWordRace();
      return;
    }
    renderWordRaceTimer();
  }, 1000);
}
function stopWordRaceTimer() {
  if (wrTimerInterval) {
    clearInterval(wrTimerInterval);
    wrTimerInterval = null;
  }
}
function renderWordRaceTimer() {
  var el = $("wordrace-timer");
  if (!el) return;
  el.textContent = wrTimeLeft + "s";
  el.classList.toggle("is-low", wrTimeLeft <= 10);
}

function wrNextWord() {
  if (wrDone) return;
  var pool = (WORD_SETS[wrLevel] || []).filter(wrEligible);
  if (!pool.length) {
    endWordRace();
    return;
  }
  wrWord = pool[Math.floor(Math.random() * pool.length)];
  wrAnswered = false;
  wrHintCount = 0;
  setHidden("wordrace-hint", true);
  setText("wordrace-hint", "");
  var wrHintBtn = $("wordrace-hint-btn");
  if (wrHintBtn) wrHintBtn.disabled = false;
  setText("wordrace-pos", wrWord.pos || "");
  setText("wordrace-clue", wrWord.definition || "");
  var input = $("wordrace-input");
  if (input) {
    input.value = "";
    input.classList.remove("is-correct", "is-wrong");
    input.disabled = false;
    input.focus();
  }
  var fb = $("wordrace-feedback");
  if (fb) {
    fb.textContent = "";
    fb.className = "feedback";
  }
  setHidden("wordrace-audio", true);
}

function wrSubmit() {
  if (wrDone || wrAnswered) return;
  var input = $("wordrace-input");
  if (!input || !input.value.trim()) return;
  wrAnswered = true;
  input.disabled = true;
  var correct = fuzzyMatch(input.value, wrWord.word);
  answeredWord(wrWord, correct);
  var fb = $("wordrace-feedback");
  if (correct) {
    input.classList.add("is-correct");
    wrScore++;
    wrTimeLeft += WR_BONUS;
    setText("wordrace-score", wrScore);
    renderWordRaceTimer();
    if (fb) {
      fb.textContent = "✅ " + wrWord.word;
      fb.className = "feedback ok";
    }
  } else {
    input.classList.add("is-wrong");
    if (fb) {
      fb.textContent = "❌ " + wrWord.word;
      fb.className = "feedback no";
    }
  }
  setHidden("wordrace-audio", false);
  speak(wrWord.word);
  setTimeout(function () {
    if (!wrDone) wrNextWord();
  }, 700);
}

function wrSkip() {
  if (wrDone || wrAnswered) return;
  wrAnswered = true;
  var input = $("wordrace-input");
  if (input) input.disabled = true;
  var fb = $("wordrace-feedback");
  if (fb) {
    fb.textContent = wrWord.word;
    fb.className = "feedback";
  }
  setHidden("wordrace-audio", false);
  speak(wrWord.word);
  setTimeout(function () {
    if (!wrDone) wrNextWord();
  }, 600);
}

// Progressive hint: each click reveals one more leading letter of the word.
function wrHint() {
  if (wrDone || wrAnswered || !wrWord) return;
  var word = wrWord.word;
  var maxReveal = Math.max(1, word.length - 1);
  if (wrHintCount >= maxReveal) return;
  wrHintCount++;
  var text = "Starts with: " + word.slice(0, wrHintCount) + "…";
  setText("wordrace-hint", text);
  if (wrHintCount >= maxReveal) {
    var btn = $("wordrace-hint-btn");
    if (btn) btn.disabled = true;
  }
  showPopover('<p class="example">' + escapeHtml(text) + "</p>");
}

function endWordRace() {
  wrDone = true;
  stopWordRaceTimer();
  var input = $("wordrace-input");
  if (input) input.disabled = true;
  var best = wrLoadBest(wrLevel);
  var isNewBest = wrScore > best;
  if (isNewBest) wrSaveBest(wrLevel, wrScore);
  setText("wordrace-final-score", wrScore);
  var bestEl = $("wordrace-final-best");
  if (bestEl) {
    bestEl.textContent = isNewBest ? "🏆 New best!" : "Best: " + Math.max(best, wrScore);
  }
  setHidden("wordrace-result", false);
}

on("wordrace-submit", "click", wrSubmit);
on("wordrace-skip", "click", wrSkip);
on("wordrace-hint-btn", "click", wrHint);
on("wordrace-audio", "click", function () {
  if (wrWord) speak(wrWord.word);
});
on("wordrace-start-btn", "click", function () {
  startWordRace(currentLevel);
});
on("wordrace-back", "click", showWordRaceSetup);
on("wordrace-change", "click", showWordRaceSetup);
on("wordrace-restart", "click", function () {
  startWordRace(wrLevel);
});

document.addEventListener("keydown", function (e) {
  var input = $("wordrace-input");
  if (!input || document.activeElement !== input) return;
  if (e.key === "Enter") {
    e.preventDefault();
    wrSubmit();
  }
});

document.addEventListener("visibilitychange", function () {
  if (!wordraceActive || wrDone) return;
  if (document.visibilityState === "hidden") stopWordRaceTimer();
  else startWordRaceTimer();
});

onLevelChange(resetWordRace);
