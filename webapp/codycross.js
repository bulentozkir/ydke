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

/* Top Words — CodyCross game page logic. Requires shared.js.
 * A themed group of 5 clues drawn from the current Level + Category (the
 * existing Category picker doubles as CodyCross's "world" -- Sports, Food,
 * Science and so on are exactly that kind of theme already). One letter
 * from each solved word lands in a shared "Bonus Code" strip. Unlike the
 * real game, that code is not itself a meaningful hidden word -- there is
 * no curated data linking these five entries to one -- so it is presented
 * honestly as a fun collectible payoff for clearing the group, not a
 * second puzzle to solve.
 */
"use strict";

var CC_GROUP_SIZE = 5;
var CC_MIN_LEN = 3;
var CC_MAX_LEN = 9;
var CC_POINTS_PER_WORD = 10;

var ccActive = false;
var ccLevel = null;
var ccDone = false;
var ccWords = []; // [{ entry, bare, bonusIdx, input, solved }]
var ccActiveRow = 0;
var ccScore = 0;

function ccBestKey(level) {
  return "udsp_codycross_best_" + currentLang + "_" + level + "_v1";
}
function ccLoadBest(level) {
  var v = parseInt(localStorage.getItem(ccBestKey(level)), 10);
  return isNaN(v) ? 0 : v;
}
function ccSaveBest(level, score) {
  try {
    localStorage.setItem(ccBestKey(level), String(score));
  } catch (e) {
    /* ignore storage errors (private mode) */
  }
}

