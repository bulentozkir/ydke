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

/* Top Words — Word Guess (Wordle-style) game page logic. Requires shared.js.
 * The board width is the TARGET WORD's own length rather than a fixed 5, so
 * it works across every level/language without a same-length word pool.
 * Guesses are not checked against a dictionary (the per-level pool is too
 * small to be a fair judge of "is this a real word") -- any input of the
 * right length is scored for letter feedback, same spirit as Hangman not
 * requiring a "real word" guess either.
 */
"use strict";

var WG_MAX_GUESSES = 6;
var WG_MIN_LEN = 3;
var WG_MAX_LEN = 9;
var wgActive = false;
var wgLevel = null;
var wgTarget = null; // { bare, entry }
var wgGuesses = []; // [{ text, result: ["correct"|"present"|"absent", ...] }]
var wgDone = false;
var wgCurrentInput = "";

function wgBestKey(level) {
  return "udsp_wordguess_best_" + currentLang + "_" + level + "_v1";
}
function wgLoadBest(level) {
  var v = parseInt(localStorage.getItem(wgBestKey(level)), 10);
  return isNaN(v) ? 0 : v;
}
function wgSaveBest(level, guesses) {
  try {
    localStorage.setItem(wgBestKey(level), String(guesses));
  } catch (e) {
    /* ignore storage errors (private mode) */
  }
}

