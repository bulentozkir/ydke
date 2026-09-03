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

/* Top Words — Hangman game page logic. Requires shared.js to be loaded first. */
"use strict";

var HM_MAX = 6;
var HM_PARTS = ["hm-head", "hm-body", "hm-larm", "hm-rarm", "hm-lleg", "hm-rleg"];
var hangmanActive = false;
var hangmanWins = 0;
var hangmanLosses = 0;
var hmLevel = null;
var hmWord = null;
var hmSlots = [];
var hmGuessed = {};
var hmWrong = 0;
var hmDone = false;

function hmFold(ch) {
  if (ch === "\u00df" || ch === "\u1e9e") return "S"; // ß / ẞ -> S
  var b = ch.normalize ? ch.normalize("NFD").replace(/[\u0300-\u036f]/g, "") : ch;
  return b.toUpperCase();
}
function hmIsLetter(ch) {
  var f = hmFold(ch);
  return f.length === 1 && f >= "A" && f <= "Z";
}

function refreshHangmanStart() {
  var set = WORD_SETS[currentLevel] || [];
  var ok = set.length > 0;
  setText("hangman-start-level", levelLabel(currentLevel));
  setText(
    "hangman-start-count",
    ok ? set.length + (set.length === 1 ? " word" : " words") + " available" : ""
  );
  setHidden("hangman-start-warning", ok);
  if (!ok) {
    setText(
      "hangman-start-warning",
      "No words at this level — pick another above."
    );
  }
  var btn = $("hangman-start-btn");
  if (btn) btn.disabled = !ok;
}

function showHangmanSetup() {
  hangmanActive = false;
  setPlayHeader(false);
  refreshHangmanStart();
  setHidden("hangman-game", true);
  setHidden("hangman-setup", false);
}

function resetHangman() {
  hangmanActive = false;
  showHangmanSetup();
}

function enterHangman() {
  if (!hangmanActive) showHangmanSetup();
}

function hmPickWord(level) {
  var all = (WORD_SETS[level] || []).filter(function (w) {
    return w && w.word && w.word.trim();
  });
  var pool = all.filter(function (w) {
    var letters = 0;
    for (var i = 0; i < w.word.length; i++) {
      if (hmIsLetter(w.word[i])) letters++;
    }
    return letters >= 2 && w.word.length <= 22;
  });
  if (!pool.length) pool = all;
  if (!pool.length) return null;
  return pool[Math.floor(Math.random() * pool.length)];
}

function startHangman(level) {
  hmLevel = level;
  hangmanActive = true;
  setPlayHeader(true);
  setHidden("hangman-setup", true);
  setHidden("hangman-game", false);
  setText("hangman-level-badge", levelLabel(level));
  setText("hangman-max", HM_MAX);
  setText("hangman-wins", hangmanWins);
  setText("hangman-losses", hangmanLosses);
  newHangmanWord();
}

function newHangmanWord() {
  var w = hmPickWord(hmLevel);
  if (!w) {
    showHangmanSetup();
    return;
  }
  hmWord = w;
  hmSlots = [];
  for (var i = 0; i < w.word.length; i++) {
    var ch = w.word[i];
    var g = hmIsLetter(ch);
    hmSlots.push({ ch: ch, guessable: g, key: g ? hmFold(ch) : null });
  }
  hmGuessed = {};
  hmWrong = 0;
  hmDone = false;
  setText("hangman-wrong", "0");
  var hmHintBtn = $("hangman-hint-btn");
  if (hmHintBtn) hmHintBtn.disabled = false;
  setText("hangman-pos", w.pos || "");
  setBilingual("hangman-clue", w.definition || "");
  setHidden("hangman-result", true);
  buildHangmanKeyboard();
  hmUpdateFigure();
  renderHangmanWord();
}

function renderHangmanWord() {
  var html = "";
  hmSlots.forEach(function (s) {
    if (!s.guessable) {
      if (s.ch === " ") html += '<span class="hm-space"></span>';
      else html += '<span class="hm-fixed">' + escapeHtml(s.ch) + "</span>";
    } else {
      var revealed = hmGuessed[s.key] || hmDone;
      html +=
        '<span class="hm-slot' + (revealed ? " filled" : "") + '">' +
        (revealed ? escapeHtml(s.ch) : "") +
        "</span>";
    }
  });
  var wordEl = $("hangman-word");
  if (wordEl) wordEl.innerHTML = html;
}

