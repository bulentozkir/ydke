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

/* Top Words — Speed Round game page logic. Requires shared.js. */
"use strict";

var SR_DURATION = 60; // seconds
var SR_BONUS = 2; // seconds gained per correct answer
var speedActive = false;
var srLevel = null;
var srTimeLeft = 0;
var srTimerInterval = null;
var srScore = 0;
var srStreak = 0;
var srAnswered = false;
var srQuestion = null;
var srDone = false;

function srBestKey(level) {
  return "udsp_speedround_best_" + currentLang + "_" + level + "_v1";
}
function srLoadBest(level) {
  var v = parseInt(localStorage.getItem(srBestKey(level)), 10);
  return isNaN(v) ? 0 : v;
}
function srSaveBest(level, score) {
  try {
    localStorage.setItem(srBestKey(level), String(score));
  } catch (e) {
    /* ignore storage errors (private mode) */
  }
}

function refreshSpeedStart() {
  var set = WORD_SETS[currentLevel] || [];
  var ok = set.length >= 5;
  setText("speedround-start-level", levelLabel(currentLevel));
  setText(
    "speedround-start-count",
    ok ? set.length + (set.length === 1 ? " word" : " words") + " available" : ""
  );
  setHidden("speedround-start-warning", ok);
  if (!ok) {
    setText(
      "speedround-start-warning",
      "Not enough words at this level (needs 5+) — pick another above."
    );
  }
  var btn = $("speedround-start-btn");
  if (btn) btn.disabled = !ok;
}

function showSpeedSetup() {
  speedActive = false;
  setPlayHeader(false);
  stopSpeedTimer();
  refreshSpeedStart();
  setHidden("speedround-game", true);
  setHidden("speedround-setup", false);
}

function resetSpeedRound() {
  speedActive = false;
  stopSpeedTimer();
  showSpeedSetup();
}

function enterSpeedRound() {
  if (!speedActive) showSpeedSetup();
}

function startSpeedRound(level) {
  srLevel = level;
  speedActive = true;
  srDone = false;
  setPlayHeader(true);
  setHidden("speedround-setup", true);
  setHidden("speedround-game", false);
  setHidden("speedround-result", true);
  setText("speedround-level-badge", levelLabel(level));
  srScore = 0;
  srStreak = 0;
  srTimeLeft = SR_DURATION;
  setText("speedround-score", "0");
  setText("speedround-streak", "");
  renderSpeedTimer();
  startSpeedTimer();
  srNextQuestion();
}

function startSpeedTimer() {
  stopSpeedTimer();
  srTimerInterval = setInterval(function () {
    srTimeLeft--;
    if (srTimeLeft <= 0) {
      srTimeLeft = 0;
      renderSpeedTimer();
      endSpeedRound();
      return;
    }
    renderSpeedTimer();
  }, 1000);
}
function stopSpeedTimer() {
  if (srTimerInterval) {
    clearInterval(srTimerInterval);
    srTimerInterval = null;
  }
}
function renderSpeedTimer() {
  var el = $("speedround-timer");
  if (!el) return;
  el.textContent = srTimeLeft + "s";
  el.classList.toggle("is-low", srTimeLeft <= 10);
}

function srBuildQuestion(level) {
  var pool = (WORD_SETS[level] || []).filter(function (w) {
    return w && w.word && w.definition;
  });
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

function srNextQuestion() {
  if (srDone) return;
  var q = srBuildQuestion(srLevel);
  if (!q) {
    endSpeedRound();
    return;
  }
  srQuestion = q;
  srAnswered = false;
  var srHintBtn = $("speedround-hint-btn");
  if (srHintBtn) srHintBtn.disabled = false;
  setText("speedround-word", q.word);
  setText("speedround-word-pos", q.pos || "");
  var box = $("speedround-options");
  if (box) {
    box.innerHTML = "";
    q.options.forEach(function (opt) {
      var b = document.createElement("button");
      b.type = "button";
      b.className = "option";
      b.textContent = opt;
      b.addEventListener("click", function () {
        srAnswer(opt, b);
      });
      box.appendChild(b);
    });
  }
}

function srAnswer(picked, btnEl) {
  if (srAnswered || srDone) return;
  srAnswered = true;
  var box = $("speedround-options");
  var correct = picked === srQuestion.answer;
  answeredWord(srQuestion.entry, correct);
  if (box) {
    box.querySelectorAll(".option").forEach(function (b) {
      b.disabled = true;
      if (b.textContent === srQuestion.answer) b.classList.add("correct");
      else if (b === btnEl) b.classList.add("wrong");
    });
  }
  if (correct) {
    srScore++;
    srStreak++;
    srTimeLeft += SR_BONUS;
    setText("speedround-score", srScore);
    setText(
      "speedround-streak",
      srStreak >= 2 ? "🔥 " + srStreak + " in a row!" : ""
    );
    renderSpeedTimer();
  } else {
    srStreak = 0;
    setText("speedround-streak", "");
  }
  setTimeout(function () {
    if (!srDone) srNextQuestion();
  }, 450);
}

// 50/50: disable two of the wrong options for the current question.
function srHint() {
  if (srAnswered || srDone || !srQuestion) return;
  var box = $("speedround-options");
  if (!box) return;
  var wrong = [];
  box.querySelectorAll(".option").forEach(function (b) {
    if (!b.disabled && b.textContent !== srQuestion.answer) wrong.push(b);
  });
  shuffle(wrong)
    .slice(0, 2)
    .forEach(function (b) {
      b.disabled = true;
      b.classList.add("is-5050");
    });
  var hintBtn = $("speedround-hint-btn");
  if (hintBtn) hintBtn.disabled = true;
}

function endSpeedRound() {
  srDone = true;
  stopSpeedTimer();
  var box = $("speedround-options");
  if (box) {
    box.querySelectorAll(".option").forEach(function (b) {
      b.disabled = true;
    });
  }
  var best = srLoadBest(srLevel);
  var isNewBest = srScore > best;
  if (isNewBest) srSaveBest(srLevel, srScore);
  setText("speedround-final-score", srScore);
  var bestEl = $("speedround-final-best");
  if (bestEl) {
    bestEl.textContent = isNewBest
      ? "🏆 New best!"
      : "Best: " + Math.max(best, srScore);
  }
  setHidden("speedround-result", false);
}

on("speedround-start-btn", "click", function () {
  startSpeedRound(currentLevel);
});
on("speedround-hint-btn", "click", srHint);
on("speedround-audio", "click", function () {
  if (srQuestion) speak(srQuestion.word);
});
on("speedround-back", "click", showSpeedSetup);
on("speedround-change", "click", showSpeedSetup);
on("speedround-restart", "click", function () {
  startSpeedRound(srLevel);
});

document.addEventListener("visibilitychange", function () {
  if (!speedActive || srDone) return;
  if (document.visibilityState === "hidden") stopSpeedTimer();
  else startSpeedTimer();
});

onLevelChange(resetSpeedRound);
