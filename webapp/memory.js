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

/* Top Words — Matching Pairs (memory) game page logic. Requires shared.js. */
"use strict";

var MEM_PAIRS = 8; // 8 pairs = 16 cards
var memoryActive = false;
var memLevel = null;
var memCards = []; // [{ pairId, kind: 'word'|'gloss', text, flipped, matched }]
var memFirstIdx = null;
var memSecondIdx = null;
var memBusy = false;
var memMoves = 0;
var memMatches = 0;
var memStartTime = null;
var memTimerInterval = null;
var memHiddenAt = null;
var memElapsedSec = 0;
var memPeeking = false;

function memGloss(w) {
  var def = w.definition || "";
  var idx = def.indexOf(" - ");
  return idx === -1 ? def : def.slice(idx + 3).trim();
}
function memEligible(w) {
  return !!(w && w.word && w.word.trim() && memGloss(w));
}
function memBestKey(level) {
  return "udsp_memory_best_" + currentLang + "_" + level + "_v1";
}
function memLoadBest(level) {
  try {
    return JSON.parse(localStorage.getItem(memBestKey(level))) || null;
  } catch (e) {
    return null;
  }
}
function memSaveBest(level, rec) {
  try {
    localStorage.setItem(memBestKey(level), JSON.stringify(rec));
  } catch (e) {
    /* ignore storage errors (private mode) */
  }
}
function memFormatTime(sec) {
  var m = Math.floor(sec / 60);
  var s = sec % 60;
  return m + ":" + (s < 10 ? "0" : "") + s;
}

function refreshMemoryStart() {
  var set = (WORD_SETS[currentLevel] || []).filter(memEligible);
  var ok = set.length >= MEM_PAIRS;
  setText("memory-start-level", levelLabel(currentLevel));
  setText(
    "memory-start-count",
    ok ? set.length + (set.length === 1 ? " word" : " words") + " available" : ""
  );
  setHidden("memory-start-warning", ok);
  if (!ok) {
    setText(
      "memory-start-warning",
      "Not enough words at this level (needs " +
        MEM_PAIRS +
        "+) — pick another above."
    );
  }
  var btn = $("memory-start-btn");
  if (btn) btn.disabled = !ok;
}

function showMemorySetup() {
  memoryActive = false;
  setPlayHeader(false);
  stopMemoryTimer();
  memHiddenAt = null;
  refreshMemoryStart();
  setHidden("memory-game", true);
  setHidden("memory-setup", false);
}

function resetMemory() {
  memoryActive = false;
  stopMemoryTimer();
  showMemorySetup();
}

function enterMemory() {
  if (!memoryActive) showMemorySetup();
}

function startMemory(level) {
  memLevel = level;
  memoryActive = true;
  setPlayHeader(true);
  setHidden("memory-setup", true);
  setHidden("memory-game", false);
  setText("memory-level-badge", levelLabel(level));
  newMemoryRound();
}

function newMemoryRound() {
  stopMemoryTimer();
  var pool = (WORD_SETS[memLevel] || []).filter(memEligible);
  var chosen = shuffle(pool).slice(0, MEM_PAIRS);
  var cards = [];
  chosen.forEach(function (w, i) {
    cards.push({ pairId: i, kind: "word", text: w.word, flipped: false, matched: false });
    cards.push({ pairId: i, kind: "gloss", text: memGloss(w), flipped: false, matched: false });
  });
  memCards = shuffle(cards);
  memFirstIdx = null;
  memSecondIdx = null;
  memBusy = false;
  memMoves = 0;
  memMatches = 0;
  memElapsedSec = 0;
  memStartTime = null;
  memHiddenAt = null;
  memPeeking = false;
  var memPeekBtn = $("memory-peek-btn");
  if (memPeekBtn) memPeekBtn.disabled = false;
  setText("memory-moves", "0");
  setText("memory-timer", "0:00");
  setHidden("memory-result", true);
  renderBestMemory();
  renderMemoryGrid();
}

function renderBestMemory() {
  var el = $("memory-best");
  if (!el) return;
  var best = memLoadBest(memLevel);
  el.textContent = best
    ? "Best: " + memFormatTime(best.time) + " · " + best.moves + " moves"
    : "";
}

function startMemoryTimer() {
  if (memTimerInterval) return;
  if (!memStartTime) memStartTime = Date.now();
  memTimerInterval = setInterval(function () {
    memElapsedSec = Math.floor((Date.now() - memStartTime) / 1000);
    setText("memory-timer", memFormatTime(memElapsedSec));
  }, 1000);
}
function stopMemoryTimer() {
  if (memTimerInterval) {
    clearInterval(memTimerInterval);
    memTimerInterval = null;
  }
}

