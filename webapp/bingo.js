/* Top Words — Word Bingo game page logic. Requires shared.js. */
"use strict";

var BINGO_SIZE = 4; // 4x4
var BINGO_CELLS = BINGO_SIZE * BINGO_SIZE;
var BINGO_LIVES = 3;
var BINGO_POINTS_HIT = 10;
var BINGO_POINTS_LINE = 25;
var BINGO_POINTS_FULL = 50;

var bgActive = false;
var bgLevel = null;
var bgScore = 0;
var bgLives = 0;
var bgDone = false;
var bgBusy = false;
var bgCard = []; // 16 word records, grid order
var bgFound = []; // 16 booleans
var bgCalls = []; // remaining indices to call
var bgCurrent = -1; // index into bgCard being called
var bgLinesDone = {};
var bgHintUsed = false;

function bgBestKey(level) {
  return "udsp_bingo_best_" + currentLang + "_" + level + "_v1";
}
function bgLoadBest(level) {
  var v = parseInt(localStorage.getItem(bgBestKey(level)), 10);
  return isNaN(v) ? 0 : v;
}
function bgSaveBest(level, score) {
  try {
    localStorage.setItem(bgBestKey(level), String(score));
  } catch (e) {
    /* ignore storage errors (private mode) */
  }
}

function bgHasDefinition(w) {
  return !!(w && w.word && w.definition && splitBilingual(w.definition)[0]);
}

// A card needs 16 DISTINCT headwords: two identical words on the same card
// would make one call have two correct answers.
function bgPool() {
  var seen = {};
  var full = [];
  var stubs = [];
  var raw = WORD_SETS[currentLevel] || [];
  for (var i = 0; i < raw.length; i++) {
    var w = raw[i];
    if (!bgHasDefinition(w)) continue;
    var key = wordKey(w);
    if (seen[key]) continue;
    seen[key] = true;
    // "Similar to: big, huge" rows are synonym lists, not definitions. They
    // still identify a word well enough to call it, but they make a poorer
    // clue, so they are only used when the level cannot fill a card without.
    if (isStubDefinition(w.definition)) stubs.push(w);
    else full.push(w);
  }
  return full.length >= BINGO_CELLS ? full : full.concat(stubs);
}

function refreshBingoStart() {
  var pool = bgPool();
  var enough = pool.length >= BINGO_CELLS;
  setText("bingo-start-level", levelLabel(currentLevel));
  setText("bingo-start-count", enough ? pool.length + " words in the pool" : "");
  setHidden("bingo-start-warning", enough);
  if (!enough) {
    setText(
      "bingo-start-warning",
      "Not enough words at this level to fill a card (need " +
        BINGO_CELLS +
        ") — pick another level or category above."
    );
  }
  var btn = $("bingo-start-btn");
  if (btn) btn.disabled = !enough;
}

function showBingoSetup() {
  bgActive = false;
  setPlayHeader(false);
  refreshBingoStart();
  setHidden("bingo-game", true);
  setHidden("bingo-setup", false);
}

function resetBingo() {
  bgActive = false;
  showBingoSetup();
}

function enterBingo() {
  if (!bgActive) showBingoSetup();
}

function startBingo(level) {
  var pool = bgPool();
  if (pool.length < BINGO_CELLS) {
    showBingoSetup();
    return;
  }
  bgLevel = level;
  bgActive = true;
  bgDone = false;
  bgBusy = false;
  bgScore = 0;
  bgLives = BINGO_LIVES;
  bgLinesDone = {};
  bgHintUsed = false;
  bgCard = shuffle(pool.slice()).slice(0, BINGO_CELLS);
  bgFound = [];
  for (var i = 0; i < BINGO_CELLS; i++) bgFound.push(false);
  bgCalls = shuffle(
    bgCard.map(function (_, i) {
      return i;
    })
  );
  setPlayHeader(true);
  setHidden("bingo-setup", true);
  setHidden("bingo-game", false);
  setHidden("bingo-result", true);
  setText("bingo-level-badge", levelLabel(level));
  setText("bingo-score", "0");
  var hintBtn = $("bingo-hint-btn");
  if (hintBtn) hintBtn.disabled = false;
  bgRenderCard();
  bgRenderLives();
  bgNextCall();
}

function bgRenderCard() {
  var grid = $("bingo-grid");
  if (!grid) return;
  grid.innerHTML = "";
  bgCard.forEach(function (w, i) {
    var cell = document.createElement("button");
    cell.type = "button";
    cell.className = "bingo-cell";
    cell.dataset.index = String(i);
    cell.textContent = w.word;
    cell.addEventListener("click", function () {
      bgPick(i);
    });
    grid.appendChild(cell);
  });
}

function bgCellAt(index) {
  var grid = $("bingo-grid");
  return grid ? grid.children[index] : null;
}

function bgRenderLives() {
  var s = "";
  for (var i = 0; i < BINGO_LIVES; i++) s += i < bgLives ? "❤️" : "🖤";
  setText("bingo-lives", s);
}

function bgNextCall() {
  if (bgDone) return;
  if (!bgCalls.length) {
    endBingo(true);
    return;
  }
  bgCurrent = bgCalls.pop();
  bgBusy = false;
  setText("bingo-progress", "Called: " + (BINGO_CELLS - bgCalls.length) + " / " + BINGO_CELLS);
  setDefinition("bingo-clue", bgCard[bgCurrent].definition);
  setText("bingo-feedback", "");
  bgUndim();
}

function bgUndim() {
  var grid = $("bingo-grid");
  if (!grid) return;
  for (var i = 0; i < grid.children.length; i++) {
    grid.children[i].classList.remove("is-dimmed");
  }
}

