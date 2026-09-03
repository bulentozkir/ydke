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

/* Top Words — Sentence Scramble (word-order) game page logic. Requires shared.js. */
"use strict";

var SENT_MAX_WRONG = 3;
var sentscrActive = false;
var sentscrWins = 0;
var sentscrLosses = 0;
var sentscrLevel = null;
var sentscrWord = null;
var sentscrTokens = []; // correct order of word-tokens
var sentscrTranslation = "";
var sentscrTiles = []; // [{ text, used }]
var sentscrSlots = []; // [{ text, tileIdx, locked } | null]
var sentscrWrong = 0;
var sentscrDone = false;
var sentscrHintUsed = false;

function sentscrTokenize(example) {
  if (!example) return null;
  var sepIdx = example.indexOf(" - ");
  var sentence = sepIdx === -1 ? example : example.slice(0, sepIdx);
  var translation = sepIdx === -1 ? "" : example.slice(sepIdx + 3).trim();
  var tokens = sentence.trim().split(/\s+/).filter(Boolean);
  if (tokens.length < 3 || tokens.length > 12) return null;
  return { tokens: tokens, translation: translation };
}
function sentscrEligible(w) {
  return !!(
    w &&
    w.word &&
    !isPlaceholderExample(w.example) &&
    sentscrTokenize(w.example)
  );
}

function refreshSentScrStart() {
  var set = (WORD_SETS[currentLevel] || []).filter(sentscrEligible);
  var ok = set.length > 0;
  setText("sentscr-start-level", levelLabel(currentLevel));
  setText(
    "sentscr-start-count",
    ok
      ? set.length + (set.length === 1 ? " sentence" : " sentences") + " available"
      : ""
  );
  setHidden("sentscr-start-warning", ok);
  if (!ok) {
    setText(
      "sentscr-start-warning",
      "No usable example sentences at this level — pick another above."
    );
  }
  var btn = $("sentscr-start-btn");
  if (btn) btn.disabled = !ok;
}

function showSentScrSetup() {
  sentscrActive = false;
  setPlayHeader(false);
  refreshSentScrStart();
  setHidden("sentscr-game", true);
  setHidden("sentscr-setup", false);
}

function resetSentScr() {
  sentscrActive = false;
  showSentScrSetup();
}

function enterSentScr() {
  if (!sentscrActive) showSentScrSetup();
}

function startSentScr(level) {
  sentscrLevel = level;
  sentscrActive = true;
  setPlayHeader(true);
  setHidden("sentscr-setup", true);
  setHidden("sentscr-game", false);
  setText("sentscr-level-badge", levelLabel(level));
  setText("sentscr-max", SENT_MAX_WRONG);
  setText("sentscr-wins", sentscrWins);
  setText("sentscr-losses", sentscrLosses);
  newSentScrItem();
}

function sentscrPick(level) {
  var pool = (WORD_SETS[level] || []).filter(sentscrEligible);
  if (!pool.length) return null;
  return pool[Math.floor(Math.random() * pool.length)];
}

function newSentScrItem() {
  var w = sentscrPick(sentscrLevel);
  if (!w) {
    showSentScrSetup();
    return;
  }
  var parsed = sentscrTokenize(w.example);
  sentscrWord = w;
  sentscrTokens = parsed.tokens;
  sentscrTranslation = parsed.translation;
  sentscrWrong = 0;
  sentscrDone = false;
  sentscrHintUsed = false;

  var shuffled = shuffle(sentscrTokens);
  var attempts = 0;
  while (
    shuffled.join(" ") === sentscrTokens.join(" ") &&
    attempts < 8 &&
    sentscrTokens.length > 1
  ) {
    shuffled = shuffle(sentscrTokens);
    attempts++;
  }
  sentscrTiles = shuffled.map(function (t) {
    return { text: t, used: false };
  });
  sentscrSlots = sentscrTokens.map(function () {
    return null;
  });

  setText("sentscr-wrong", "0");
  setText("sentscr-pos", w.pos || "");
  setText("sentscr-clue", w.definition || "");
  setHidden("sentscr-result", true);
  setHidden("sentscr-hint", true);
  var hintBtn = $("sentscr-hint-btn");
  if (hintBtn) hintBtn.disabled = false;
  renderSentScrTiles();
  renderSentScrAnswer();
}

function renderSentScrAnswer() {
  var box = $("sentscr-answer");
  if (!box) return;
  box.classList.remove("is-shaking");
  var html = "";
  sentscrSlots.forEach(function (slot, i) {
    if (!slot) {
      html +=
        '<button type="button" class="sentscr-slot is-empty" data-idx="' + i + '"></button>';
    } else {
      html +=
        '<button type="button" class="sentscr-slot is-filled' +
        (slot.locked ? " is-locked" : "") +
        '" data-idx="' +
        i +
        '">' +
        escapeHtml(slot.text) +
        "</button>";
    }
  });
  box.innerHTML = html;
  box
    .querySelectorAll(".sentscr-slot.is-filled:not(.is-locked)")
    .forEach(function (btn) {
      btn.addEventListener("click", function () {
        sentscrClearSlot(parseInt(btn.getAttribute("data-idx"), 10));
      });
    });
}

