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

/* Top Words — Word Scramble game page logic. Requires shared.js. */
"use strict";

var SCR_MAX_WRONG = 3;
var scrambleActive = false;
var scrambleWins = 0;
var scrambleLosses = 0;
var scrLevel = null;
var scrWord = null; // current word entry
var scrTarget = ""; // base word being unscrambled (article stripped)
var scrTiles = []; // [{ char, used }]
var scrSlots = []; // [{ char, tileIdx, locked } | null]
var scrWrong = 0;
var scrHints = 0;
var scrDone = false;

function scrStripArticle(word) {
  return (word || "").replace(/^(der|die|das)\s+/i, "").trim();
}
function scrBaseWord(entry) {
  var w = scrStripArticle(entry.word);
  w = w.replace(/^sich\s+/i, "").trim();
  return w;
}
function scrEligible(w) {
  if (!w || !w.word) return false;
  var base = scrBaseWord(w);
  return !!base && !/\s/.test(base) && base.length >= 3 && base.length <= 12;
}
function scrPickWord(level) {
  var all = (WORD_SETS[level] || []).filter(scrEligible);
  if (!all.length) return null;
  return all[Math.floor(Math.random() * all.length)];
}

function refreshScrambleStart() {
  var set = (WORD_SETS[currentLevel] || []).filter(scrEligible);
  var ok = set.length > 0;
  setText("scramble-start-level", levelLabel(currentLevel));
  setText(
    "scramble-start-count",
    ok ? set.length + (set.length === 1 ? " word" : " words") + " available" : ""
  );
  setHidden("scramble-start-warning", ok);
  if (!ok) {
    setText(
      "scramble-start-warning",
      "No words at this level — pick another above."
    );
  }
  var btn = $("scramble-start-btn");
  if (btn) btn.disabled = !ok;
}

function showScrambleSetup() {
  scrambleActive = false;
  setPlayHeader(false);
  refreshScrambleStart();
  setHidden("scramble-game", true);
  setHidden("scramble-setup", false);
}

function resetScramble() {
  scrambleActive = false;
  showScrambleSetup();
}

function enterScramble() {
  if (!scrambleActive) showScrambleSetup();
}

function startScramble(level) {
  scrLevel = level;
  scrambleActive = true;
  setPlayHeader(true);
  setHidden("scramble-setup", true);
  setHidden("scramble-game", false);
  setText("scramble-level-badge", levelLabel(level));
  setText("scramble-max", SCR_MAX_WRONG);
  setText("scramble-wins", scrambleWins);
  setText("scramble-losses", scrambleLosses);
  newScrambleWord();
}

function scrShuffleLetters(str) {
  var arr = str.split("");
  var shuffled = arr;
  var attempts = 0;
  do {
    shuffled = shuffle(arr);
    attempts++;
  } while (
    shuffled.join("").toLowerCase() === str.toLowerCase() &&
    attempts < 8 &&
    arr.length > 1
  );
  return shuffled.join("");
}

function newScrambleWord() {
  var w = scrPickWord(scrLevel);
  if (!w) {
    showScrambleSetup();
    return;
  }
  scrWord = w;
  scrTarget = scrBaseWord(w);
  scrWrong = 0;
  scrHints = 0;
  scrDone = false;

  var scrambled = scrShuffleLetters(scrTarget);
  scrTiles = scrambled.split("").map(function (ch) {
    return { char: ch, used: false };
  });
  scrSlots = scrTarget.split("").map(function () {
    return null;
  });

  setText("scramble-wrong", "0");
  setText("scramble-pos", w.pos || "");
  setText("scramble-clue", w.definition || "");
  setHidden("scramble-result", true);
  var hintBtn = $("scramble-hint-btn");
  if (hintBtn) hintBtn.disabled = false;
  renderScrambleTiles();
  renderScrambleAnswer();
}

function renderScrambleAnswer() {
  var box = $("scramble-answer");
  if (!box) return;
  box.classList.remove("is-shaking");
  var html = "";
  scrSlots.forEach(function (slot, i) {
    if (!slot) {
      html +=
        '<button type="button" class="scramble-slot is-empty" data-idx="' +
        i +
        '"></button>';
    } else {
      html +=
        '<button type="button" class="scramble-slot is-filled' +
        (slot.locked ? " is-locked" : "") +
        '" data-idx="' +
        i +
        '">' +
        escapeHtml(slot.char) +
        "</button>";
    }
  });
  box.innerHTML = html;
  box
    .querySelectorAll(".scramble-slot.is-filled:not(.is-locked)")
    .forEach(function (btn) {
      btn.addEventListener("click", function () {
        scrClearSlot(parseInt(btn.getAttribute("data-idx"), 10));
      });
    });
}

function renderScrambleTiles() {
  var box = $("scramble-tiles");
  if (!box) return;
  box.innerHTML = "";
  scrTiles.forEach(function (tile, i) {
    var b = document.createElement("button");
    b.type = "button";
    b.className = "scramble-tile";
    b.textContent = tile.char;
    b.disabled = tile.used;
    b.addEventListener("click", function () {
      scrPlaceTile(i);
    });
    box.appendChild(b);
  });
}

function scrPlaceTile(tileIdx) {
  if (scrDone || scrTiles[tileIdx].used) return;
  var emptyIdx = scrSlots.findIndex(function (s) {
    return !s;
  });
  if (emptyIdx === -1) return;
  scrTiles[tileIdx].used = true;
  scrSlots[emptyIdx] = {
    char: scrTiles[tileIdx].char,
    tileIdx: tileIdx,
    locked: false,
  };
  renderScrambleTiles();
  renderScrambleAnswer();
  scrMaybeCheck();
}

