/* Top Words — Word Class game page logic. Requires shared.js. */
"use strict";

var WC_ROUNDS = 20;
// Only the four classes that carry meaning in exam questions. The data also
// holds "phrase", "number", "determiner" and a generic "word" bucket; those
// are not word classes a learner is ever asked to identify, and including
// them would turn a 4-button game into a 12-button guessing screen.
var WC_CLASSES = [
  { value: "noun", label: "Noun", tr: "İsim" },
  { value: "verb", label: "Verb", tr: "Fiil" },
  { value: "adjective", label: "Adjective", tr: "Sıfat" },
  { value: "adverb", label: "Adverb", tr: "Zarf" },
];

var wcActive = false;
var wcLevel = null;
var wcRound = 0;
var wcScore = 0;
var wcDone = false;
var wcAnswered = false;
var wcCurrent = null;
var wcQueue = [];

function wcBestKey(level) {
  return "udsp_wordclass_best_" + currentLang + "_" + level + "_v1";
}
function wcLoadBest(level) {
  var v = parseInt(localStorage.getItem(wcBestKey(level)), 10);
  return isNaN(v) ? 0 : v;
}
function wcSaveBest(level, score) {
  try {
    localStorage.setItem(wcBestKey(level), String(score));
  } catch (e) {
    /* ignore storage errors (private mode) */
  }
}

function wcClassOf(w) {
  if (!w || !w.pos) return null;
  var pos = String(w.pos).toLowerCase().trim();
  for (var i = 0; i < WC_CLASSES.length; i++) {
    if (WC_CLASSES[i].value === pos) return WC_CLASSES[i];
  }
  return null;
}

function wcEligible(w) {
  return !!(w && w.word && wcClassOf(w));
}

function wcPool() {
  return (WORD_SETS[currentLevel] || []).filter(wcEligible);
}

function refreshWordClassStart() {
  var pool = wcPool();
  var enough = pool.length >= 5;
  setText("wordclass-start-level", levelLabel(currentLevel));
  setText("wordclass-start-count", enough ? pool.length + " words in the pool" : "");
  setHidden("wordclass-start-warning", enough);
  if (!enough) {
    setText(
      "wordclass-start-warning",
      "Not enough tagged words at this level (need 5) — pick another level or category above."
    );
  }
  var btn = $("wordclass-start-btn");
  if (btn) btn.disabled = !enough;
}

function showWordClassSetup() {
  wcActive = false;
  setPlayHeader(false);
  refreshWordClassStart();
  setHidden("wordclass-game", true);
  setHidden("wordclass-setup", false);
}

function resetWordClass() {
  wcActive = false;
  showWordClassSetup();
}

function enterWordClass() {
  if (!wcActive) showWordClassSetup();
}

function startWordClass(level) {
  wcLevel = level;
  wcActive = true;
  wcDone = false;
  setPlayHeader(true);
  setHidden("wordclass-setup", true);
  setHidden("wordclass-game", false);
  setHidden("wordclass-result", true);
  setText("wordclass-level-badge", levelLabel(level));
  wcRound = 0;
  wcScore = 0;
  setText("wordclass-score", "0");
  // A shuffled queue rather than random picks, so a 20-round session never
  // asks about the same word twice while other words go untested.
  wcQueue = shuffle(wcPool().slice());
  wcRenderOptions();
  wcNextRound();
}

function wcRenderOptions() {
  var box = $("wordclass-options");
  if (!box) return;
  box.innerHTML = "";
  // The four buttons never change, so they are built once per game and only
  // their state is reset per round -- the learner can rely on muscle memory
  // for the positions, which is the whole point of a fixed-choice drill.
  WC_CLASSES.forEach(function (cls, i) {
    var btn = document.createElement("button");
    btn.type = "button";
    btn.className = "option";
    btn.dataset.pos = cls.value;
    btn.innerHTML =
      '<span class="key">' +
      (i + 1) +
      '</span><span>' +
      escapeHtml(cls.label) +
      ' <span class="wc-tr" lang="tr">· ' +
      escapeHtml(cls.tr) +
      "</span></span>";
    btn.addEventListener("click", function () {
      wcAnswer(cls.value);
    });
    box.appendChild(btn);
  });
}

