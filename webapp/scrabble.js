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

/* Top Words — Scrabble game page logic. Requires shared.js.
 * A simplified single-word Scrabble: the rack holds the target word's own
 * letters plus a few decoys, tiles carry classic English Scrabble point
 * values (applied uniformly across languages -- there is no authoritative
 * per-language value table on hand, so this is deliberately labelled
 * "Scrabble-style scoring" rather than an official tournament table), and
 * building the full rack for a correct word earns a bonus, mirroring a
 * real "bingo". Guesses are not checked against a dictionary, same
 * reasoning as Word Guess: the per-level pool is too small to be a fair
 * judge of "is this a real word".
 */
"use strict";

var SCRAB_ROUNDS = 10;
var SCRAB_MIN_LEN = 3;
var SCRAB_MAX_LEN = 9;
var SCRAB_DECOY_COUNT = 3;
var SCRAB_BINGO_BONUS = 10;

var SCRAB_LETTER_VALUES = {
  A: 1, E: 1, I: 1, O: 1, U: 1, L: 1, N: 1, S: 1, T: 1, R: 1,
  D: 2, G: 2,
  B: 3, C: 3, M: 3, P: 3,
  F: 4, H: 4, V: 4, W: 4, Y: 4,
  K: 5,
  J: 8, X: 8,
  Q: 10, Z: 10,
};
var SCRAB_COMMON_LETTERS = {
  en: "ETAOINSHRDLU",
  de: "ENISTRADHUG",
  fr: "ESAINTRULOD",
  it: "EAIONLRTSC",
  es: "EAOSRNILDTU",
  pt: "AEOSRINDTUM",
  nl: "ENATIRODSLG",
};

var scrabActive = false;
var scrabLevel = null;
var scrabRound = 0;
var scrabScore = 0;
var scrabDone = false;
var scrabTarget = null; // { bare, entry }
var scrabRack = []; // [{ letter, used }]
var scrabBuilt = []; // rack indices, in build order

function scrabBestKey(level) {
  return "udsp_scrabble_best_" + currentLang + "_" + level + "_v1";
}
function scrabLoadBest(level) {
  var v = parseInt(localStorage.getItem(scrabBestKey(level)), 10);
  return isNaN(v) ? 0 : v;
}
function scrabSaveBest(level, score) {
  try {
    localStorage.setItem(scrabBestKey(level), String(score));
  } catch (e) {
    /* ignore storage errors (private mode) */
  }
}

