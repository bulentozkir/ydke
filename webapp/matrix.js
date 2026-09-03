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

/* Top Words — Word Matrix (letter-grid word search) game page logic.
 * Requires shared.js.
 *
 * Target words are placed straight (right / down / diagonal-down-right) into
 * a fixed 8x8 letter grid. Tap a start cell then an end cell in a straight
 * line to select a run of cells; a match is found by comparing the exact set
 * of selected cells (order-agnostic, so either sweep direction works) against
 * each unfound placed word's cells. The word list shows each word's Turkish
 * meaning as a "clue" by default (pure visual search); an optional per-word
 * hint reveals that word's spelling.
 */
"use strict";

var MTX_SIZE = 8;
var MTX_WORD_COUNT = 6;
var matrixActive = false;
var mtxLevel = null;
var mtxGrid = []; // flat array, size*size letters
var mtxPlaced = []; // [{ word, gloss, cells:[{r,c}], found, hintUsed }]
var mtxSelStart = null; // { r, c } | null
var mtxStartTime = null;
var mtxTimerInterval = null;
var mtxHiddenAt = null;
var mtxElapsedSec = 0;
var mtxHintsUsed = 0;
var mtxDone = false;

var MTX_DIRECTIONS = [
  { dr: 0, dc: 1 }, // right
  { dr: 1, dc: 0 }, // down
  { dr: 1, dc: 1 }, // diagonal down-right
];

function mtxGloss(w) {
  var def = w.definition || "";
  var idx = def.indexOf(" - ");
  return idx === -1 ? def : def.slice(idx + 3).trim();
}
function mtxBaseWord(entry) {
  var w = (entry.word || "").replace(/^(der|die|das)\s+/i, "").trim();
  w = w.replace(/^sich\s+/i, "").trim();
  return w;
}
function mtxFold(ch) {
  var b = ch.normalize ? ch.normalize("NFD").replace(/[\u0300-\u036f]/g, "") : ch;
  return b.toUpperCase();
}
function mtxEligible(w) {
  if (!w || !w.word || !w.definition) return false;
  var base = mtxBaseWord(w);
  return !!base && !/\s/.test(base) && base.length >= 3 && base.length <= MTX_SIZE;
}
function mtxBestKey(level) {
  return "udsp_matrix_best_" + currentLang + "_" + level + "_v1";
}
function mtxLoadBest(level) {
  try {
    return JSON.parse(localStorage.getItem(mtxBestKey(level))) || null;
  } catch (e) {
    return null;
  }
}
function mtxSaveBest(level, rec) {
  try {
    localStorage.setItem(mtxBestKey(level), JSON.stringify(rec));
  } catch (e) {
    /* ignore storage errors (private mode) */
  }
}
function mtxFormatTime(sec) {
  var m = Math.floor(sec / 60);
  var s = sec % 60;
  return m + ":" + (s < 10 ? "0" : "") + s;
}

function refreshMatrixStart() {
  var set = (WORD_SETS[currentLevel] || []).filter(mtxEligible);
  var ok = set.length >= 5;
  setText("matrix-start-level", levelLabel(currentLevel));
  setText(
    "matrix-start-count",
    ok ? set.length + (set.length === 1 ? " word" : " words") + " available" : ""
  );
  setHidden("matrix-start-warning", ok);
  if (!ok) {
    setText(
      "matrix-start-warning",
      "Not enough words at this level (needs 5+) — pick another above."
    );
  }
  var btn = $("matrix-start-btn");
  if (btn) btn.disabled = !ok;
}

function showMatrixSetup() {
  matrixActive = false;
  setPlayHeader(false);
  stopMatrixTimer();
  mtxHiddenAt = null;
  refreshMatrixStart();
  setHidden("matrix-game", true);
  setHidden("matrix-setup", false);
}

function resetMatrix() {
  matrixActive = false;
  stopMatrixTimer();
  showMatrixSetup();
}

function enterMatrix() {
  if (!matrixActive) showMatrixSetup();
}

function startMatrix(level) {
  mtxLevel = level;
  matrixActive = true;
  setPlayHeader(true);
  setHidden("matrix-setup", true);
  setHidden("matrix-game", false);
  setText("matrix-level-badge", levelLabel(level));
  newMatrixRound();
}