function renderMemoryGrid() {
  var box = $("memory-grid");
  if (!box) return;
  box.innerHTML = "";
  memCards.forEach(function (c, i) {
    var card = document.createElement("div");
    var audible = c.matched && c.kind === "word";
    card.className =
      "memory-card" +
      (c.flipped || c.matched || memPeeking ? " is-flipped" : "") +
      (c.matched ? " is-matched" : "") +
      (audible ? " is-audible" : "");
    if (audible) card.setAttribute("aria-label", "Listen: " + c.text);
    card.innerHTML =
      '<div class="memory-card-inner">' +
      '<div class="memory-card-face memory-card-front">❓</div>' +
      '<div class="memory-card-face memory-card-back">' +
      escapeHtml(c.text) +
      (audible ? ' <span class="mem-audio-icon">🔊</span>' : "") +
      "</div>" +
      "</div>";
    card.addEventListener("click", function () {
      memFlip(i);
    });
    box.appendChild(card);
  });
}

function memFlip(i) {
  var matchedCard = memCards[i];
  if (matchedCard && matchedCard.matched) {
    // Matched cards stay face-up; clicking a matched WORD card replays its
    // pronunciation instead of doing nothing (gloss cards have no audio).
    if (matchedCard.kind === "word") speak(matchedCard.text);
    return;
  }
  if (memBusy) return;
  var c = memCards[i];
  if (!c || c.matched || c.flipped) return;
  if (memFirstIdx === null) startMemoryTimer();
  c.flipped = true;
  renderMemoryGrid();
  if (memFirstIdx === null) {
    memFirstIdx = i;
    return;
  }
  memSecondIdx = i;
  memMoves++;
  setText("memory-moves", memMoves);
  memBusy = true;
  var first = memCards[memFirstIdx];
  var second = memCards[memSecondIdx];
  if (first.pairId === second.pairId) {
    first.matched = true;
    second.matched = true;
    memMatches++;
    memFirstIdx = null;
    memSecondIdx = null;
    memBusy = false;
    renderMemoryGrid();
    if (memMatches >= MEM_PAIRS) finishMemory();
  } else {
    setTimeout(function () {
      first.flipped = false;
      second.flipped = false;
      memFirstIdx = null;
      memSecondIdx = null;
      memBusy = false;
      renderMemoryGrid();
    }, 900);
  }
}

// One-time peek: briefly flips every card face-up, then hides them again.
function memPeek() {
  if (!memoryActive || memBusy || memPeeking) return;
  var btn = $("memory-peek-btn");
  if (btn && btn.disabled) return;
  startMemoryTimer();
  memPeeking = true;
  memBusy = true;
  if (btn) btn.disabled = true;
  renderMemoryGrid();
  setTimeout(function () {
    memPeeking = false;
    memBusy = false;
    renderMemoryGrid();
  }, 1200);
}

function finishMemory() {
  stopMemoryTimer();
  memHiddenAt = null;
  var best = memLoadBest(memLevel);
  var isNewBest =
    !best ||
    memElapsedSec < best.time ||
    (memElapsedSec === best.time && memMoves < best.moves);
  if (isNewBest) memSaveBest(memLevel, { time: memElapsedSec, moves: memMoves });

  setText("memory-final-time", memFormatTime(memElapsedSec));
  setText("memory-final-moves", memMoves);
  var bestEl = $("memory-final-best");
  if (bestEl) {
    var b = memLoadBest(memLevel);
    bestEl.textContent = isNewBest
      ? "🏆 New best!"
      : "Best: " + memFormatTime(b.time) + " · " + b.moves + " moves";
  }
  setHidden("memory-result", false);
}

on("memory-start-btn", "click", function () {
  startMemory(currentLevel);
});
on("memory-back", "click", showMemorySetup);
on("memory-change", "click", showMemorySetup);
on("memory-restart", "click", newMemoryRound);
on("memory-peek-btn", "click", memPeek);

document.addEventListener("visibilitychange", function () {
  if (!memoryActive || memMatches >= MEM_PAIRS || !memStartTime) return;
  if (document.visibilityState === "hidden") {
    memElapsedSec = Math.floor((Date.now() - memStartTime) / 1000);
    memHiddenAt = Date.now();
    stopMemoryTimer();
  } else if (memHiddenAt) {
    memStartTime += Date.now() - memHiddenAt;
    memHiddenAt = null;
    startMemoryTimer();
  }
});

onLevelChange(resetMemory);