function buildHangmanKeyboard() {
  var kb = $("hangman-keyboard");
  if (!kb) return;
  kb.innerHTML = "";
  for (var i = 65; i <= 90; i++) {
    var L = String.fromCharCode(i);
    var b = document.createElement("button");
    b.type = "button";
    b.className = "hm-key";
    b.textContent = L;
    b.setAttribute("data-letter", L);
    (function (letter) {
      b.addEventListener("click", function () {
        hmGuess(letter);
      });
    })(L);
    kb.appendChild(b);
  }
}

function hmUpdateFigure() {
  HM_PARTS.forEach(function (id, i) {
    var el = $(id);
    if (el) el.classList.toggle("show", i < hmWrong);
  });
}

function hmGuess(letter) {
  if (hmDone || hmGuessed[letter]) return;
  hmGuessed[letter] = true;
  var hit = hmSlots.some(function (s) {
    return s.guessable && s.key === letter;
  });
  var kb = $("hangman-keyboard");
  var btn = kb ? kb.querySelector('button[data-letter="' + letter + '"]') : null;
  if (btn) {
    btn.disabled = true;
    btn.classList.add(hit ? "correct" : "wrong");
  }
  if (!hit) {
    hmWrong++;
    setText("hangman-wrong", hmWrong);
    hmUpdateFigure();
  }
  renderHangmanWord();
  var won = hmSlots.every(function (s) {
    return !s.guessable || hmGuessed[s.key];
  });
  if (won) endHangman(true);
  else if (hmWrong >= HM_MAX) endHangman(false);
}

// Reveals one random not-yet-guessed letter (counts as a correct guess, so
// no wrong-guess penalty). One reveal per word.
function hmHint() {
  if (hmDone) return;
  var keys = [];
  hmSlots.forEach(function (s) {
    if (s.guessable && !hmGuessed[s.key] && keys.indexOf(s.key) === -1) {
      keys.push(s.key);
    }
  });
  if (!keys.length) return;
  var key = keys[Math.floor(Math.random() * keys.length)];
  var btn = $("hangman-hint-btn");
  if (btn) btn.disabled = true;
  hmGuess(key);
}

function endHangman(win) {
  hmDone = true;
  renderHangmanWord();
  var kb = $("hangman-keyboard");
  if (kb) {
    kb.querySelectorAll("button").forEach(function (b) {
      b.disabled = true;
    });
  }
  if (!win) {
    hangmanLosses++;
    setText("hangman-losses", hangmanLosses);
    HM_PARTS.forEach(function (id) {
      var el = $(id);
      if (el) el.classList.add("show");
    });
  } else {
    hangmanWins++;
    setText("hangman-wins", hangmanWins);
  }
  var w = hmWord;
  var rt = $("hangman-result-text");
  if (rt) {
    rt.textContent = win ? "🎉 Correct!" : "💀 Game over";
    rt.className = "hangman-result-text " + (win ? "win" : "lose");
  }
  var ansEl = $("hangman-answer");
  if (ansEl) {
    ansEl.innerHTML =
      '<span class="hm-answer-label">Word:</span> <strong>' +
      escapeHtml(w.word) +
      "</strong>";
  }
  var ex = $("hangman-example");
  if (ex) {
    ex.innerHTML = bilingualHtml(w.example || "");
    ex.hidden = !w.example;
  }
  var linkDetails = $("hangman-link-details");
  if (linkDetails) linkDetails.href = vocabDetailsUrl(w.word);
  var linkExamples = $("hangman-link-examples");
  if (linkExamples) linkExamples.href = vocabExamplesUrl(w.word);
  setHidden("hangman-result", false);
  speak(w.word);
}

on("hangman-start-btn", "click", function () {
  startHangman(currentLevel);
});
on("hangman-back", "click", showHangmanSetup);
on("hangman-change", "click", showHangmanSetup);
on("hangman-next", "click", newHangmanWord);
on("hangman-hint-btn", "click", hmHint);
on("hangman-audio", "click", function () {
  if (hmWord) speak(hmWord.word);
});
document.addEventListener("keydown", function (e) {
  var g = $("hangman-game");
  if (!g || g.hidden || hmDone) return;
  if (e.ctrlKey || e.metaKey || e.altKey) return;
  var k = e.key;
  if (k && k.length === 1) {
    var f = hmFold(k);
    if (f.length === 1 && f >= "A" && f <= "Z") {
      e.preventDefault();
      hmGuess(f);
    }
  }
});

onLevelChange(resetHangman);