function mtxBuildGrid(words) {
  var size = MTX_SIZE;
  var cells = new Array(size * size).fill(null);
  var placed = [];
  words.forEach(function (w) {
    var letters = mtxBaseWord(w).split("").map(mtxFold);
    var tries = 80;
    var done = false;
    while (tries-- > 0 && !done) {
      var dir = MTX_DIRECTIONS[Math.floor(Math.random() * MTX_DIRECTIONS.length)];
      var len = letters.length;
      var maxRow = dir.dr ? size - len : size - 1;
      var maxCol = dir.dc ? size - len : size - 1;
      if (maxRow < 0 || maxCol < 0) break;
      var row = Math.floor(Math.random() * (maxRow + 1));
      var col = Math.floor(Math.random() * (maxCol + 1));
      var fits = true;
      for (var i = 0; i < len; i++) {
        var r = row + dir.dr * i;
        var c = col + dir.dc * i;
        var idx = r * size + c;
        if (cells[idx] && cells[idx] !== letters[i]) {
          fits = false;
          break;
        }
      }
      if (fits) {
        var cellList = [];
        for (var j = 0; j < len; j++) {
          var rr = row + dir.dr * j;
          var cc = col + dir.dc * j;
          cells[rr * size + cc] = letters[j];
          cellList.push({ r: rr, c: cc });
        }
        placed.push({
          word: w.word,
          gloss: mtxGloss(w),
          cells: cellList,
          found: false,
          hintUsed: false,
        });
        done = true;
      }
    }
  });
  for (var i = 0; i < cells.length; i++) {
    if (!cells[i]) cells[i] = String.fromCharCode(65 + Math.floor(Math.random() * 26));
  }
  return { cells: cells, placed: placed };
}

function newMatrixRound() {
  stopMatrixTimer();
  var pool = shuffle((WORD_SETS[mtxLevel] || []).filter(mtxEligible));
  var built = mtxBuildGrid(pool.slice(0, MTX_WORD_COUNT));
  if (built.placed.length < 3 && pool.length > MTX_WORD_COUNT) {
    built = mtxBuildGrid(pool.slice(0, MTX_WORD_COUNT * 2));
  }
  mtxGrid = built.cells;
  mtxPlaced = built.placed;
  mtxSelStart = null;
  mtxHintsUsed = 0;
  mtxElapsedSec = 0;
  mtxStartTime = null;
  mtxHiddenAt = null;
  mtxDone = false;
  setText("matrix-timer", "0:00");
  setHidden("matrix-result", true);
  renderMatrixBest();
  renderMatrixGrid();
  renderMatrixWords();
}

function renderMatrixBest() {
  var el = $("matrix-best");
  if (!el) return;
  var best = mtxLoadBest(mtxLevel);
  el.textContent = best
    ? "Best: " + mtxFormatTime(best.time) + " · " + best.hints + " hints"
    : "";
}

function startMatrixTimer() {
  if (mtxTimerInterval) return;
  if (!mtxStartTime) mtxStartTime = Date.now();
  mtxTimerInterval = setInterval(function () {
    mtxElapsedSec = Math.floor((Date.now() - mtxStartTime) / 1000);
    setText("matrix-timer", mtxFormatTime(mtxElapsedSec));
  }, 1000);
}
function stopMatrixTimer() {
  if (mtxTimerInterval) {
    clearInterval(mtxTimerInterval);
    mtxTimerInterval = null;
  }
}

function mtxCellIsFound(r, c) {
  return mtxPlaced.some(function (p) {
    return (
      p.found &&
      p.cells.some(function (cell) {
        return cell.r === r && cell.c === c;
      })
    );
  });
}

function renderMatrixGrid() {
  var box = $("matrix-grid");
  if (!box) return;
  box.innerHTML = "";
  for (var r = 0; r < MTX_SIZE; r++) {
    for (var c = 0; c < MTX_SIZE; c++) {
      (function (row, col) {
        var idx = row * MTX_SIZE + col;
        var cell = document.createElement("div");
        cell.className = "matrix-cell";
        cell.textContent = mtxGrid[idx];
        if (mtxCellIsFound(row, col)) cell.classList.add("is-found");
        cell.addEventListener("click", function () {
          mtxCellClick(row, col);
        });
        box.appendChild(cell);
        cell.setAttribute("data-r", row);
        cell.setAttribute("data-c", col);
      })(r, c);
    }
  }
}

function renderMatrixWords() {
  var box = $("matrix-words");
  if (!box) return;
  box.innerHTML = "";
  mtxPlaced.forEach(function (p, i) {
    var item = document.createElement("div");
    item.className = "matrix-word-item" + (p.found ? " is-found" : "");
    var label = document.createElement("span");
    label.textContent = p.found
      ? p.word
      : p.hintUsed
      ? p.word + " — " + p.gloss
      : p.gloss;
    item.appendChild(label);
    if (!p.found) {
      var hintBtn = document.createElement("button");
      hintBtn.type = "button";
      hintBtn.className = "matrix-hint-btn";
      hintBtn.textContent = "💡";
      hintBtn.disabled = p.hintUsed;
      hintBtn.addEventListener("click", function () {
        mtxUseHint(i);
      });
      item.appendChild(hintBtn);
    } else {
      var audioBtn = document.createElement("button");
      audioBtn.type = "button";
      audioBtn.className = "matrix-hint-btn";
      audioBtn.textContent = "🔊";
      audioBtn.setAttribute("aria-label", "Listen");
      audioBtn.addEventListener("click", function () {
        speak(p.word);
      });
      item.appendChild(audioBtn);
    }
    box.appendChild(item);
  });
}