function scrClearSlot(idx) {
  if (scrDone) return;
  var slot = scrSlots[idx];
  if (!slot || slot.locked) return;
  scrTiles[slot.tileIdx].used = false;
  scrSlots[idx] = null;
  renderScrambleTiles();
  renderScrambleAnswer();
}

function scrMaybeCheck() {
  var filled = scrSlots.every(function (s) {
    return !!s;
  });
  if (!filled) return;
  var attempt = scrSlots
    .map(function (s) {
      return s.char;
    })
    .join("");
  if (attempt.toLowerCase() === scrTarget.toLowerCase()) {
    endScramble(true);
    return;
  }
  scrWrong++;
  setText("scramble-wrong", scrWrong);
  var box = $("scramble-answer");
  if (box) box.classList.add("is-shaking");
  setTimeout(function () {
    if (scrWrong >= SCR_MAX_WRONG) {
      endScramble(false);
      return;
    }
    scrSlots.forEach(function (s, i) {
      if (s && !s.locked) {
        scrTiles[s.tileIdx].used = false;
        scrSlots[i] = null;
      }
    });
    renderScrambleTiles();
    renderScrambleAnswer();
  }, 450);
}

function endScramble(win) {
  scrDone = true;
  scrSlots.forEach(function (s) {
    if (s) s.locked = true;
  });
  renderScrambleAnswer();
  var tilesBox = $("scramble-tiles");
  if (tilesBox) {
    tilesBox.querySelectorAll("button").forEach(function (b) {
      b.disabled = true;
    });
  }
  var hintBtn = $("scramble-hint-btn");
  if (hintBtn) hintBtn.disabled = true;

  if (!win) {
    scrambleLosses++;
    setText("scramble-losses", scrambleLosses);
  } else {
    scrambleWins++;
    setText("scramble-wins", scrambleWins);
  }

  var w = scrWord;
  var rt = $("scramble-result-text");
  if (rt) {
    rt.textContent = win ? "🎉 Correct!" : "💀 Game over";
    rt.className = "hangman-result-text " + (win ? "win" : "lose");
  }
  var ansEl = $("scramble-answer-reveal");
  if (ansEl) {
    ansEl.innerHTML =
      '<span class="hm-answer-label">Word:</span> <strong>' +
      escapeHtml(w.word) +
      "</strong>";
  }
  var ex = $("scramble-example");
  if (ex) {
    ex.textContent = w.example || "";
    ex.hidden = !w.example;
  }
  var linkDetails = $("scramble-link-details");
  if (linkDetails) linkDetails.href = vocabDetailsUrl(w.word);
  var linkExamples = $("scramble-link-examples");
  if (linkExamples) linkExamples.href = vocabExamplesUrl(w.word);
  setHidden("scramble-result", false);
  speak(w.word);
}

on("scramble-reshuffle", "click", function () {
  if (scrDone) return;
  var unused = [];
  scrTiles.forEach(function (t, i) {
    if (!t.used) unused.push(i);
  });
  var chars = unused.map(function (i) {
    return scrTiles[i].char;
  });
  var shuffled = shuffle(chars);
  unused.forEach(function (tileIdx, k) {
    scrTiles[tileIdx].char = shuffled[k];
  });
  renderScrambleTiles();
});

on("scramble-hint-btn", "click", function () {
  if (scrDone) return;
  var i = scrHints;
  if (i >= scrTarget.length) return;
  var wantChar = scrTarget[i];
  var existing = scrSlots[i];
  if (existing && existing.char.toLowerCase() === wantChar.toLowerCase()) {
    existing.locked = true;
  } else {
    if (existing) {
      scrTiles[existing.tileIdx].used = false;
      scrSlots[i] = null;
    }
    var tileIdx = scrTiles.findIndex(function (t) {
      return !t.used && t.char.toLowerCase() === wantChar.toLowerCase();
    });
    if (tileIdx === -1) return;
    scrTiles[tileIdx].used = true;
    scrSlots[i] = { char: scrTiles[tileIdx].char, tileIdx: tileIdx, locked: true };
  }
  scrHints++;
  if (scrHints >= scrTarget.length) {
    var hintBtn = $("scramble-hint-btn");
    if (hintBtn) hintBtn.disabled = true;
  }
  renderScrambleTiles();
  renderScrambleAnswer();
  scrMaybeCheck();
});

on("scramble-start-btn", "click", function () {
  startScramble(currentLevel);
});
on("scramble-back", "click", showScrambleSetup);
on("scramble-change", "click", showScrambleSetup);
on("scramble-next", "click", newScrambleWord);
on("scramble-audio", "click", function () {
  if (scrWord) speak(scrWord.word);
});

document.addEventListener("keydown", function (e) {
  var g = $("scramble-game");
  if (!g || g.hidden || scrDone) return;
  if (e.ctrlKey || e.metaKey || e.altKey) return;
  var k = e.key;
  if (k === "Backspace") {
    for (var i = scrSlots.length - 1; i >= 0; i--) {
      if (scrSlots[i] && !scrSlots[i].locked) {
        scrClearSlot(i);
        break;
      }
    }
    e.preventDefault();
    return;
  }
  if (k && k.length === 1) {
    var idx = scrTiles.findIndex(function (t) {
      return !t.used && t.char.toLowerCase() === k.toLowerCase();
    });
    if (idx !== -1) {
      e.preventDefault();
      scrPlaceTile(idx);
    }
  }
});

onLevelChange(resetScramble);
