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

/* Top Words — Listening Choice audio-recognition game. Requires shared.js. */
"use strict";

var LC_ROUNDS = 15;
var listeningChoiceActive = false;
var lcLevel = null;
var lcRound = 0;
var lcScore = 0;
var lcQueue = [];
var lcCurrent = null; // { entry, options }
var lcAnswered = false;
var lcDone = false;
var lcSpeechTimer = null;
var lcNextTimer = null;

function lcClearTimers() {
  if (lcSpeechTimer) clearTimeout(lcSpeechTimer);
  if (lcNextTimer) clearTimeout(lcNextTimer);
  lcSpeechTimer = null;
  lcNextTimer = null;
}

function lcBestKey(level) {
  return "udsp_listeningchoice_best_" + currentLang + "_" + level + "_v1";
}
function lcLoadBest(level) {
  var value = parseInt(localStorage.getItem(lcBestKey(level)), 10);
  return isNaN(value) ? 0 : value;
}
function lcSaveBest(level, score) {
  try {
    localStorage.setItem(lcBestKey(level), String(score));
  } catch (e) {
    /* ignore storage errors (private mode) */
  }
}

function lcEligible(entry) {
  return !!(entry && entry.word && entry.definition);
}

function refreshListeningChoiceStart() {
  var pool = (WORD_SETS[currentLevel] || []).filter(lcEligible);
  var ok = pool.length >= 4;
  setText("listeningchoice-start-level", levelLabel(currentLevel));
  setText(
    "listeningchoice-start-count",
    ok ? pool.length + (pool.length === 1 ? " word" : " words") + " available" : ""
  );
  setHidden("listeningchoice-start-warning", ok);
  if (!ok) {
    setText(
      "listeningchoice-start-warning",
      "Not enough words at this level (needs 4+) — pick another above."
    );
  }
  var button = $("listeningchoice-start-btn");
  if (button) button.disabled = !ok;
}

function showListeningChoiceSetup() {
  listeningChoiceActive = false;
  lcDone = true;
  lcClearTimers();
  setPlayHeader(false);
  refreshListeningChoiceStart();
  setHidden("listeningchoice-game", true);
  setHidden("listeningchoice-setup", false);
}

function resetListeningChoice() {
  listeningChoiceActive = false;
  showListeningChoiceSetup();
}

function startListeningChoice(level) {
  var pool = (WORD_SETS[level] || []).filter(lcEligible);
  if (pool.length < 4) return;

  lcLevel = level;
  lcClearTimers();
  listeningChoiceActive = true;
  lcDone = false;
  lcAnswered = false;
  lcRound = 0;
  lcScore = 0;
  lcQueue = shuffle(pool.slice());
  setPlayHeader(true);
  setHidden("listeningchoice-setup", true);
  setHidden("listeningchoice-game", false);
  setHidden("listeningchoice-result", true);
  setText("listeningchoice-level-badge", levelLabel(level));
  setText("listeningchoice-score", "0");
  lcNextRound();
}

function lcNextRound() {
  if (lcDone) return;
  if (lcRound >= LC_ROUNDS) {
    endListeningChoice();
    return;
  }

  var pool = (WORD_SETS[lcLevel] || []).filter(lcEligible);
  if (pool.length < 4) {
    endListeningChoice();
    return;
  }
  if (!lcQueue.length) lcQueue = shuffle(pool.slice());

  var entry = lcQueue.pop();
  var distractors = shuffle(
    pool.filter(function (candidate) {
      return candidate !== entry && candidate.definition !== entry.definition;
    })
  ).slice(0, 3);
  if (distractors.length < 3) {
    endListeningChoice();
    return;
  }

  lcRound++;
  lcCurrent = { entry: entry, options: shuffle([entry].concat(distractors)) };
  lcAnswered = false;
  setText("listeningchoice-progress", "Round " + lcRound + " / " + LC_ROUNDS);
  setText("listeningchoice-word-reveal", "");
  setText("listeningchoice-feedback", "");
  setHidden("listeningchoice-word-reveal", true);

  var box = $("listeningchoice-options");
  if (box) {
    box.innerHTML = "";
    lcCurrent.options.forEach(function (option, index) {
      var button = document.createElement("button");
      button.type = "button";
      button.className = "option";
      button.innerHTML =
        '<span class="lc-option-number">' +
        (index + 1) +
        '</span><span class="lc-option-text">' +
        bilingualHtml(option.definition) +
        "</span>";
      button.addEventListener("click", function () {
        lcAnswer(option, button);
      });
      box.appendChild(button);
    });
  }

  lcSpeechTimer = setTimeout(function () {
    lcSpeechTimer = null;
    if (listeningChoiceActive && !lcDone && !lcAnswered && lcCurrent) {
      speak(lcCurrent.entry.word);
    }
  }, 120);
}

function lcAnswer(picked, button) {
  if (lcDone || lcAnswered || !lcCurrent) return;
  lcAnswered = true;
  var correct = picked === lcCurrent.entry;
  answeredWord(lcCurrent.entry, correct);

  var box = $("listeningchoice-options");
  if (box) {
    box.querySelectorAll(".option").forEach(function (candidate) {
      candidate.disabled = true;
    });
  }
  lcCurrent.options.forEach(function (option, index) {
    if (!box) return;
    var candidate = box.children[index];
    if (option === lcCurrent.entry) candidate.classList.add("correct");
    else if (candidate === button) candidate.classList.add("wrong");
  });

  if (correct) {
    lcScore++;
    setText("listeningchoice-score", String(lcScore));
  }
  setText("listeningchoice-word-reveal", lcCurrent.entry.word);
  setHidden("listeningchoice-word-reveal", false);
  setText(
    "listeningchoice-feedback",
    correct ? "Correct · Doğru" : "Not quite · Doğru anlam highlighted"
  );

  lcNextTimer = setTimeout(function () {
    lcNextTimer = null;
    if (listeningChoiceActive && !lcDone) lcNextRound();
  }, 850);
}

function endListeningChoice() {
  if (lcDone) return;
  lcDone = true;
  lcClearTimers();
  var best = lcLoadBest(lcLevel);
  var isNewBest = lcScore > best;
  if (isNewBest) lcSaveBest(lcLevel, lcScore);
  setText("listeningchoice-final-score", lcScore + " / " + LC_ROUNDS);
  var bestElement = $("listeningchoice-final-best");
  if (bestElement) {
    bestElement.textContent = isNewBest
      ? "🏆 New best!"
      : "Best: " + Math.max(best, lcScore) + " / " + LC_ROUNDS;
  }
  setHidden("listeningchoice-result", false);
}

on("listeningchoice-start-btn", "click", function () {
  startListeningChoice(currentLevel);
});
on("listeningchoice-replay", "click", function () {
  if (lcCurrent) speak(lcCurrent.entry.word);
});
on("listeningchoice-back", "click", showListeningChoiceSetup);
on("listeningchoice-change", "click", showListeningChoiceSetup);
on("listeningchoice-restart", "click", function () {
  startListeningChoice(lcLevel);
});

document.addEventListener("keydown", function (event) {
  var game = $("listeningchoice-game");
  if (!game || game.hidden || lcDone || lcAnswered) return;
  var index = parseInt(event.key, 10) - 1;
  var box = $("listeningchoice-options");
  if (box && index >= 0 && index < 4) box.children[index].click();
});

onLevelChange(resetListeningChoice);