function mtxUseHint(i) {
  if (mtxDone) return;
  var p = mtxPlaced[i];
  if (!p || p.found || p.hintUsed) return;
  p.hintUsed = true;
  mtxHintsUsed++;
  renderMatrixWords();
}

function mtxLinePath(start, end) {
  var dr = end.r - start.r;
  var dc = end.c - start.c;
  if (dr === 0 && dc === 0) return null;
  if (!(dr === 0 || dc === 0 || Math.abs(dr) === Math.abs(dc))) return null;
  var stepR = dr === 0 ? 0 : dr / Math.abs(dr);
  var stepC = dc === 0 ? 0 : dc / Math.abs(dc);
  var len = Math.max(Math.abs(dr), Math.abs(dc)) + 1;
  var path = [];
  for (var i = 0; i < len; i++) {
    path.push({ r: start.r + stepR * i, c: start.c + stepC * i });
  }
  return path;
}

function mtxCellsEqual(pathA, pathB) {
  if (pathA.length !== pathB.length) return false;
  var setA = pathA
    .map(function (c) {
      return c.r + "," + c.c;
    })
    .sort();
  var setB = pathB
    .map(function (c) {
      return c.r + "," + c.c;
    })
    .sort();
  return setA.join("|") === setB.join("|");
}

function mtxHighlightSelection(path) {
  var box = $("matrix-grid");
  if (!box) return;
  box.querySelectorAll(".matrix-cell").forEach(function (cell) {
    cell.classList.remove("is-selected");
  });
  path.forEach(function (cellPos) {
    var sel = box.querySelector(
      '.matrix-cell[data-r="' + cellPos.r + '"][data-c="' + cellPos.c + '"]'
    );
    if (sel) sel.classList.add("is-selected");
  });
}

function mtxCellClick(r, c) {
  if (mtxDone) return;
  if (mtxCellIsFound(r, c)) return;
  if (!mtxStartTime) startMatrixTimer();

  if (!mtxSelStart) {
    mtxSelStart = { r: r, c: c };
    mtxHighlightSelection([{ r: r, c: c }]);
    return;
  }
  if (mtxSelStart.r === r && mtxSelStart.c === c) {
    mtxSelStart = null;
    mtxHighlightSelection([]);
    return;
  }
  var path = mtxLinePath(mtxSelStart, { r: r, c: c });
  mtxSelStart = null;
  if (!path) {
    mtxSelStart = { r: r, c: c };
    mtxHighlightSelection([{ r: r, c: c }]);
    return;
  }
  var match = mtxPlaced.find(function (p) {
    return !p.found && mtxCellsEqual(path, p.cells);
  });
  if (match) {
    match.found = true;
    mtxHighlightSelection([]);
    renderMatrixGrid();
    renderMatrixWords();
    if (
      mtxPlaced.every(function (p) {
        return p.found;
      })
    ) {
      finishMatrix();
    }
  } else {
    mtxHighlightSelection(path);
    setTimeout(function () {
      mtxHighlightSelection([]);
    }, 400);
  }
}

function finishMatrix() {
  mtxDone = true;
  stopMatrixTimer();
  mtxHiddenAt = null;
  var best = mtxLoadBest(mtxLevel);
  var isNewBest =
    !best ||
    mtxElapsedSec < best.time ||
    (mtxElapsedSec === best.time && mtxHintsUsed < best.hints);
  if (isNewBest) mtxSaveBest(mtxLevel, { time: mtxElapsedSec, hints: mtxHintsUsed });
  setText("matrix-final-time", mtxFormatTime(mtxElapsedSec));
  setText("matrix-final-hints", mtxHintsUsed);
  var bestEl = $("matrix-final-best");
  if (bestEl) {
    var b = mtxLoadBest(mtxLevel);
    bestEl.textContent = isNewBest
      ? "🏆 New best!"
      : "Best: " + mtxFormatTime(b.time) + " · " + b.hints + " hints";
  }
  setHidden("matrix-result", false);
}

on("matrix-back", "click", showMatrixSetup);
on("matrix-change", "click", showMatrixSetup);
on("matrix-restart", "click", newMatrixRound);
on("matrix-start-btn", "click", function () {
  startMatrix(currentLevel);
});

document.addEventListener("visibilitychange", function () {
  if (!matrixActive || mtxDone || !mtxStartTime) return;
  if (document.visibilityState === "hidden") {
    mtxElapsedSec = Math.floor((Date.now() - mtxStartTime) / 1000);
    mtxHiddenAt = Date.now();
    stopMatrixTimer();
  } else if (mtxHiddenAt) {
    mtxStartTime += Date.now() - mtxHiddenAt;
    mtxHiddenAt = null;
    startMatrixTimer();
  }
});

onLevelChange(resetMatrix);