function wgStripArticle(word) {
  var patterns = {
    de: /^(der|die|das)\s+/i,
    fr: /^(?:(le|la|les)\s+|l['’])/i,
    it: /^(?:(il|lo|la|i|gli|le)\s+|l['’])/i,
    es: /^(el|la|los|las)\s+/i,
    pt: /^(o|a|os|as)\s+/i,
    nl: /^(de|het)\s+/i,
  };
  var pattern = patterns[currentLang];
  return pattern ? (word || "").replace(pattern, "").trim() : (word || "").trim();
}

function wgEligible(w) {
  if (!w || !w.word || !w.definition) return false;
  var bare = wgStripArticle(w.word);
  return (
    !!bare &&
    /^[A-Za-zÀ-ÖØ-öø-ÿ]+$/.test(bare) &&
    !/ß/i.test(bare) &&
    bare.length >= WG_MIN_LEN &&
    bare.length <= WG_MAX_LEN
  );
}

function wgPool(level) {
  return (WORD_SETS[level] || []).filter(wgEligible);
}

function refreshWordGuessStart() {
  var pool = wgPool(currentLevel);
  var ok = pool.length >= 3;
  setText("wordguess-start-level", levelLabel(currentLevel));
  setText(
    "wordguess-start-count",
    ok ? pool.length + (pool.length === 1 ? " word" : " words") + " available" : ""
  );
  setHidden("wordguess-start-warning", ok);
  if (!ok) {
    setText(
      "wordguess-start-warning",
      "Not enough short single-word entries at this level (needs 3+) — pick another above."
    );
  }
  var btn = $("wordguess-start-btn");
  if (btn) btn.disabled = !ok;
}

function showWordGuessSetup() {
  wgActive = false;
  setPlayHeader(false);
  refreshWordGuessStart();
  setHidden("wordguess-game", true);
  setHidden("wordguess-setup", false);
}
function resetWordGuess() {
  wgActive = false;
  showWordGuessSetup();
}
function enterWordGuess() {
  if (!wgActive) showWordGuessSetup();
}

function startWordGuess(level) {
  wgLevel = level;
  wgActive = true;
  wgDone = false;
  setPlayHeader(true);
  setHidden("wordguess-setup", true);
  setHidden("wordguess-game", false);
  setHidden("wordguess-result", true);
  setText("wordguess-level-badge", levelLabel(level));

  var pool = wgPool(level);
  var entry = pool[Math.floor(Math.random() * pool.length)];
  wgTarget = { bare: wgStripArticle(entry.word), entry: entry };
  wgGuesses = [];
  wgCurrentInput = "";
  setDefinition("wordguess-def", entry.definition || "");
  setText("wordguess-length-hint", wgTarget.bare.length + " letters");
  renderWordGuessBoard();
  renderWordGuessKeyboard();
}

function wgFold(ch) {
  var s = String(ch == null ? "" : ch);
  s = s.normalize ? s.normalize("NFD").replace(/[\u0300-\u036f]/g, "") : s;
  return s.toUpperCase();
}

// Standard two-pass Wordle scoring: exact positions first, then greedily
// match leftover letters so a repeated letter isn't flagged "present" twice.
function wgScoreGuess(guess, target) {
  var n = target.length;
  var result = new Array(n);
  var targetLetters = target.split("");
  var used = new Array(n).fill(false);
  var i, j;
  for (i = 0; i < n; i++) {
    if (wgFold(guess[i]) === wgFold(targetLetters[i])) {
      result[i] = "correct";
      used[i] = true;
    }
  }
  for (i = 0; i < n; i++) {
    if (result[i]) continue;
    var found = -1;
    for (j = 0; j < n; j++) {
      if (!used[j] && wgFold(guess[i]) === wgFold(targetLetters[j])) {
        found = j;
        break;
      }
    }
    if (found !== -1) {
      result[i] = "present";
      used[found] = true;
    } else {
      result[i] = "absent";
    }
  }
  return result;
}

function renderWordGuessBoard() {
  var n = wgTarget.bare.length;
  var rows = [];
  for (var r = 0; r < WG_MAX_GUESSES; r++) {
    var cells = [];
    var guessObj = wgGuesses[r];
    var text = guessObj ? guessObj.text : r === wgGuesses.length ? wgCurrentInput : "";
    for (var c = 0; c < n; c++) {
      var ch = (text[c] || "").toUpperCase();
      var cls = "wg-cell";
      var label = ch || "Empty";
      if (guessObj) cls += " wg-" + guessObj.result[c];
      else if (ch) cls += " wg-filled";
      if (guessObj) {
        label +=
          guessObj.result[c] === "correct"
            ? ", correct position"
            : guessObj.result[c] === "present"
              ? ", present in another position"
              : ", not in the word";
      }
      cells.push(
        '<span class="' +
          cls +
          '" role="gridcell" aria-label="' +
          escapeHtml(label) +
          '">' +
          escapeHtml(ch) +
          "</span>"
      );
    }
    rows.push('<div class="wg-row" role="row">' + cells.join("") + "</div>");
  }
  var board = $("wordguess-board");
  if (board) {
    board.style.setProperty("--wg-cols", n);
    board.style.setProperty("--wg-row-max", n * 42 + (n - 1) * 6 + "px");
    board.innerHTML = rows.join("");
  }
}

var WG_KB_ROWS = ["QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM"];
var WG_STATE_RANK = { correct: 3, present: 2, absent: 1 };
function renderWordGuessKeyboard() {
  var letterState = {};
  wgGuesses.forEach(function (g) {
    for (var i = 0; i < g.text.length; i++) {
      var k = wgFold(g.text[i]);
      var st = g.result[i];
      if (!letterState[k] || WG_STATE_RANK[st] > WG_STATE_RANK[letterState[k]]) {
        letterState[k] = st;
      }
    }
  });
  var html = WG_KB_ROWS.map(function (row) {
    var keys = row
      .split("")
      .map(function (ch) {
        var st = letterState[ch] ? " wg-key-" + letterState[ch] : "";
        return '<button type="button" class="wg-key' + st + '" data-key="' + ch + '">' + ch + "</button>";
      })
      .join("");
    return '<div class="wg-kb-row">' + keys + "</div>";
  }).join("");
  html +=
    '<div class="wg-kb-row">' +
    '<button type="button" class="wg-key wg-key-wide" data-key="ENTER" aria-label="Submit guess">Enter</button>' +
    '<button type="button" class="wg-key wg-key-wide" data-key="BACK" aria-label="Backspace">⌫</button>' +
    "</div>";
  var kb = $("wordguess-keyboard");
  if (kb) kb.innerHTML = html;
}

function wgFlashMessage(msg) {
  setText("wordguess-message", msg);
  setHidden("wordguess-message", false);
  setTimeout(function () {
    setHidden("wordguess-message", true);
  }, 1100);
}

function wgPressKey(key) {
  if (wgDone || !wgTarget) return;
  if (key === "ENTER") {
    wgSubmitGuess();
    return;
  }
  if (key === "BACK") {
    wgCurrentInput = wgCurrentInput.slice(0, -1);
    renderWordGuessBoard();
    return;
  }
  if (wgCurrentInput.length < wgTarget.bare.length && /^[A-ZÀ-ÖØ-öø-ÿ]$/i.test(key)) {
    wgCurrentInput += key;
    renderWordGuessBoard();
  }
}

function wgSubmitGuess() {
  if (wgDone || !wgTarget) return;
  var n = wgTarget.bare.length;
  if (wgCurrentInput.length !== n) {
    wgFlashMessage("Not enough letters · Yetersiz harf");
    return;
  }
  var result = wgScoreGuess(wgCurrentInput, wgTarget.bare);
  wgGuesses.push({ text: wgCurrentInput, result: result });
  var won = result.every(function (r) {
    return r === "correct";
  });
  wgCurrentInput = "";
  renderWordGuessBoard();
  renderWordGuessKeyboard();
  if (won) {
    endWordGuess(true);
  } else if (wgGuesses.length >= WG_MAX_GUESSES) {
    endWordGuess(false);
  }
}

function endWordGuess(won) {
  wgDone = true;
  answeredWord(wgTarget.entry, won);
  var used = wgGuesses.length;
  var best = wgLoadBest(wgLevel);
  var isNewBest = won && (best === 0 || used < best);
  if (isNewBest) wgSaveBest(wgLevel, used);
  setText("wordguess-final-word", wgTarget.entry.word);
  setDefinition("wordguess-final-def", wgTarget.entry.definition || "");
  var resultEl = $("wordguess-final-result");
  if (resultEl) {
    resultEl.classList.toggle("win", won);
    resultEl.classList.toggle("lose", !won);
  }
  setText(
    "wordguess-final-result",
    won ? "🎉 Solved in " + used + " / " + WG_MAX_GUESSES : "❌ " + wgTarget.bare.toUpperCase()
  );
  var bestEl = $("wordguess-final-best");
  if (bestEl) {
    bestEl.textContent = isNewBest ? "🏆 New best!" : best ? "Best: " + best + " / " + WG_MAX_GUESSES : "";
  }
  setHidden("wordguess-result", false);
}

on("wordguess-start-btn", "click", function () {
  startWordGuess(currentLevel);
});
on("wordguess-back", "click", showWordGuessSetup);
on("wordguess-change", "click", showWordGuessSetup);
on("wordguess-restart", "click", function () {
  startWordGuess(wgLevel);
});
on("wordguess-audio", "click", function () {
  if (wgTarget) speak(wgTarget.entry.word);
});

document.addEventListener("click", function (e) {
  var t = e.target.closest && e.target.closest(".wg-key");
  if (t) wgPressKey(t.getAttribute("data-key"));
});

document.addEventListener("keydown", function (e) {
  var g = $("wordguess-game");
  if (!g || g.hidden || wgDone) return;
  if (e.key === "Enter") {
    wgSubmitGuess();
    return;
  }
  if (e.key === "Backspace") {
    wgPressKey("BACK");
    return;
  }
  if (/^[a-zA-ZÀ-ÖØ-öø-ÿ]$/.test(e.key)) wgPressKey(e.key.toUpperCase());
});

onLevelChange(resetWordGuess);
