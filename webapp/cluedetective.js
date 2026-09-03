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

/* Top Words — Clue Detective progressive-deduction game. Requires shared.js. */
"use strict";

var CD_ROUNDS = 10;
var CD_MAX_CLUES = 5;
var clueDetectiveActive = false;
var cdLevel = null;
var cdRound = 0;
var cdScore = 0;
var cdQueue = [];
var cdCurrent = null;
var cdCluesShown = 0;
var cdWrongGuesses = 0;
var cdAnswered = false;
var cdDone = false;

function cdBestKey(level) {
  return "udsp_cluedetective_best_" + currentLang + "_" + level + "_v1";
}
function cdLoadBest(level) {
  var value = parseInt(localStorage.getItem(cdBestKey(level)), 10);
  return isNaN(value) ? 0 : value;
}
function cdSaveBest(level, score) {
  try {
    localStorage.setItem(cdBestKey(level), String(score));
  } catch (e) {
    /* ignore storage errors (private mode) */
  }
}

function cdStripArticle(word) {
  var patterns = {
    de: /^(der|die|das)\s+/i,
    fr: /^(?:(le|la|les)\s+|l['’])/i,
    it: /^(?:(il|lo|la|i|gli|le)\s+|l['’])/i,
    es: /^(el|la|los|las)\s+/i,
    pt: /^(o|a|os|as)\s+/i,
    nl: /^(de|het)\s+/i,
  };
  var pattern = patterns[currentLang];
  return pattern ? String(word || "").replace(pattern, "").trim() : String(word || "").trim();
}

function cdNormalize(text) {
  return foldText(text)
    .replace(/[’']/g, "'")
    .replace(/\s+/g, " ");
}

function cdEligible(entry) {
  return !!(entry && entry.word && entry.definition && cdStripArticle(entry.word).length >= 2);
}

function refreshClueDetectiveStart() {
  var pool = (WORD_SETS[currentLevel] || []).filter(cdEligible);
  var ok = pool.length >= 5;
  setText("cluedetective-start-level", levelLabel(currentLevel));
  setText(
    "cluedetective-start-count",
    ok ? pool.length + (pool.length === 1 ? " word" : " words") + " available" : ""
  );
  setHidden("cluedetective-start-warning", ok);
  if (!ok) {
    setText(
      "cluedetective-start-warning",
      "Not enough words at this level (needs 5+) — pick another above."
    );
  }
  var button = $("cluedetective-start-btn");
  if (button) button.disabled = !ok;
}

function showClueDetectiveSetup() {
  clueDetectiveActive = false;
  setPlayHeader(false);
  refreshClueDetectiveStart();
  setHidden("cluedetective-game", true);
  setHidden("cluedetective-setup", false);
}

function resetClueDetective() {
  clueDetectiveActive = false;
  showClueDetectiveSetup();
}

function startClueDetective(level) {
  var pool = (WORD_SETS[level] || []).filter(cdEligible);
  if (pool.length < 5) return;

  cdLevel = level;
  clueDetectiveActive = true;
  cdDone = false;
  cdAnswered = false;
  cdRound = 0;
  cdScore = 0;
  cdQueue = shuffle(pool.slice());
  setPlayHeader(true);
  setHidden("cluedetective-setup", true);
  setHidden("cluedetective-game", false);
  setHidden("cluedetective-result", true);
  setText("cluedetective-level-badge", levelLabel(level));
  setText("cluedetective-score", "0");
  cdNextRound();
}

function cdNextRound() {
  if (cdDone) return;
  if (cdRound >= CD_ROUNDS) {
    endClueDetective();
    return;
  }
  if (!cdQueue.length) {
    cdQueue = shuffle((WORD_SETS[cdLevel] || []).filter(cdEligible));
  }

  cdRound++;
  cdCurrent = cdQueue.pop();
  cdCluesShown = 0;
  cdWrongGuesses = 0;
  cdAnswered = false;
  setText("cluedetective-progress", "Case " + cdRound + " / " + CD_ROUNDS);
  setText("cluedetective-feedback", "");
  setText("cluedetective-answer", "");
  setHidden("cluedetective-answer", true);
  setHidden("cluedetective-next", true);
  var clueList = $("cluedetective-clues");
  if (clueList) clueList.innerHTML = "";
  var input = $("cluedetective-input");
  if (input) {
    input.value = "";
    input.disabled = false;
    input.focus();
  }
  var submit = $("cluedetective-submit");
  if (submit) submit.disabled = false;
  var skip = $("cluedetective-skip");
  if (skip) skip.disabled = false;
  var clue = $("cluedetective-clue-btn");
  if (clue) clue.disabled = false;
  cdRevealClue();
}

function cdClueText(stage) {
  var bare = cdStripArticle(cdCurrent.word);
  if (stage === 1) return "Word class: " + (cdCurrent.pos || "unknown");
  if (stage === 2) return "Topic: " + (cdCurrent.category || "General");
  if (stage === 3) {
    var wordCount = bare.split(/\s+/).length;
    var letterCount = bare.replace(/\s+/g, "").length;
    return letterCount + " letters" + (wordCount > 1 ? " · " + wordCount + " words" : "");
  }
  if (stage === 4) return "Starts with: " + bare.slice(0, 1).toUpperCase();
  return "Meaning: " + cdCurrent.definition;
}

function cdPointsAvailable() {
  return Math.max(1, CD_MAX_CLUES + 1 - cdCluesShown - cdWrongGuesses);
}

function cdRevealClue() {
  if (cdDone || cdAnswered || !cdCurrent || cdCluesShown >= CD_MAX_CLUES) return;
  cdCluesShown++;
  var list = $("cluedetective-clues");
  if (list) {
    var item = document.createElement("li");
    item.innerHTML =
      '<span class="cd-clue-number">' +
      cdCluesShown +
      '</span><span>' +
      escapeHtml(cdClueText(cdCluesShown)) +
      "</span>";
    list.appendChild(item);
  }
  setText("cluedetective-points", cdPointsAvailable() + " points available");
  var button = $("cluedetective-clue-btn");
  if (button) button.disabled = cdCluesShown >= CD_MAX_CLUES;
}

function cdSubmit() {
  if (cdDone || cdAnswered || !cdCurrent) return;
  var input = $("cluedetective-input");
  if (!input || !input.value.trim()) return;
  var guess = cdNormalize(input.value);
  var full = cdNormalize(cdCurrent.word);
  var bare = cdNormalize(cdStripArticle(cdCurrent.word));

  if (guess === full || guess === bare) {
    cdAnswered = true;
    var points = cdPointsAvailable();
    cdScore += points;
    answeredWord(cdCurrent, true);
    setText("cluedetective-score", String(cdScore));
    cdFinishCase("✓ Solved · " + points + " points", true);
    return;
  }

  cdWrongGuesses++;
  input.value = "";
  input.focus();
  setText("cluedetective-feedback", "Not that word · Try another clue");
  setText("cluedetective-points", cdPointsAvailable() + " points available");
  if (cdCluesShown < CD_MAX_CLUES) cdRevealClue();
}

function cdSkip() {
  if (cdDone || cdAnswered || !cdCurrent) return;
  cdAnswered = true;
  answeredWord(cdCurrent, false);
  cdFinishCase("Skipped · Pas geçildi", false);
}

function cdFinishCase(message, solved) {
  var input = $("cluedetective-input");
  if (input) input.disabled = true;
  var submit = $("cluedetective-submit");
  if (submit) submit.disabled = true;
  var skip = $("cluedetective-skip");
  if (skip) skip.disabled = true;
  var clue = $("cluedetective-clue-btn");
  if (clue) clue.disabled = true;
  setText("cluedetective-feedback", message);
  setText("cluedetective-answer", cdCurrent.word);
  setHidden("cluedetective-answer", false);
  setHidden("cluedetective-next", false);
  if (solved) speak(cdCurrent.word);
}

function endClueDetective() {
  if (cdDone) return;
  cdDone = true;
  var best = cdLoadBest(cdLevel);
  var isNewBest = cdScore > best;
  if (isNewBest) cdSaveBest(cdLevel, cdScore);
  setText("cluedetective-final-score", cdScore + " / " + CD_ROUNDS * CD_MAX_CLUES);
  var bestElement = $("cluedetective-final-best");
  if (bestElement) {
    bestElement.textContent = isNewBest
      ? "🏆 New best!"
      : "Best: " + Math.max(best, cdScore) + " / " + CD_ROUNDS * CD_MAX_CLUES;
  }
  setHidden("cluedetective-result", false);
}

on("cluedetective-start-btn", "click", function () {
  startClueDetective(currentLevel);
});
on("cluedetective-submit", "click", cdSubmit);
on("cluedetective-clue-btn", "click", cdRevealClue);
on("cluedetective-skip", "click", cdSkip);
on("cluedetective-next", "click", cdNextRound);
on("cluedetective-back", "click", showClueDetectiveSetup);
on("cluedetective-change", "click", showClueDetectiveSetup);
on("cluedetective-restart", "click", function () {
  startClueDetective(cdLevel);
});

document.addEventListener("keydown", function (event) {
  var input = $("cluedetective-input");
  if (event.key === "Enter" && input && document.activeElement === input) {
    event.preventDefault();
    cdSubmit();
  }
});

onLevelChange(resetClueDetective);
