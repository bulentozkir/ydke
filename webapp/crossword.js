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

/* Top Words — Crossword game page logic. Requires shared.js.
 * Generates a real intersecting grid client-side: the first word is placed
 * across, then every following candidate is greedily crossed onto an
 * already-placed word wherever a shared letter allows it, same idea a human
 * setter uses for a small themed grid. Several shuffled attempts are tried
 * and the one that placed the most words wins, since a single greedy pass
 * can dead-end early depending on word order.
 *
 * Grid letters are compared in FOLDED form (accents stripped, uppercased) --
 * same simplification Word Guess and Daily Challenge use for their letter
 * feedback -- so a crossing between an accented and a plain letter is never
 * incorrectly blocked, and typing does not need to reproduce diacritics.
 */
"use strict";

var CW_WORD_COUNT = 6;
var CW_MIN_LEN = 3;
var CW_MAX_LEN = 9;
var CW_GEN_ATTEMPTS = 6;

var cwActive = false;
var cwLevel = null;
var cwDone = false;
var cwPlaced = []; // [{ entry, bare, row, col, dir: 'A'|'D', number }]
var cwGrid = []; // [row][col] -> null | { letter, words: [{wordIndex, dir}] }
var cwRows = 0;
var cwCols = 0;
var cwActiveCell = null; // { row, col, dir }
var cwSolvedWords = {}; // wordIndex -> true
var cwReveals = 0;
var cwStartTime = null;
var cwHiddenAt = null;
var cwElapsedSec = 0;
var cwTimerInterval = null;

function cwBestKey(level) {
  return "udsp_crossword_best_" + currentLang + "_" + level + "_v1";
}
function cwLoadBest(level) {
  try {
    return JSON.parse(localStorage.getItem(cwBestKey(level))) || null;
  } catch (e) {
    return null;
  }
}
function cwSaveBest(level, rec) {
  try {
    localStorage.setItem(cwBestKey(level), JSON.stringify(rec));
  } catch (e) {
    /* ignore storage errors (private mode) */
  }
}
function cwFormatTime(sec) {
  var m = Math.floor(sec / 60);
  var s = sec % 60;
  return m + ":" + (s < 10 ? "0" : "") + s;
}

