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

/* Top Words — Daily Challenge game page logic. Requires shared.js.
 * One Wordle-style puzzle per language+level+calendar day: the target word
 * is picked deterministically from (language, level, today's date) instead
 * of at random, so reloading the page or opening it later the same day shows
 * the SAME puzzle and the in-progress guesses already made. Once solved or
 * failed, the puzzle for that combination is locked until the next calendar
 * day -- that lock, plus a small day-streak, is what turns this into a daily
 * habit rather than another free-play Word Guess round. No ticking timer is
 * used anywhere on this page, matching the rest of the app's low-CPU idle
 * behaviour.
 */
"use strict";

var DC_MAX_GUESSES = 6;
var DC_MIN_LEN = 3;
var DC_MAX_LEN = 9;
var dcActive = false;
var dcLevel = null;
var dcTarget = null; // { bare, entry }
var dcGuesses = []; // [{ text, result: ["correct"|"present"|"absent", ...] }]
var dcCurrentInput = "";
var dcDone = false;
var dcWon = false;
var dcDate = "";

function dcStateKey(level) {
  return "udsp_dailychallenge_state_" + currentLang + "_" + level + "_v1";
}
function dcStreakKey() {
  return "udsp_dailychallenge_streak_" + currentLang + "_v1";
}

function dcStripArticle(word) {
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

function dcEligible(w) {
  if (!w || !w.word || !w.definition) return false;
  var bare = dcStripArticle(w.word);
  return (
    !!bare &&
    /^[A-Za-zÀ-ÖØ-öø-ÿ]+$/.test(bare) &&
    !/ß/i.test(bare) &&
    bare.length >= DC_MIN_LEN &&
    bare.length <= DC_MAX_LEN
  );
}

function dcPool(level) {
  return (WORD_SETS[level] || []).filter(dcEligible);
}

// A small string hash feeding a xorshift32 PRNG -- deterministic, no
// external dependency, and only ever used to pick one array index, so
// cryptographic quality is not a concern here.
function dcHashSeed(str) {
  var h = 2166136261;
  for (var i = 0; i < str.length; i++) {
    h ^= str.charCodeAt(i);
    h = (h * 16777619) >>> 0;
  }
  return h >>> 0 || 1;
}
function dcSeededRandom(seed) {
  var s = seed >>> 0;
  return function () {
    s ^= s << 13;
    s >>>= 0;
    s ^= s >>> 17;
    s ^= s << 5;
    s >>>= 0;
    return s / 4294967296;
  };
}
function dcPickTarget(level, dateStr) {
  var pool = dcPool(level);
  if (!pool.length) return null;
  var rand = dcSeededRandom(dcHashSeed(currentLang + "|" + level + "|" + dateStr));
  var entry = pool[Math.floor(rand() * pool.length)];
  return { bare: dcStripArticle(entry.word), entry: entry };
}

function dcFold(ch) {
  var s = String(ch == null ? "" : ch);
  s = s.normalize ? s.normalize("NFD").replace(/[\u0300-\u036f]/g, "") : s;
  return s.toUpperCase();
}

// Same two-pass Wordle scoring as Word Guess: exact positions first, then
// greedily match leftover letters so a repeated letter isn't double-flagged.
function dcScoreGuess(guess, target) {
  var n = target.length;
  var result = new Array(n);
  var targetLetters = target.split("");
  var used = new Array(n).fill(false);
  var i, j;
  for (i = 0; i < n; i++) {
    if (dcFold(guess[i]) === dcFold(targetLetters[i])) {
      result[i] = "correct";
      used[i] = true;
    }
  }
  for (i = 0; i < n; i++) {
    if (result[i]) continue;
    var found = -1;
    for (j = 0; j < n; j++) {
      if (!used[j] && dcFold(guess[i]) === dcFold(targetLetters[j])) {
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

function refreshDailyChallengeStart() {
  var pool = dcPool(currentLevel);
  var ok = pool.length >= 3;
  var today = todayStr();
  var state = lsGet(dcStateKey(currentLevel), null);
  var playedToday = !!(state && state.date === today);
  setText("dailychallenge-start-level", levelLabel(currentLevel));
  setHidden("dailychallenge-start-warning", ok);
  if (!ok) {
    setText(
      "dailychallenge-start-warning",
      "Not enough short single-word entries at this level (needs 3+) — pick another above."
    );
  }
  var label = "";
  if (ok) {
    if (playedToday && state.done) label = "✅ Played today — tap to view result";
    else if (playedToday) label = "In progress — tap to continue";
    else label = pool.length + (pool.length === 1 ? " word" : " words") + " available";
  }
  setText("dailychallenge-start-count", label);
  var btn = $("dailychallenge-start-btn");
  if (btn) btn.disabled = !ok;
  var streak = lsGet(dcStreakKey(), { current: 0, longest: 0, last: "" });
  setText("dailychallenge-streak-count", String(streak.current || 0));
}

function showDailyChallengeSetup() {
  dcActive = false;
  setPlayHeader(false);
  refreshDailyChallengeStart();
  setHidden("dailychallenge-game", true);
  setHidden("dailychallenge-setup", false);
}
function resetDailyChallenge() {
  dcActive = false;
  showDailyChallengeSetup();
}
function enterDailyChallenge() {
  if (!dcActive) showDailyChallengeSetup();
}

function startDailyChallenge(level) {
  dcLevel = level;
  dcActive = true;
  dcDate = todayStr();
  setPlayHeader(true);
  setHidden("dailychallenge-setup", true);
  setHidden("dailychallenge-game", false);
  setHidden("dailychallenge-result", true);
  setText("dailychallenge-level-badge", levelLabel(level));

  dcTarget = dcPickTarget(level, dcDate);
  var state = lsGet(dcStateKey(level), null);
  if (state && state.date === dcDate) {
    dcGuesses = state.guesses || [];
    dcDone = !!state.done;
    dcWon = !!state.won;
  } else {
    dcGuesses = [];
    dcDone = false;
    dcWon = false;
  }
  dcCurrentInput = "";

  if (!dcTarget) {
    setHidden("dailychallenge-game", true);
    setHidden("dailychallenge-setup", false);
    return;
  }

  setDefinition("dailychallenge-def", dcTarget.entry.definition || "");
  setText("dailychallenge-length-hint", dcTarget.bare.length + " letters");
  renderDailyChallengeBoard();
  renderDailyChallengeKeyboard();

  if (dcDone) showDailyChallengeResult();
}

function dcSaveState() {
  lsSet(dcStateKey(dcLevel), {
    date: dcDate,
    guesses: dcGuesses,
    done: dcDone,
    won: dcWon,
  });
}

function renderDailyChallengeBoard() {
  var n = dcTarget.bare.length;
  var rows = [];
  for (var r = 0; r < DC_MAX_GUESSES; r++) {
    var cells = [];
    var guessObj = dcGuesses[r];
    var text = guessObj ? guessObj.text : r === dcGuesses.length ? dcCurrentInput : "";
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
  var board = $("dailychallenge-board");
  if (board) {
    board.style.setProperty("--wg-cols", n);
    board.style.setProperty("--wg-row-max", n * 42 + (n - 1) * 6 + "px");
    board.innerHTML = rows.join("");
  }
}

var DC_KB_ROWS = ["QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM"];
var DC_STATE_RANK = { correct: 3, present: 2, absent: 1 };
function renderDailyChallengeKeyboard() {
  var letterState = {};
  dcGuesses.forEach(function (g) {
    for (var i = 0; i < g.text.length; i++) {
      var k = dcFold(g.text[i]);
      var st = g.result[i];
      if (!letterState[k] || DC_STATE_RANK[st] > DC_STATE_RANK[letterState[k]]) {
        letterState[k] = st;
      }
    }
  });
  var html = DC_KB_ROWS.map(function (row) {
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
  var kb = $("dailychallenge-keyboard");
  if (kb) kb.innerHTML = html;
}

function dcFlashMessage(msg) {
  setText("dailychallenge-message", msg);
  setHidden("dailychallenge-message", false);
  setTimeout(function () {
    setHidden("dailychallenge-message", true);
  }, 1100);
}

function dcPressKey(key) {
  if (dcDone || !dcTarget) return;
  if (key === "ENTER") {
    dcSubmitGuess();
    return;
  }
  if (key === "BACK") {
    dcCurrentInput = dcCurrentInput.slice(0, -1);
    renderDailyChallengeBoard();
    return;
  }
  if (dcCurrentInput.length < dcTarget.bare.length && /^[A-ZÀ-ÖØ-öø-ÿ]$/i.test(key)) {
    dcCurrentInput += key;
    renderDailyChallengeBoard();
  }
}

function dcSubmitGuess() {
  if (dcDone || !dcTarget) return;
  var n = dcTarget.bare.length;
  if (dcCurrentInput.length !== n) {
    dcFlashMessage("Not enough letters · Yetersiz harf");
    return;
  }
  var result = dcScoreGuess(dcCurrentInput, dcTarget.bare);
  dcGuesses.push({ text: dcCurrentInput, result: result });
  var won = result.every(function (r) {
    return r === "correct";
  });
  dcCurrentInput = "";
  renderDailyChallengeBoard();
  renderDailyChallengeKeyboard();
  if (won) {
    endDailyChallenge(true);
  } else if (dcGuesses.length >= DC_MAX_GUESSES) {
    endDailyChallenge(false);
  } else {
    dcSaveState();
  }
}

// Streak only ever advances (or resets) once per calendar day, guarded by
// `last`, so replaying an already-solved puzzle on a later visit the same
// day cannot double-count and switching level mid-day cannot re-trigger it.
function dcTouchStreak(won) {
  var key = dcStreakKey();
  var s = lsGet(key, { current: 0, longest: 0, last: "" });
  var t = todayStr();
  if (s.last === t) return s;
  if (won) {
    var y = new Date();
    y.setDate(y.getDate() - 1);
    var ystr = y.getFullYear() + "-" + (y.getMonth() + 1) + "-" + y.getDate();
    s.current = s.last === ystr ? (s.current || 0) + 1 : 1;
    s.longest = Math.max(s.longest || 0, s.current);
  } else {
    s.current = 0;
  }
  s.last = t;
  lsSet(key, s);
  return s;
}

function endDailyChallenge(won) {
  dcDone = true;
  dcWon = won;
  answeredWord(dcTarget.entry, won);
  dcSaveState();
  dcTouchStreak(won);
  if (won) speak(dcTarget.entry.word);
  showDailyChallengeResult();
}

function dcShareText() {
  var grid = dcGuesses
    .map(function (g) {
      return g.result
        .map(function (r) {
          return r === "correct" ? "🟩" : r === "present" ? "🟨" : "⬛";
        })
        .join("");
    })
    .join("\n");
  var scoreLine = dcWon ? dcGuesses.length + "/" + DC_MAX_GUESSES : "X/" + DC_MAX_GUESSES;
  return (
    "Top Words Daily 📅 " +
    dcDate +
    " · " +
    (LANGS[currentLang] ? LANGS[currentLang].label : currentLang) +
    " " +
    levelLabel(dcLevel) +
    "\n" +
    scoreLine +
    "\n" +
    grid +
    "\nhttps://udsp.vercel.app/dailychallenge.html"
  );
}

function dcCopyToClipboard(text) {
  function fallback() {
    var ta = document.createElement("textarea");
    ta.value = text;
    ta.style.position = "fixed";
    ta.style.left = "-9999px";
    document.body.appendChild(ta);
    ta.focus();
    ta.select();
    try {
      document.execCommand("copy");
    } catch (e) {
      /* clipboard unavailable -- the share text is still visible on screen */
    }
    document.body.removeChild(ta);
  }
  try {
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).catch(fallback);
    } else {
      fallback();
    }
  } catch (e) {
    fallback();
  }
}

function showDailyChallengeResult() {
  var resultEl = $("dailychallenge-final-result");
  if (resultEl) {
    resultEl.classList.toggle("win", dcWon);
    resultEl.classList.toggle("lose", !dcWon);
  }
  setText(
    "dailychallenge-final-result",
    dcWon ? "🎉 Solved in " + dcGuesses.length + " / " + DC_MAX_GUESSES : "❌ " + dcTarget.bare.toUpperCase()
  );
  setText("dailychallenge-final-word", dcTarget.entry.word);
  setDefinition("dailychallenge-final-def", dcTarget.entry.definition || "");
  var streak = lsGet(dcStreakKey(), { current: 0, longest: 0, last: "" });
  setText(
    "dailychallenge-final-streak",
    "🔥 " + (streak.current || 0) + " day streak · Best: " + (streak.longest || 0)
  );
  setHidden("dailychallenge-result", false);
}

on("dailychallenge-start-btn", "click", function () {
  startDailyChallenge(currentLevel);
});
on("dailychallenge-back", "click", showDailyChallengeSetup);
on("dailychallenge-change", "click", showDailyChallengeSetup);
on("dailychallenge-audio", "click", function () {
  if (dcTarget) speak(dcTarget.entry.word);
});
on("dailychallenge-share", "click", function () {
  var text = dcShareText();
  dcCopyToClipboard(text);
  dcFlashMessage("📋 Copied to clipboard · Panoya kopyalandı");
});

document.addEventListener("click", function (e) {
  var t = e.target.closest && e.target.closest(".wg-key");
  if (t) dcPressKey(t.getAttribute("data-key"));
});

document.addEventListener("keydown", function (e) {
  var g = $("dailychallenge-game");
  if (!g || g.hidden || dcDone) return;
  if (e.key === "Enter") {
    dcSubmitGuess();
    return;
  }
  if (e.key === "Backspace") {
    dcPressKey("BACK");
    return;
  }
  if (/^[a-zA-ZÀ-ÖØ-öø-ÿ]$/.test(e.key)) dcPressKey(e.key.toUpperCase());
});

onLevelChange(resetDailyChallenge);