function ccStripArticle(word) {
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
function ccFold(ch) {
  var s = String(ch == null ? "" : ch);
  s = s.normalize ? s.normalize("NFD").replace(/[\u0300-\u036f]/g, "") : s;
  return s.toUpperCase();
}
function ccEligible(w) {
  if (!w || !w.word || !w.definition) return false;
  var bare = ccStripArticle(w.word);
  return (
    !!bare &&
    /^[A-Za-zÀ-ÖØ-öø-ÿ]+$/.test(bare) &&
    !/ß/i.test(bare) &&
    bare.length >= CC_MIN_LEN &&
    bare.length <= CC_MAX_LEN
  );
}
function ccPool(level) {
  return (WORD_SETS[level] || []).filter(ccEligible);
}

function refreshCodyCrossStart() {
  var pool = ccPool(currentLevel);
  var ok = pool.length >= CC_GROUP_SIZE;
  setText("codycross-start-level", levelLabel(currentLevel));
  setText(
    "codycross-start-count",
    ok ? pool.length + (pool.length === 1 ? " word" : " words") + " available" : ""
  );
  setHidden("codycross-start-warning", ok);
  if (!ok) {
    setText(
      "codycross-start-warning",
      "Not enough short single-word entries at this level+category (needs " +
        CC_GROUP_SIZE +
        "+) — pick another above."
    );
  }
  var btn = $("codycross-start-btn");
  if (btn) btn.disabled = !ok;
  var best = ccLoadBest(currentLevel);
  setText("codycross-start-best", best ? "🏆 Best: " + best + " pts" : "");
}

function showCodyCrossSetup() {
  ccActive = false;
  setPlayHeader(false);
  refreshCodyCrossStart();
  setHidden("codycross-game", true);
  setHidden("codycross-setup", false);
}
function resetCodyCross() {
  ccActive = false;
  showCodyCrossSetup();
}
function enterCodyCross() {
  if (!ccActive) showCodyCrossSetup();
}

function startCodyCross(level) {
  var pool = ccPool(level);
  if (pool.length < CC_GROUP_SIZE) {
    refreshCodyCrossStart();
    return;
  }
  var group = shuffle(pool.slice()).slice(0, CC_GROUP_SIZE);
  ccWords = group.map(function (entry, i) {
    var bare = ccFold(ccStripArticle(entry.word));
    return { entry: entry, bare: bare, bonusIdx: i % bare.length, input: "", solved: false };
  });

  ccLevel = level;
  ccActive = true;
  ccDone = false;
  ccScore = 0;
  ccActiveRow = 0;

  setPlayHeader(true);
  setHidden("codycross-setup", true);
  setHidden("codycross-game", false);
  setHidden("codycross-result", true);
  setText("codycross-level-badge", levelLabel(level));
  setText("codycross-score", "0");

  ccRenderRows();
  ccRenderKeyboard();
  ccRenderBonusCode();
}

function ccRenderRows() {
  var host = $("codycross-rows");
  if (!host) return;
  host.innerHTML = ccWords
    .map(function (w, idx) {
      var text = w.solved ? w.bare : w.input;
      var cells = [];
      for (var i = 0; i < w.bare.length; i++) {
        var ch = (text[i] || "").toUpperCase();
        var cls = "wg-cell";
        if (w.solved) cls += " wg-correct";
        else if (ch) cls += " wg-filled";
        if (i === w.bonusIdx) cls += " cc-bonus-cell";
        cells.push('<span class="' + cls + '">' + escapeHtml(ch) + "</span>");
      }
      var textId = "cc-clue-text-" + idx;
      return (
        '<li class="cc-clue-row' +
        (idx === ccActiveRow && !w.solved ? " is-active" : "") +
        (w.solved ? " is-solved" : "") +
        '" data-row="' +
        idx +
        '">' +
        '<p class="cc-clue-text-wrap">' +
        '<button type="button" class="clue-bulb" data-clue-for="' +
        textId +
        '" aria-label="Show clue">💡</button>' +
        '<span class="cc-clue-text clue-gated" id="' +
        textId +
        '" tabindex="0">' +
        escapeHtml(w.entry.definition || "") +
        "</span>" +
        (w.solved ? '<button type="button" class="cw-clue-audio" data-row="' + idx + '">🔊</button>' : "") +
        "</p>" +
        '<div class="wg-row cc-row-cells">' +
        cells.join("") +
        "</div>" +
        "</li>"
      );
    })
    .join("");
}

function ccRenderKeyboard() {
  var kb = $("codycross-keyboard");
  if (!kb) return;
  var rows = ["QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM"];
  var html = rows
    .map(function (row) {
      var keys = row
        .split("")
        .map(function (ch) {
          return '<button type="button" class="wg-key" data-key="' + ch + '">' + ch + "</button>";
        })
        .join("");
      return '<div class="wg-kb-row">' + keys + "</div>";
    })
    .join("");
  html +=
    '<div class="wg-kb-row">' +
    '<button type="button" class="wg-key wg-key-wide" data-key="ENTER" aria-label="Next clue">Next</button>' +
    '<button type="button" class="wg-key wg-key-wide" data-key="BACK" aria-label="Backspace">⌫</button>' +
    "</div>";
  kb.innerHTML = html;
}

function ccRenderBonusCode() {
  var host = $("codycross-code");
  if (!host) return;
  host.innerHTML = ccWords
    .map(function (w) {
      var letter = w.solved ? w.bare[w.bonusIdx] : "";
      return (
        '<span class="cc-code-letter' +
        (w.solved ? " is-revealed" : "") +
        '">' +
        (letter ? escapeHtml(letter) : "?") +
        "</span>"
      );
    })
    .join("");
}

function ccSelectRow(idx) {
  if (ccWords[idx] && ccWords[idx].solved) return;
  ccActiveRow = idx;
  ccRenderRows();
}

function ccNextUnsolvedRow() {
  for (var i = 0; i < ccWords.length; i++) {
    if (!ccWords[i].solved) return i;
  }
  return -1;
}

function ccPressKey(key) {
  if (ccDone) return;
  var w = ccWords[ccActiveRow];
  if (!w || w.solved) return;
  if (key === "ENTER") {
    var next = ccNextUnsolvedRow();
    if (next !== -1) ccSelectRow(next);
    return;
  }
  if (key === "BACK") {
    w.input = w.input.slice(0, -1);
    ccRenderRows();
    return;
  }
  if (w.input.length >= w.bare.length || !/^[A-ZÀ-ÖØ-öø-ÿ]$/i.test(key)) return;
  w.input += key.toUpperCase();
  ccRenderRows();

  if (w.input.length === w.bare.length) {
    if (ccFold(w.input) === w.bare) {
      w.solved = true;
      ccScore += CC_POINTS_PER_WORD;
      setText("codycross-score", String(ccScore));
      answeredWord(w.entry, true);
      speak(w.entry.word);
      ccRenderRows();
      ccRenderBonusCode();
      var solvedCount = ccWords.filter(function (x) {
        return x.solved;
      }).length;
      if (solvedCount === ccWords.length) {
        endCodyCross();
        return;
      }
      var nextRow = ccNextUnsolvedRow();
      if (nextRow !== -1) ccSelectRow(nextRow);
    } else {
      var row = document.querySelector('.cc-clue-row[data-row="' + ccActiveRow + '"] .cc-row-cells');
      if (row) {
        row.classList.remove("is-shaking");
        void row.offsetWidth;
        row.classList.add("is-shaking");
      }
      setTimeout(function () {
        w.input = "";
        ccRenderRows();
      }, 350);
    }
  }
}

function ccHint() {
  if (ccDone) return;
  var w = ccWords[ccActiveRow];
  if (!w || w.solved) return;
  if (w.input.length < w.bare.length) {
    w.input += w.bare[w.input.length];
    ccRenderRows();
    if (w.input.length === w.bare.length) ccPressKey("");
  }
}

function endCodyCross() {
  ccDone = true;
  var best = ccLoadBest(ccLevel);
  var isNewBest = ccScore > best;
  if (isNewBest) ccSaveBest(ccLevel, ccScore);
  setText("codycross-final-score", String(ccScore));
  setText(
    "codycross-final-code",
    ccWords
      .map(function (w) {
        return w.bare[w.bonusIdx];
      })
      .join("")
  );
  var bestEl = $("codycross-final-best");
  if (bestEl) {
    bestEl.textContent = isNewBest ? "🏆 New best!" : "Best: " + Math.max(best, ccScore) + " pts";
  }
  setHidden("codycross-result", false);
}

on("codycross-start-btn", "click", function () {
  startCodyCross(currentLevel);
});
on("codycross-hint-btn", "click", ccHint);
on("codycross-back", "click", showCodyCrossSetup);
on("codycross-change", "click", showCodyCrossSetup);
on("codycross-restart", "click", function () {
  startCodyCross(ccLevel);
});

document.addEventListener("click", function (e) {
  var key = e.target.closest && e.target.closest(".wg-key");
  if (key) {
    ccPressKey(key.getAttribute("data-key"));
    return;
  }
  var audioBtn = e.target.closest && e.target.closest(".cw-clue-audio");
  if (audioBtn) {
    var idx = parseInt(audioBtn.getAttribute("data-row"), 10);
    if (ccWords[idx]) speak(ccWords[idx].entry.word);
    return;
  }
  var row = e.target.closest && e.target.closest(".cc-clue-row");
  if (row && row.parentElement && row.parentElement.id === "codycross-rows") {
    ccSelectRow(parseInt(row.getAttribute("data-row"), 10));
  }
});

document.addEventListener("keydown", function (e) {
  var g = $("codycross-game");
  if (!g || g.hidden || ccDone) return;
  if (e.key === "Enter") {
    ccPressKey("ENTER");
    return;
  }
  if (e.key === "Backspace") {
    ccPressKey("BACK");
    return;
  }
  if (/^[a-zA-ZÀ-ÖØ-öø-ÿ]$/.test(e.key)) ccPressKey(e.key.toUpperCase());
});

onLevelChange(resetCodyCross);