function cwStripArticle(word) {
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
function cwFold(ch) {
  var s = String(ch == null ? "" : ch);
  s = s.normalize ? s.normalize("NFD").replace(/[\u0300-\u036f]/g, "") : s;
  return s.toUpperCase();
}
function cwEligible(w) {
  if (!w || !w.word || !w.definition) return false;
  var bare = cwStripArticle(w.word);
  return (
    !!bare &&
    /^[A-Za-zÀ-ÖØ-öø-ÿ]+$/.test(bare) &&
    !/ß/i.test(bare) &&
    bare.length >= CW_MIN_LEN &&
    bare.length <= CW_MAX_LEN
  );
}
function cwPool(level) {
  return (WORD_SETS[level] || []).filter(cwEligible);
}

function cwTryPlacement(candidates) {
  var placed = [];
  var cellMap = {};
  function key(r, c) {
    return r + "," + c;
  }
  function canPlace(word, row, col, dir) {
    for (var i = 0; i < word.length; i++) {
      var r = dir === "A" ? row : row + i;
      var c = dir === "A" ? col + i : col;
      var k = key(r, c);
      if (cellMap[k] !== undefined && cellMap[k] !== word[i]) return false;
    }
    return true;
  }
  function place(entry, word, row, col, dir) {
    for (var i = 0; i < word.length; i++) {
      var r = dir === "A" ? row : row + i;
      var c = dir === "A" ? col + i : col;
      cellMap[key(r, c)] = word[i];
    }
    placed.push({ entry: entry, bare: word, row: row, col: col, dir: dir });
  }

  var first = candidates[0];
  place(first.entry, first.bare, 0, 0, "A");

  for (var ci = 1; ci < candidates.length && placed.length < CW_WORD_COUNT; ci++) {
    var cand = candidates[ci];
    var word = cand.bare;
    var spot = null;
    for (var pi = 0; pi < placed.length && !spot; pi++) {
      var p = placed[pi];
      for (var pj = 0; pj < p.bare.length && !spot; pj++) {
        var pLetter = p.bare[pj];
        for (var wj = 0; wj < word.length; wj++) {
          if (word[wj] !== pLetter) continue;
          var newDir = p.dir === "A" ? "D" : "A";
          var row, col;
          if (p.dir === "A") {
            row = p.row - wj;
            col = p.col + pj;
          } else {
            row = p.row + pj;
            col = p.col - wj;
          }
          if (canPlace(word, row, col, newDir)) {
            spot = { row: row, col: col, dir: newDir };
            break;
          }
        }
      }
    }
    if (spot) place(cand.entry, word, spot.row, spot.col, spot.dir);
  }
  return placed;
}

function cwGenerate(level) {
  var seen = {};
  var eligible = cwPool(level)
    .map(function (w) {
      return { entry: w, bare: cwFold(cwStripArticle(w.word)) };
    })
    .filter(function (e) {
      if (!e.bare || seen[e.bare]) return false;
      seen[e.bare] = true;
      return true;
    });
  if (eligible.length < 3) return null;

  var best = null;
  for (var a = 0; a < CW_GEN_ATTEMPTS; a++) {
    var shuffled = shuffle(eligible.slice()).slice(0, Math.min(eligible.length, 30));
    var placed = cwTryPlacement(shuffled);
    if (!best || placed.length > best.length) best = placed;
    if (best.length >= CW_WORD_COUNT) break;
  }
  if (!best || best.length < 3) return null;
  return best;
}

function cwNormalizeAndNumber(placed) {
  var minRow = Math.min.apply(
    null,
    placed.map(function (p) {
      return p.row;
    })
  );
  var minCol = Math.min.apply(
    null,
    placed.map(function (p) {
      return p.col;
    })
  );
  placed.forEach(function (p) {
    p.row -= minRow;
    p.col -= minCol;
  });

  var starts = placed.map(function (p, i) {
    return { row: p.row, col: p.col, idx: i };
  });
  starts.sort(function (a, b) {
    return a.row - b.row || a.col - b.col;
  });
  var num = 0;
  var lastKey = null;
  starts.forEach(function (s) {
    var k = s.row + "," + s.col;
    if (k !== lastKey) {
      num++;
      lastKey = k;
    }
    placed[s.idx].number = num;
  });

  var maxRow = 0,
    maxCol = 0;
  placed.forEach(function (p) {
    var endRow = p.dir === "D" ? p.row + p.bare.length - 1 : p.row;
    var endCol = p.dir === "A" ? p.col + p.bare.length - 1 : p.col;
    maxRow = Math.max(maxRow, endRow);
    maxCol = Math.max(maxCol, endCol);
  });
  return { rows: maxRow + 1, cols: maxCol + 1 };
}

function cwBuildGridModel(placed, rows, cols) {
  var grid = [];
  for (var r = 0; r < rows; r++) {
    var row = [];
    for (var c = 0; c < cols; c++) row.push(null);
    grid.push(row);
  }
  placed.forEach(function (p, wIdx) {
    for (var i = 0; i < p.bare.length; i++) {
      var r = p.dir === "A" ? p.row : p.row + i;
      var c = p.dir === "A" ? p.col + i : p.col;
      if (!grid[r][c]) grid[r][c] = { letter: p.bare[i], words: [] };
      grid[r][c].words.push({ wordIndex: wIdx, dir: p.dir });
    }
  });
  return grid;
}

function refreshCrosswordStart() {
  var pool = cwPool(currentLevel);
  var ok = pool.length >= 3;
  setText("crossword-start-level", levelLabel(currentLevel));
  setText(
    "crossword-start-count",
    ok ? pool.length + (pool.length === 1 ? " word" : " words") + " available" : ""
  );
  setHidden("crossword-start-warning", ok);
  if (!ok) {
    setText(
      "crossword-start-warning",
      "Not enough short single-word entries at this level (needs 3+) — pick another above."
    );
  }
  var btn = $("crossword-start-btn");
  if (btn) btn.disabled = !ok;
  var best = cwLoadBest(currentLevel);
  setText(
    "crossword-start-best",
    best ? "🏆 Best: " + cwFormatTime(best.time) + " · " + best.reveals + " reveals" : ""
  );
}

function showCrosswordSetup() {
  cwActive = false;
  setPlayHeader(false);
  stopCrosswordTimer();
  cwHiddenAt = null;
  refreshCrosswordStart();
  setHidden("crossword-game", true);
  setHidden("crossword-setup", false);
}
function resetCrossword() {
  cwActive = false;
  stopCrosswordTimer();
  showCrosswordSetup();
}
function enterCrossword() {
  if (!cwActive) showCrosswordSetup();
}

function startCrossword(level) {
  var placed = cwGenerate(level);
  if (!placed) {
    refreshCrosswordStart();
    return;
  }
  var dims = cwNormalizeAndNumber(placed);
  cwLevel = level;
  cwActive = true;
  cwDone = false;
  cwPlaced = placed;
  cwRows = dims.rows;
  cwCols = dims.cols;
  cwGrid = cwBuildGridModel(placed, cwRows, cwCols);
  cwSolvedWords = {};
  cwReveals = 0;
  cwElapsedSec = 0;
  cwStartTime = null;
  cwHiddenAt = null;
  cwActiveCell = null;

  setPlayHeader(true);
  setHidden("crossword-setup", true);
  setHidden("crossword-game", false);
  setHidden("crossword-result", true);
  setText("crossword-level-badge", levelLabel(level));
  setText("crossword-timer", "0:00");
  setText("crossword-progress", "0 / " + placed.length + " solved");

  cwRenderGrid();
  cwRenderClues();
  cwSelectWord(0);
}

function cwCellNode(row, col) {
  var grid = $("crossword-grid");
  return grid ? grid.querySelector('[data-row="' + row + '"][data-col="' + col + '"]') : null;
}

function cwRenderGrid() {
  var host = $("crossword-grid");
  if (!host) return;
  host.style.setProperty("--cw-cols", cwCols);
  var html = [];
  for (var r = 0; r < cwRows; r++) {
    for (var c = 0; c < cwCols; c++) {
      var cell = cwGrid[r][c];
      if (!cell) {
        html.push('<span class="cw-cell is-block"></span>');
        continue;
      }
      var startWord = cwPlaced.find(function (p) {
        return p.row === r && p.col === c;
      });
      html.push(
        '<span class="cw-cell" data-row="' +
          r +
          '" data-col="' +
          c +
          '">' +
          (startWord ? '<em class="cw-number">' + startWord.number + "</em>" : "") +
          '<input maxlength="1" autocomplete="off" autocapitalize="characters" aria-label="Row ' +
          (r + 1) +
          " column " +
          (c + 1) +
          '" />' +
          "</span>"
      );
    }
  }
  host.innerHTML = html.join("");
}

function cwClueLabel(p) {
  return p.number + (p.dir === "A" ? "A" : "D") + ". " + escapeHtml(p.entry.definition || "");
}

function cwRenderClues() {
  var across = [];
  var down = [];
  cwPlaced.forEach(function (p, idx) {
    var solved = !!cwSolvedWords[idx];
    var textId = "cw-clue-text-" + idx;
    var li =
      '<li class="cw-clue' +
      (solved ? " is-solved" : "") +
      '" data-word="' +
      idx +
      '">' +
      '<span class="cw-clue-num">' +
      p.number +
      "</span>" +
      '<button type="button" class="clue-bulb" data-clue-for="' +
      textId +
      '" aria-label="Show clue">💡</button>' +
      '<span class="cw-clue-text clue-gated" id="' +
      textId +
      '" tabindex="0">' +
      escapeHtml(p.entry.definition || "") +
      "</span>" +
      (solved ? '<button type="button" class="cw-clue-audio" data-word="' + idx + '">🔊</button>' : "") +
      "</li>";
    if (p.dir === "A") across.push(li);
    else down.push(li);
  });
  var acrossEl = $("crossword-across");
  var downEl = $("crossword-down");
  if (acrossEl) acrossEl.innerHTML = across.join("");
  if (downEl) downEl.innerHTML = down.join("");
}

function cwWordCells(wordIndex) {
  var p = cwPlaced[wordIndex];
  var cells = [];
  for (var i = 0; i < p.bare.length; i++) {
    cells.push({
      row: p.dir === "A" ? p.row : p.row + i,
      col: p.dir === "A" ? p.col + i : p.col,
      letter: p.bare[i],
    });
  }
  return cells;
}

function cwHighlightActiveWord() {
  var host = $("crossword-grid");
  if (!host) return;
  host.querySelectorAll(".cw-cell.is-active-word").forEach(function (el) {
    el.classList.remove("is-active-word");
  });
  host.querySelectorAll(".cw-cell.is-active-cell").forEach(function (el) {
    el.classList.remove("is-active-cell");
  });
  document.querySelectorAll(".cw-clue.is-active").forEach(function (el) {
    el.classList.remove("is-active");
  });
  if (!cwActiveCell) return;
  var cell = cwGrid[cwActiveCell.row][cwActiveCell.col];
  var match = cell.words.find(function (w) {
    return w.dir === cwActiveCell.dir;
  });
  if (!match) return;
  cwWordCells(match.wordIndex).forEach(function (wc) {
    var node = cwCellNode(wc.row, wc.col);
    if (node) node.classList.add("is-active-word");
  });
  var current = cwCellNode(cwActiveCell.row, cwActiveCell.col);
  if (current) current.classList.add("is-active-cell");
  var clueNode = document.querySelector('.cw-clue[data-word="' + match.wordIndex + '"]');
  if (clueNode) clueNode.classList.add("is-active");
}

function cwSelectCell(row, col, preferDir) {
  var cell = cwGrid[row] && cwGrid[row][col];
  if (!cell) return;
  var dirs = cell.words.map(function (w) {
    return w.dir;
  });
  var dir = preferDir && dirs.indexOf(preferDir) !== -1 ? preferDir : dirs[0];
  if (cwActiveCell && cwActiveCell.row === row && cwActiveCell.col === col && dirs.length > 1) {
    dir = cwActiveCell.dir === "A" ? "D" : "A";
  }
  cwActiveCell = { row: row, col: col, dir: dir };
  cwHighlightActiveWord();
  var node = cwCellNode(row, col);
  var input = node ? node.querySelector("input") : null;
  if (input) input.focus();
}

function cwSelectWord(wordIndex) {
  var p = cwPlaced[wordIndex];
  if (!p) return;
  if (!cwStartTime) startCrosswordTimer();
  cwSelectCell(p.row, p.col, p.dir);
}

function cwAdvance(step) {
  if (!cwActiveCell) return;
  var cell = cwGrid[cwActiveCell.row][cwActiveCell.col];
  var match = cell.words.find(function (w) {
    return w.dir === cwActiveCell.dir;
  });
  if (!match) return;
  var cells = cwWordCells(match.wordIndex);
  var idx = cells.findIndex(function (wc) {
    return wc.row === cwActiveCell.row && wc.col === cwActiveCell.col;
  });
  var next = cells[idx + step];
  if (next) cwSelectCell(next.row, next.col, cwActiveCell.dir);
}

function cwCheckWordSolved(wordIndex) {
  var cells = cwWordCells(wordIndex);
  return cells.every(function (wc) {
    var node = cwCellNode(wc.row, wc.col);
    var input = node ? node.querySelector("input") : null;
    return input && cwFold(input.value) === wc.letter;
  });
}

function cwOnCellInput(row, col) {
  if (!cwStartTime) startCrosswordTimer();
  var node = cwCellNode(row, col);
  var input = node ? node.querySelector("input") : null;
  if (input) input.value = cwFold(input.value).slice(0, 1);
  cwAdvance(1);

  var cell = cwGrid[row][col];
  cell.words.forEach(function (w) {
    if (cwSolvedWords[w.wordIndex]) return;
    if (cwCheckWordSolved(w.wordIndex)) {
      cwSolvedWords[w.wordIndex] = true;
      answeredWord(cwPlaced[w.wordIndex].entry, true);
    }
  });
  var solvedCount = Object.keys(cwSolvedWords).length;
  setText("crossword-progress", solvedCount + " / " + cwPlaced.length + " solved");
  cwRenderClues();
  cwHighlightActiveWord();

  if (solvedCount === cwPlaced.length) endCrossword();
}

function cwCheckAll() {
  var host = $("crossword-grid");
  if (!host) return;
  host.querySelectorAll(".cw-cell:not(.is-block)").forEach(function (node) {
    var r = parseInt(node.getAttribute("data-row"), 10);
    var c = parseInt(node.getAttribute("data-col"), 10);
    var input = node.querySelector("input");
    node.classList.remove("is-correct", "is-wrong");
    if (!input.value) return;
    node.classList.add(cwFold(input.value) === cwGrid[r][c].letter ? "is-correct" : "is-wrong");
  });
  setTimeout(function () {
    host.querySelectorAll(".cw-cell").forEach(function (node) {
      node.classList.remove("is-correct", "is-wrong");
    });
  }, 900);
}

function cwRevealActiveWord() {
  if (!cwActiveCell || cwDone) return;
  var cell = cwGrid[cwActiveCell.row][cwActiveCell.col];
  var match = cell.words.find(function (w) {
    return w.dir === cwActiveCell.dir;
  });
  if (!match || cwSolvedWords[match.wordIndex]) return;
  cwReveals++;
  cwWordCells(match.wordIndex).forEach(function (wc) {
    var node = cwCellNode(wc.row, wc.col);
    var input = node ? node.querySelector("input") : null;
    if (input) input.value = wc.letter;
  });
  cwSolvedWords[match.wordIndex] = true;
  answeredWord(cwPlaced[match.wordIndex].entry, false);
  var solvedCount = Object.keys(cwSolvedWords).length;
  setText("crossword-progress", solvedCount + " / " + cwPlaced.length + " solved");
  cwRenderClues();
  cwHighlightActiveWord();
  if (solvedCount === cwPlaced.length) endCrossword();
}

function startCrosswordTimer() {
  if (cwTimerInterval) return;
  if (!cwStartTime) cwStartTime = Date.now();
  cwTimerInterval = setInterval(function () {
    cwElapsedSec = Math.floor((Date.now() - cwStartTime) / 1000);
    setText("crossword-timer", cwFormatTime(cwElapsedSec));
  }, 1000);
}
function stopCrosswordTimer() {
  if (cwTimerInterval) {
    clearInterval(cwTimerInterval);
    cwTimerInterval = null;
  }
}
document.addEventListener("visibilitychange", function () {
  if (!cwActive || cwDone || !cwStartTime) return;
  if (document.visibilityState === "hidden") {
    cwElapsedSec = Math.floor((Date.now() - cwStartTime) / 1000);
    cwHiddenAt = Date.now();
    stopCrosswordTimer();
  } else if (cwHiddenAt) {
    cwStartTime += Date.now() - cwHiddenAt;
    cwHiddenAt = null;
    startCrosswordTimer();
  }
});

function endCrossword() {
  cwDone = true;
  stopCrosswordTimer();
  cwHiddenAt = null;
  var record = { time: cwElapsedSec, reveals: cwReveals };
  var best = cwLoadBest(cwLevel);
  var isNewBest =
    !best ||
    record.time < best.time ||
    (record.time === best.time && record.reveals < best.reveals);
  if (isNewBest) cwSaveBest(cwLevel, record);
  setText("crossword-final-time", cwFormatTime(cwElapsedSec));
  setText("crossword-final-reveals", String(cwReveals));
  var bestEl = $("crossword-final-best");
  if (bestEl) {
    bestEl.textContent = isNewBest
      ? "🏆 New best!"
      : "Best: " + cwFormatTime(best.time) + " · " + best.reveals + " reveals";
  }
  setHidden("crossword-result", false);
}

on("crossword-start-btn", "click", function () {
  startCrossword(currentLevel);
});
on("crossword-check-btn", "click", cwCheckAll);
on("crossword-reveal-btn", "click", cwRevealActiveWord);
on("crossword-back", "click", showCrosswordSetup);
on("crossword-change", "click", showCrosswordSetup);
on("crossword-restart", "click", function () {
  startCrossword(cwLevel);
});

document.addEventListener("click", function (e) {
  var cell = e.target.closest && e.target.closest(".cw-cell:not(.is-block)");
  if (cell) {
    cwSelectCell(parseInt(cell.getAttribute("data-row"), 10), parseInt(cell.getAttribute("data-col"), 10));
    return;
  }
  var audioBtn = e.target.closest && e.target.closest(".cw-clue-audio");
  if (audioBtn) {
    var wIdx = parseInt(audioBtn.getAttribute("data-word"), 10);
    var p = cwPlaced[wIdx];
    if (p) speak(p.entry.word);
    return;
  }
  var clue = e.target.closest && e.target.closest(".cw-clue");
  if (clue) cwSelectWord(parseInt(clue.getAttribute("data-word"), 10));
});

document.addEventListener("input", function (e) {
  var cell = e.target.closest && e.target.closest(".cw-cell");
  if (!cell || !cell.parentElement || cell.parentElement.id !== "crossword-grid") return;
  cwOnCellInput(parseInt(cell.getAttribute("data-row"), 10), parseInt(cell.getAttribute("data-col"), 10));
});

document.addEventListener("keydown", function (e) {
  var cell = e.target.closest && e.target.closest(".cw-cell");
  if (!cell || !cell.parentElement || cell.parentElement.id !== "crossword-grid") return;
  if (e.key === "Backspace" && !e.target.value) {
    e.preventDefault();
    cwAdvance(-1);
  }
});

onLevelChange(resetCrossword);