function bgPick(index) {
  if (bgDone || bgBusy || bgCurrent < 0) return;
  if (bgFound[index]) return;
  var cell = bgCellAt(index);
  if (!cell) return;
  var correct = index === bgCurrent;
  answeredWord(bgCard[bgCurrent], correct);
  if (correct) {
    bgBusy = true;
    bgFound[index] = true;
    cell.classList.add("is-found");
    bgScore += BINGO_POINTS_HIT;
    var lines = bgNewLines();
    if (lines.length) {
      bgScore += BINGO_POINTS_LINE * lines.length;
      bgFlashLines(lines);
      setText(
        "bingo-feedback",
        "🎉 " + (lines.length > 1 ? lines.length + " lines!" : "Line!") + " +" +
          (BINGO_POINTS_HIT + BINGO_POINTS_LINE * lines.length)
      );
    } else {
      setText("bingo-feedback", "✅ " + bgCard[index].word);
    }
    setText("bingo-score", bgScore);
    setTimeout(function () {
      if (!bgDone) bgNextCall();
    }, lines.length ? 1100 : 650);
  } else {
    bgLives--;
    bgRenderLives();
    cell.classList.add("is-wrong");
    setTimeout(function () {
      cell.classList.remove("is-wrong");
    }, 450);
    setText("bingo-feedback", "❌ Not that one — " + bgLives + " left");
    if (bgLives <= 0) {
      bgBusy = true;
      // Reveal the answer that ended the run, so the last call still teaches.
      var target = bgCellAt(bgCurrent);
      if (target) target.classList.add("is-found");
      setTimeout(function () {
        endBingo(false);
      }, 900);
    }
  }
}

// Row / column / diagonal indices, computed once per call rather than stored,
// because a 4x4 board is 10 lines and the check is cheaper than the bookkeeping.
function bgLines() {
  var lines = [];
  var r, c, line;
  for (r = 0; r < BINGO_SIZE; r++) {
    line = [];
    for (c = 0; c < BINGO_SIZE; c++) line.push(r * BINGO_SIZE + c);
    lines.push({ id: "r" + r, cells: line });
  }
  for (c = 0; c < BINGO_SIZE; c++) {
    line = [];
    for (r = 0; r < BINGO_SIZE; r++) line.push(r * BINGO_SIZE + c);
    lines.push({ id: "c" + c, cells: line });
  }
  line = [];
  for (r = 0; r < BINGO_SIZE; r++) line.push(r * BINGO_SIZE + r);
  lines.push({ id: "d0", cells: line });
  line = [];
  for (r = 0; r < BINGO_SIZE; r++) line.push(r * BINGO_SIZE + (BINGO_SIZE - 1 - r));
  lines.push({ id: "d1", cells: line });
  return lines;
}

function bgNewLines() {
  var all = bgLines();
  var fresh = [];
  for (var i = 0; i < all.length; i++) {
    if (bgLinesDone[all[i].id]) continue;
    var complete = all[i].cells.every(function (idx) {
      return bgFound[idx];
    });
    if (complete) {
      bgLinesDone[all[i].id] = true;
      fresh.push(all[i]);
    }
  }
  return fresh;
}

function bgFlashLines(lines) {
  lines.forEach(function (line) {
    line.cells.forEach(function (idx) {
      var cell = bgCellAt(idx);
      if (!cell) return;
      cell.classList.add("is-line");
      setTimeout(function () {
        cell.classList.remove("is-line");
      }, 900);
    });
  });
}

// One hint per card: dim every cell except three decoys and the answer. It
// shortens the scan without naming the word.
function bgHint() {
  if (bgDone || bgBusy || bgHintUsed || bgCurrent < 0) return;
  bgHintUsed = true;
  var btn = $("bingo-hint-btn");
  if (btn) btn.disabled = true;
  var candidates = [];
  for (var i = 0; i < BINGO_CELLS; i++) {
    if (i !== bgCurrent && !bgFound[i]) candidates.push(i);
  }
  var keep = {};
  keep[bgCurrent] = true;
  shuffle(candidates)
    .slice(0, 3)
    .forEach(function (i) {
      keep[i] = true;
    });
  for (var j = 0; j < BINGO_CELLS; j++) {
    var cell = bgCellAt(j);
    if (cell && !keep[j] && !bgFound[j]) cell.classList.add("is-dimmed");
  }
}

function endBingo(cleared) {
  bgDone = true;
  if (cleared) bgScore += BINGO_POINTS_FULL;
  setText("bingo-score", bgScore);
  var best = bgLoadBest(bgLevel);
  var isNewBest = bgScore > best;
  if (isNewBest) bgSaveBest(bgLevel, bgScore);
  var title = $("bingo-result-title");
  if (title) {
    title.textContent = cleared ? "🎱 BINGO! Full card!" : "💥 Out of lives";
    title.classList.toggle("win", cleared);
    title.classList.toggle("lose", !cleared);
  }
  var hits = bgFound.filter(Boolean).length;
  setText("bingo-final-score", bgScore + " pts · " + hits + " / " + BINGO_CELLS + " found");
  var bestEl = $("bingo-final-best");
  if (bestEl) {
    bestEl.textContent = isNewBest ? "🏆 New best!" : "Best: " + Math.max(best, bgScore) + " pts";
  }
  setHidden("bingo-result", false);
}

on("bingo-hint-btn", "click", bgHint);
on("bingo-start-btn", "click", function () {
  startBingo(currentLevel);
});
on("bingo-back", "click", showBingoSetup);
on("bingo-change", "click", showBingoSetup);
on("bingo-restart", "click", function () {
  startBingo(bgLevel);
});

onLevelChange(resetBingo);