function scrabStripArticle(word) {
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
function scrabFold(ch) {
  var s = String(ch == null ? "" : ch);
  s = s.normalize ? s.normalize("NFD").replace(/[\u0300-\u036f]/g, "") : s;
  return s.toUpperCase();
}
function scrabValue(ch) {
  return SCRAB_LETTER_VALUES[scrabFold(ch)] || 1;
}
function scrabEligible(w) {
  if (!w || !w.word || !w.definition) return false;
  var bare = scrabStripArticle(w.word);
  return (
    !!bare &&
    /^[A-Za-zÀ-ÖØ-öø-ÿ]+$/.test(bare) &&
    !/ß/i.test(bare) &&
    bare.length >= SCRAB_MIN_LEN &&
    bare.length <= SCRAB_MAX_LEN
  );
}
function scrabPool(level) {
  return (WORD_SETS[level] || []).filter(scrabEligible);
}

function refreshScrabbleStart() {
  var pool = scrabPool(currentLevel);
  var ok = pool.length >= 5;
  setText("scrabble-start-level", levelLabel(currentLevel));
  setText(
    "scrabble-start-count",
    ok ? pool.length + (pool.length === 1 ? " word" : " words") + " available" : ""
  );
  setHidden("scrabble-start-warning", ok);
  if (!ok) {
    setText(
      "scrabble-start-warning",
      "Not enough short single-word entries at this level (needs 5+) — pick another above."
    );
  }
  var btn = $("scrabble-start-btn");
  if (btn) btn.disabled = !ok;
  var best = scrabLoadBest(currentLevel);
  setText("scrabble-start-best", best ? "🏆 Best: " + best + " pts" : "");
}

function showScrabbleSetup() {
  scrabActive = false;
  setPlayHeader(false);
  refreshScrabbleStart();
  setHidden("scrabble-game", true);
  setHidden("scrabble-setup", false);
}
function resetScrabble() {
  scrabActive = false;
  showScrabbleSetup();
}
function enterScrabble() {
  if (!scrabActive) showScrabbleSetup();
}

function startScrabble(level) {
  scrabLevel = level;
  scrabActive = true;
  scrabDone = false;
  scrabRound = 0;
  scrabScore = 0;
  setPlayHeader(true);
  setHidden("scrabble-setup", true);
  setHidden("scrabble-game", false);
  setHidden("scrabble-result", true);
  setText("scrabble-level-badge", levelLabel(level));
  setText("scrabble-score", "0");
  scrabNextWord();
}

function scrabBuildRack(target) {
  var letters = target.split("").map(function (ch) {
    return ch.toUpperCase();
  });
  var commons = SCRAB_COMMON_LETTERS[currentLang] || SCRAB_COMMON_LETTERS.en;
  for (var i = 0; i < SCRAB_DECOY_COUNT; i++) {
    letters.push(commons[Math.floor(Math.random() * commons.length)]);
  }
  return shuffle(letters).map(function (letter) {
    return { letter: letter, used: false };
  });
}

function scrabNextWord() {
  if (scrabDone) return;
  if (scrabRound >= SCRAB_ROUNDS) {
    endScrabble();
    return;
  }
  var pool = scrabPool(scrabLevel);
  if (pool.length < 5) {
    endScrabble();
    return;
  }
  scrabRound++;
  var entry = pool[Math.floor(Math.random() * pool.length)];
  scrabTarget = { bare: scrabStripArticle(entry.word), entry: entry };
  scrabRack = scrabBuildRack(scrabTarget.bare);
  scrabBuilt = [];
  setText("scrabble-progress", "Word " + scrabRound + " / " + SCRAB_ROUNDS);
  setDefinition("scrabble-def", entry.definition || "");
  setText("scrabble-length-hint", scrabTarget.bare.length + " letters");
  setHidden("scrabble-message", true);
  scrabRenderBuild();
  scrabRenderRack();
}

function scrabRenderBuild() {
  var row = $("scrabble-build");
  if (!row) return;
  var n = scrabTarget.bare.length;
  var cells = [];
  for (var i = 0; i < n; i++) {
    var rackIdx = scrabBuilt[i];
    var tile = rackIdx != null ? scrabRack[rackIdx] : null;
    cells.push(
      '<span class="scrab-slot' +
        (tile ? " is-filled" : "") +
        '" data-slot="' +
        i +
        '">' +
        (tile ? escapeHtml(tile.letter) : "") +
        "</span>"
    );
  }
  row.innerHTML = cells.join("");
}

function scrabRenderRack() {
  var box = $("scrabble-rack");
  if (!box) return;
  box.innerHTML = scrabRack
    .map(function (tile, idx) {
      return (
        '<button type="button" class="scrab-tile' +
        (tile.used ? " is-used" : "") +
        '" data-idx="' +
        idx +
        '" ' +
        (tile.used ? "disabled" : "") +
        ">" +
        escapeHtml(tile.letter) +
        '<span class="scrab-value">' +
        scrabValue(tile.letter) +
        "</span></button>"
      );
    })
    .join("");
}

function scrabFlashMessage(msg) {
  setText("scrabble-message", msg);
  setHidden("scrabble-message", false);
  setTimeout(function () {
    setHidden("scrabble-message", true);
  }, 1100);
}

function scrabPickRackTile(idx) {
  if (scrabDone || !scrabTarget) return;
  var tile = scrabRack[idx];
  if (!tile || tile.used) return;
  if (scrabBuilt.length >= scrabTarget.bare.length) return;
  tile.used = true;
  scrabBuilt.push(idx);
  scrabRenderBuild();
  scrabRenderRack();
  if (scrabBuilt.length === scrabTarget.bare.length) scrabSubmit();
}

function scrabPickBuildSlot(slotIdx) {
  if (scrabDone) return;
  if (slotIdx >= scrabBuilt.length) return;
  var rackIdx = scrabBuilt[slotIdx];
  scrabBuilt.splice(slotIdx, 1);
  if (rackIdx != null && scrabRack[rackIdx]) scrabRack[rackIdx].used = false;
  scrabRenderBuild();
  scrabRenderRack();
}

function scrabClearBuild() {
  if (scrabDone) return;
  scrabBuilt.forEach(function (rackIdx) {
    if (scrabRack[rackIdx]) scrabRack[rackIdx].used = false;
  });
  scrabBuilt = [];
  scrabRenderBuild();
  scrabRenderRack();
}

function scrabSubmit() {
  if (scrabDone || !scrabTarget) return;
  var built = scrabBuilt
    .map(function (rackIdx) {
      return scrabRack[rackIdx].letter;
    })
    .join("");
  var correct = scrabFold(built) === scrabFold(scrabTarget.bare);
  answeredWord(scrabTarget.entry, correct);
  if (correct) {
    var points = scrabBuilt.reduce(function (sum, rackIdx) {
      return sum + scrabValue(scrabRack[rackIdx].letter);
    }, 0);
    if (scrabBuilt.length === scrabRack.length) points += SCRAB_BINGO_BONUS;
    scrabScore += points;
    setText("scrabble-score", String(scrabScore));
    scrabFlashMessage("✅ +" + points + (scrabBuilt.length === scrabRack.length ? " (Bingo!)" : ""));
    speak(scrabTarget.entry.word);
    setTimeout(function () {
      if (!scrabDone) scrabNextWord();
    }, 700);
  } else {
    var board = $("scrabble-card");
    if (board) {
      board.classList.remove("is-shaking");
      void board.offsetWidth;
      board.classList.add("is-shaking");
    }
    scrabFlashMessage("❌ Not quite — try again");
    scrabClearBuild();
  }
}

function endScrabble() {
  scrabDone = true;
  var best = scrabLoadBest(scrabLevel);
  var isNewBest = scrabScore > best;
  if (isNewBest) scrabSaveBest(scrabLevel, scrabScore);
  setText("scrabble-final-score", String(scrabScore));
  var bestEl = $("scrabble-final-best");
  if (bestEl) {
    bestEl.textContent = isNewBest ? "🏆 New best!" : "Best: " + Math.max(best, scrabScore) + " pts";
  }
  setHidden("scrabble-result", false);
}

on("scrabble-start-btn", "click", function () {
  startScrabble(currentLevel);
});
on("scrabble-clear-btn", "click", scrabClearBuild);
on("scrabble-audio", "click", function () {
  if (scrabTarget) speak(scrabTarget.entry.word);
});
on("scrabble-back", "click", showScrabbleSetup);
on("scrabble-change", "click", showScrabbleSetup);
on("scrabble-restart", "click", function () {
  startScrabble(scrabLevel);
});

document.addEventListener("click", function (e) {
  var tileBtn = e.target.closest && e.target.closest(".scrab-tile");
  if (tileBtn) {
    scrabPickRackTile(parseInt(tileBtn.getAttribute("data-idx"), 10));
    return;
  }
  var slot = e.target.closest && e.target.closest(".scrab-slot");
  if (slot && slot.parentElement && slot.parentElement.id === "scrabble-build") {
    scrabPickBuildSlot(parseInt(slot.getAttribute("data-slot"), 10));
  }
});

document.addEventListener("keydown", function (e) {
  var g = $("scrabble-game");
  if (!g || g.hidden || scrabDone || !scrabTarget) return;
  if (e.key === "Backspace") {
    scrabPickBuildSlot(scrabBuilt.length - 1);
    return;
  }
  if (/^[a-zA-ZÀ-ÖØ-öø-ÿ]$/.test(e.key)) {
    var want = scrabFold(e.key);
    var idx = scrabRack.findIndex(function (tile) {
      return !tile.used && scrabFold(tile.letter) === want;
    });
    if (idx !== -1) scrabPickRackTile(idx);
  }
});

onLevelChange(resetScrabble);
