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

/* Top Words — Survival Streak game page logic. Requires shared.js. */
"use strict";

var survivalActive = false;
var survLevel = null;
var survStreak = 0;
var survDone = false;
var survAnswered = false;
var survQuestion = null;

function survBestKey(level) {
  return "udsp_survival_best_" + currentLang + "_" + level + "_v1";
}
function survLoadBest(level) {
  var v = parseInt(localStorage.getItem(survBestKey(level)), 10);
  return isNaN(v) ? 0 : v;
}
function survSaveBest(level, streak) {
  try {
    localStorage.setItem(survBestKey(level), String(streak));
  } catch (e) {
    /* ignore storage errors (private mode) */
  }
}
function survEligible(w) {
  return !!(w && w.word && w.definition);
}

function refreshSurvivalStart() {
  var pool = (WORD_SETS[currentLevel] || []).filter(survEligible);
  var ok = pool.length >= 4;
  setText("survival-start-level", levelLabel(currentLevel));
  setText(
    "survival-start-count",
    ok ? pool.length + (pool.length === 1 ? " word" : " words") + " available" : ""
  );
  setHidden("survival-start-warning", ok);
  if (!ok) {
    setText(
      "survival-start-warning",
      "Not enough words at this level (needs 4+) — pick another above."
    );
  }
  var btn = $("survival-start-btn");
  if (btn) btn.disabled = !ok;
}

function showSurvivalSetup() {
  survivalActive = false;
  setPlayHeader(false);
  refreshSurvivalStart();
  setHidden("survival-game", true);
  setHidden("survival-setup", false);
}

function resetSurvival() {
  survivalActive = false;
  showSurvivalSetup();
}

function enterSurvival() {
  if (!survivalActive) showSurvivalSetup();
}

function startSurvival(level) {
  survLevel = level;
  survivalActive = true;
  survDone = false;
  setPlayHeader(true);
  setHidden("survival-setup", true);
  setHidden("survival-game", false);
  setHidden("survival-result", true);
  setText("survival-level-badge", levelLabel(level));
  survStreak = 0;
  setText("survival-streak-count", "0");
  survNextQuestion();
}

function survBuildQuestion(level) {
  var pool = (WORD_SETS[level] || []).filter(survEligible);
  if (pool.length < 4) return null;
  var idx = Math.floor(Math.random() * pool.length);
  var correct = pool[idx];
  var distractors = shuffle(
    pool.filter(function (w) {
      return w !== correct;
    })
  ).slice(0, 3);
  var options = shuffle([correct].concat(distractors));
  return {
    word: correct.word,
    // Kept so answering can grade the actual word, not just the option string.
    entry: correct,
    pos: correct.pos,
    answer: correct.definition,
    options: options.map(function (o) {
      return o.definition;
    }),
  };
}

function survNextQuestion() {
  if (survDone) return;
  var q = survBuildQuestion(survLevel);
  if (!q) {
    endSurvival();
    return;
  }
  survQuestion = q;
  survAnswered = false;
  var survHintBtn = $("survival-hint-btn");
  if (survHintBtn) survHintBtn.disabled = false;
  setText("survival-word", q.word);
  setText("survival-word-pos", q.pos || "");
  var box = $("survival-options");
  if (box) {
    box.innerHTML = "";
    q.options.forEach(function (opt) {
      var b = document.createElement("button");
      b.type = "button";
      b.className = "option";
      b.textContent = opt;
      b.addEventListener("click", function () {
        survAnswer(opt, b);
      });
      box.appendChild(b);
    });
  }
}

function survAnswer(picked, btnEl) {
  if (survAnswered || survDone) return;
  survAnswered = true;
  var box = $("survival-options");
  var correct = picked === survQuestion.answer;
  answeredWord(survQuestion.entry, correct);
  if (box) {
    box.querySelectorAll(".option").forEach(function (b) {
      b.disabled = true;
      if (b.textContent === survQuestion.answer) b.classList.add("correct");
      else if (b === btnEl) b.classList.add("wrong");
    });
  }
  if (correct) {
    survStreak++;
    setText("survival-streak-count", survStreak);
    setTimeout(function () {
      if (!survDone) survNextQuestion();
    }, 350);
  } else {
    setTimeout(function () {
      endSurvival();
    }, 900);
  }
}

// 50/50: disable two of the wrong options for the current question.
function survHint() {
  if (survAnswered || survDone || !survQuestion) return;
  var box = $("survival-options");
  if (!box) return;
  var wrong = [];
  box.querySelectorAll(".option").forEach(function (b) {
    if (!b.disabled && b.textContent !== survQuestion.answer) wrong.push(b);
  });
  shuffle(wrong)
    .slice(0, 2)
    .forEach(function (b) {
      b.disabled = true;
      b.classList.add("is-5050");
    });
  var hintBtn = $("survival-hint-btn");
  if (hintBtn) hintBtn.disabled = true;
}

function endSurvival() {
  survDone = true;
  var best = survLoadBest(survLevel);
  var isNewBest = survStreak > best;
  if (isNewBest) survSaveBest(survLevel, survStreak);
  setText("survival-final-streak", survStreak);
  var bestEl = $("survival-final-best");
  if (bestEl) {
    bestEl.textContent = isNewBest
      ? "🏆 New best!"
      : "Best streak: " + Math.max(best, survStreak);
  }
  setHidden("survival-result", false);
}

on("survival-start-btn", "click", function () {
  startSurvival(currentLevel);
});
on("survival-hint-btn", "click", survHint);
on("survival-audio", "click", function () {
  if (survQuestion) speak(survQuestion.word);
});
on("survival-back", "click", showSurvivalSetup);
on("survival-change", "click", showSurvivalSetup);
on("survival-restart", "click", function () {
  startSurvival(survLevel);
});

document.addEventListener("keydown", function (e) {
  var g = $("survival-game");
  if (!g || g.hidden || survDone) return;
  if (e.ctrlKey || e.metaKey || e.altKey) return;
  var map = { 1: 0, 2: 1, 3: 2, 4: 3 };
  var k = e.key;
  if (k in map) {
    var idx = map[k];
    var wrap = $("survival-options");
    var btns = wrap ? wrap.querySelectorAll(".option") : [];
    if (btns[idx] && !btns[idx].disabled) {
      e.preventDefault();
      btns[idx].click();
    }
  }
});

onLevelChange(resetSurvival);