function renderSentScrTiles() {
  var box = $("sentscr-tiles");
  if (!box) return;
  box.innerHTML = "";
  sentscrTiles.forEach(function (tile, i) {
    var b = document.createElement("button");
    b.type = "button";
    b.className = "sentscr-tile";
    b.textContent = tile.text;
    b.disabled = tile.used;
    b.addEventListener("click", function () {
      sentscrPlaceTile(i);
    });
    box.appendChild(b);
  });
}

function sentscrPlaceTile(tileIdx) {
  if (sentscrDone || sentscrTiles[tileIdx].used) return;
  var emptyIdx = sentscrSlots.findIndex(function (s) {
    return !s;
  });
  if (emptyIdx === -1) return;
  sentscrTiles[tileIdx].used = true;
  sentscrSlots[emptyIdx] = {
    text: sentscrTiles[tileIdx].text,
    tileIdx: tileIdx,
    locked: false,
  };
  renderSentScrTiles();
  renderSentScrAnswer();
  sentscrMaybeCheck();
}

function sentscrClearSlot(idx) {
  if (sentscrDone) return;
  var slot = sentscrSlots[idx];
  if (!slot || slot.locked) return;
  sentscrTiles[slot.tileIdx].used = false;
  sentscrSlots[idx] = null;
  renderSentScrTiles();
  renderSentScrAnswer();
}

function sentscrMaybeCheck() {
  var filled = sentscrSlots.every(function (s) {
    return !!s;
  });
  if (!filled) return;
  var isMatch = sentscrSlots.every(function (s, i) {
    return s.text === sentscrTokens[i];
  });
  if (isMatch) {
    endSentScr(true);
    return;
  }
  sentscrWrong++;
  setText("sentscr-wrong", sentscrWrong);
  var box = $("sentscr-answer");
  if (box) box.classList.add("is-shaking");
  setTimeout(function () {
    if (sentscrWrong >= SENT_MAX_WRONG) {
      endSentScr(false);
      return;
    }
    sentscrSlots.forEach(function (s, i) {
      if (s && !s.locked) {
        sentscrTiles[s.tileIdx].used = false;
        sentscrSlots[i] = null;
      }
    });
    renderSentScrTiles();
    renderSentScrAnswer();
  }, 450);
}

function endSentScr(win) {
  sentscrDone = true;
  renderSentScrAnswer();
  var tilesBox = $("sentscr-tiles");
  if (tilesBox) {
    tilesBox.querySelectorAll("button").forEach(function (b) {
      b.disabled = true;
    });
  }
  var hintBtn = $("sentscr-hint-btn");
  if (hintBtn) hintBtn.disabled = true;

  if (!win) {
    sentscrLosses++;
    setText("sentscr-losses", sentscrLosses);
  } else {
    sentscrWins++;
    setText("sentscr-wins", sentscrWins);
  }

  var w = sentscrWord;
  var rt = $("sentscr-result-text");
  if (rt) {
    rt.textContent = win ? "🎉 Correct!" : "💀 Game over";
    rt.className = "hangman-result-text " + (win ? "win" : "lose");
  }
  var ansEl = $("sentscr-answer-reveal");
  if (ansEl) {
    ansEl.innerHTML =
      '<span class="hm-answer-label">Sentence:</span> <strong>' +
      escapeHtml(sentscrTokens.join(" ")) +
      "</strong>";
  }
  var ex = $("sentscr-example");
  if (ex) {
    ex.textContent = sentscrTranslation || "";
    ex.hidden = !sentscrTranslation;
  }
  var linkDetails = $("sentscr-link-details");
  if (linkDetails) linkDetails.href = vocabDetailsUrl(w.word);
  var linkExamples = $("sentscr-link-examples");
  if (linkExamples) linkExamples.href = vocabExamplesUrl(w.word);
  setHidden("sentscr-result", false);
  speak(sentscrTokens.join(" "));
}

on("sentscr-reshuffle", "click", function () {
  if (sentscrDone) return;
  var unused = [];
  sentscrTiles.forEach(function (t, i) {
    if (!t.used) unused.push(i);
  });
  var texts = unused.map(function (i) {
    return sentscrTiles[i].text;
  });
  var shuffled = shuffle(texts);
  unused.forEach(function (tileIdx, k) {
    sentscrTiles[tileIdx].text = shuffled[k];
  });
  renderSentScrTiles();
});

on("sentscr-hint-btn", "click", function () {
  if (sentscrDone || sentscrHintUsed) return;
  sentscrHintUsed = true;
  var hasTranslation = !!sentscrTranslation;
  var text = hasTranslation ? "Translation: " + sentscrTranslation : "";
  setText("sentscr-hint", text);
  var btn = $("sentscr-hint-btn");
  if (btn) btn.disabled = true;
  if (hasTranslation) showPopover('<p class="example">' + escapeHtml(text) + "</p>");
});

on("sentscr-back", "click", showSentScrSetup);
on("sentscr-change", "click", showSentScrSetup);
on("sentscr-next", "click", newSentScrItem);
on("sentscr-audio", "click", function () {
  if (sentscrTokens.length) speak(sentscrTokens.join(" "));
});
on("sentscr-start-btn", "click", function () {
  startSentScr(currentLevel);
});

onLevelChange(resetSentScr);