function wcResetOptions() {
  var buttons = $("wordclass-options").querySelectorAll(".option");
  for (var i = 0; i < buttons.length; i++) {
    buttons[i].disabled = false;
    buttons[i].classList.remove("correct", "wrong");
  }
}

function wcNextRound() {
  if (wcDone) return;
  if (wcRound >= WC_ROUNDS || !wcQueue.length) {
    endWordClass();
    return;
  }
  wcRound++;
  wcCurrent = wcQueue.pop();
  wcAnswered = false;
  setText("wordclass-progress", "Round " + wcRound + " / " + WC_ROUNDS);
  setText("wordclass-word", wcCurrent.word);
  setText("wordclass-feedback", "");
  setHidden("wordclass-hint", true);
  setText("wordclass-hint", "");
  var hintBtn = $("wordclass-hint-btn");
  // The example sentence is only a usable hint when it is a real sentence;
  // the placeholder rows would just show the word back to the learner.
  if (hintBtn) hintBtn.disabled = isPlaceholderExample(wcCurrent.example);
  wcResetOptions();
}

function wcAnswer(pos) {
  if (wcDone || wcAnswered || !wcCurrent) return;
  wcAnswered = true;
  var truth = wcClassOf(wcCurrent);
  var correct = truth && truth.value === pos;
  var buttons = $("wordclass-options").querySelectorAll(".option");
  for (var i = 0; i < buttons.length; i++) {
    buttons[i].disabled = true;
    if (truth && buttons[i].dataset.pos === truth.value) buttons[i].classList.add("correct");
    else if (buttons[i].dataset.pos === pos) buttons[i].classList.add("wrong");
  }
  if (correct) {
    wcScore++;
    setText("wordclass-score", wcScore);
    setText("wordclass-feedback", "✅ Correct — " + truth.label + " · " + truth.tr);
  } else {
    setText(
      "wordclass-feedback",
      "❌ “" + wcCurrent.word + "” is a " + truth.label + " · " + truth.tr
    );
  }
  setDefinition("wordclass-hint", wcCurrent.definition);
  setHidden("wordclass-hint", false);
  answeredWord(wcCurrent, correct);
  setTimeout(function () {
    if (!wcDone) wcNextRound();
  }, 1400);
}

function wcHint() {
  if (wcDone || wcAnswered || !wcCurrent) return;
  if (isPlaceholderExample(wcCurrent.example)) return;
  setBilingual("wordclass-hint", wcCurrent.example);
  setHidden("wordclass-hint", false);
  var btn = $("wordclass-hint-btn");
  if (btn) btn.disabled = true;
}

function wcSpeak() {
  if (!wcCurrent) return;
  speak(wcCurrent.word);
}

function endWordClass() {
  wcDone = true;
  var best = wcLoadBest(wcLevel);
  var isNewBest = wcScore > best;
  if (isNewBest) wcSaveBest(wcLevel, wcScore);
  setText("wordclass-final-score", wcScore + " / " + WC_ROUNDS);
  var bestEl = $("wordclass-final-best");
  if (bestEl) {
    bestEl.textContent = isNewBest
      ? "🏆 New best!"
      : "Best: " + Math.max(best, wcScore) + " / " + WC_ROUNDS;
  }
  setHidden("wordclass-result", false);
}

on("wordclass-hint-btn", "click", wcHint);
on("wordclass-audio", "click", wcSpeak);
on("wordclass-start-btn", "click", function () {
  startWordClass(currentLevel);
});
on("wordclass-back", "click", showWordClassSetup);
on("wordclass-change", "click", showWordClassSetup);
on("wordclass-restart", "click", function () {
  startWordClass(wcLevel);
});

document.addEventListener("keydown", function (e) {
  var g = $("wordclass-game");
  if (!g || g.hidden || wcDone) return;
  var n = parseInt(e.key, 10);
  if (n >= 1 && n <= WC_CLASSES.length) wcAnswer(WC_CLASSES[n - 1].value);
});

onLevelChange(resetWordClass);
