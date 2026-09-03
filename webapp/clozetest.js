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

/* Top Words — Cloze Test (fill-in-the-blank) page logic. Requires shared.js. */
"use strict";

var czActive = false;
var czWins = 0;
var czLosses = 0;
var czLevel = null;
var czItem = null; // { entry, base, sentence, translation, span, options }
var czDone = false;
var czPoolCache = {};

function czFold(s) {
  if (!s) return "";
  var t = String(s).replace(/\u00df/g, "ss");
  t = t.normalize ? t.normalize("NFD").replace(/[\u0300-\u036f]/g, "") : t;
  return t.toLowerCase();
}
function czCommonPrefixLen(a, b) {
  var n = Math.min(a.length, b.length);
  var i = 0;
  while (i < n && a[i] === b[i]) i++;
  return i;
}
function czStripArticle(word) {
  return (word || "").replace(/^(der|die|das)\s+/i, "").trim();
}
function czBaseWord(entry) {
  var w = czStripArticle(entry.word);
  w = w.replace(/^sich\s+/i, "").trim();
  return w;
}
// Locate the target word (or its inflected form) inside the example
// sentence. Returns { start, end } character offsets, or null if no
// usable single-token match was found (multi-word phrases are skipped).
function czFindSpan(sentence, baseWord) {
  if (!sentence || !baseWord || /\s/.test(baseWord)) return null;
  var target = czFold(baseWord);
  if (target.length < 2) return null;
  var re = /[A-Za-z\u00c0-\u00d6\u00d8-\u00f6\u00f8-\u00ff]+/g;
  var m;
  var best = null;
  var bestScore = 0;
  while ((m = re.exec(sentence))) {
    var tok = m[0];
    var tokFold = czFold(tok);
    if (tokFold === target) {
      return { start: m.index, end: m.index + tok.length };
    }
    if (target.length >= 5) {
      var cp = czCommonPrefixLen(tokFold, target);
      var need = Math.max(5, Math.ceil(target.length * 0.65));
      var lenDiff = Math.abs(tokFold.length - target.length);
      if (cp >= need && lenDiff <= 5) {
        var ratio = cp / target.length;
        if (ratio > bestScore) {
          bestScore = ratio;
          best = { start: m.index, end: m.index + tok.length };
        }
      }
    }
  }
  return bestScore >= 0.6 ? best : null;
}

function czBuildPool(level) {
  var key = currentLang + "|" + level;
  var cached = czPoolCache[key];
  if (cached) return cached;
  var set = WORD_SETS[level] || [];
  var pool = [];
  set.forEach(function (w) {
    if (!w || !w.word || !w.example || isPlaceholderExample(w.example)) return;
    var base = czBaseWord(w);
    if (!base || /\s/.test(base) || base.length < 2) return;
    var sepIdx = w.example.indexOf(" - ");
    var sentence = sepIdx === -1 ? w.example : w.example.slice(0, sepIdx);
    var translation =
      sepIdx === -1 ? "" : w.example.slice(sepIdx + 3).trim();
    sentence = sentence.trim();
    var span = czFindSpan(sentence, base);
    if (!span) return;
    pool.push({
      entry: w,
      base: base,
      sentence: sentence,
      translation: translation,
      span: span,
    });
  });
  czPoolCache[key] = pool;
  return pool;
}

function refreshClozeStart() {
  var pool = czBuildPool(currentLevel);
  var ok = pool.length >= 4;
  setText("cloze-start-level", levelLabel(currentLevel));
  setText(
    "cloze-start-count",
    ok ? pool.length + (pool.length === 1 ? " word" : " words") + " available" : ""
  );
  setHidden("cloze-start-warning", ok);
  if (!ok) {
    setText(
      "cloze-start-warning",
      "Not enough words at this level (needs 4+) — pick another above."
    );
  }
  var btn = $("cloze-start-btn");
  if (btn) btn.disabled = !ok;
}

function showClozeSetup() {
  czActive = false;
  setPlayHeader(false);
  refreshClozeStart();
  setHidden("cloze-game", true);
  setHidden("cloze-setup", false);
}

function resetCloze() {
  czActive = false;
  showClozeSetup();
}

function enterCloze() {
  if (!czActive) showClozeSetup();
}

function czPickItem(level) {
  var pool = czBuildPool(level);
  if (!pool.length) return null;
  return pool[Math.floor(Math.random() * pool.length)];
}

function czBuildOptions(item, level) {
  var seen = {};
  seen[czFold(item.base)] = true;
  var candidates = czBuildPool(level).filter(function (p) {
    var f = czFold(p.base);
    if (seen[f]) return false;
    seen[f] = true;
    return true;
  });
  var samePos = candidates.filter(function (p) {
    return p.entry.pos === item.entry.pos;
  });
  var source = samePos.length >= 3 ? samePos : candidates;
  var distractors = shuffle(source).slice(0, 3);
  if (distractors.length < 3) return null;
  return shuffle(
    [{ base: item.base, isCorrect: true }].concat(
      distractors.map(function (d) {
        return { base: d.base, isCorrect: false };
      })
    )
  );
}

function startCloze(level) {
  czLevel = level;
  czActive = true;
  setPlayHeader(true);
  setHidden("cloze-setup", true);
  setHidden("cloze-game", false);
  setText("cloze-level-badge", levelLabel(level));
  setText("cloze-wins", czWins);
  setText("cloze-losses", czLosses);
  newClozeItem();
}

function newClozeItem() {
  var item = czPickItem(czLevel);
  if (!item) {
    showClozeSetup();
    return;
  }
  var options = czBuildOptions(item, czLevel);
  if (!options) {
    showClozeSetup();
    return;
  }
  czItem = item;
  czItem.options = options;
  czDone = false;
  renderClozeItem();
}

function renderClozeItem() {
  var it = czItem;
  setText("cloze-pos", it.entry.pos || "");
  var before = it.sentence.slice(0, it.span.start);
  var after = it.sentence.slice(it.span.end);
  var sentEl = $("cloze-sentence");
  if (sentEl) {
    sentEl.innerHTML =
      escapeHtml(before) +
      '<span class="cloze-blank">_______</span>' +
      escapeHtml(after);
  }
  resetExample("cloze-hint-btn", "cloze-hint", it.entry.definition, true);
  var fb = $("cloze-feedback");
  if (fb) {
    fb.textContent = "";
    fb.className = "feedback";
  }
  setHidden("cloze-result", true);

  var letters = ["A", "B", "C", "D"];
  var wrap = $("cloze-options");
  if (!wrap) return;
  wrap.innerHTML = "";
  it.options.forEach(function (opt, idx) {
    var btn = document.createElement("button");
    btn.type = "button";
    btn.className = "option";
    btn.innerHTML =
      '<span class="key">' + letters[idx] + "</span><span>" +
      escapeHtml(opt.base) + "</span>";
    btn.addEventListener("click", function () {
      answerCloze(idx, btn);
    });
    wrap.appendChild(btn);
  });
}

function answerCloze(idx, btn) {
  if (czDone || !czItem) return;
  czDone = true;
  var chosen = czItem.options[idx];
  var isCorrect = !!chosen.isCorrect;

  var wrap = $("cloze-options");
  if (wrap) {
    wrap.querySelectorAll(".option").forEach(function (el, i) {
      el.disabled = true;
      if (czItem.options[i].isCorrect) el.classList.add("correct");
    });
  }
  if (!isCorrect) btn.classList.add("wrong");

  var fb = $("cloze-feedback");
  if (fb) {
    fb.textContent = isCorrect ? "Correct!" : "Not quite.";
    fb.className = "feedback " + (isCorrect ? "ok" : "no");
  }

  if (isCorrect) {
    czWins++;
    setText("cloze-wins", czWins);
  } else {
    czLosses++;
    setText("cloze-losses", czLosses);
  }

  stats.answered = (stats.answered || 0) + 1;
  if (isCorrect) stats.correct = (stats.correct || 0) + 1;
  lsSet(STATS_KEY, stats);
  // Objective per-word evidence -> feeds the SRS schedule and the mistake list
  // that Flashcards/Quiz can now be filtered down to.
  answeredWord(czItem.entry, isCorrect);
  touchStreak();

  showClozeResult(isCorrect);
}

function showClozeResult(isCorrect) {
  var it = czItem;
  var w = it.entry;
  resetExample(
    "cloze-example-btn",
    "cloze-full-sentence",
    it.sentence + (it.translation ? " - " + it.translation : ""),
    true
  );
  var linkDetails = $("cloze-link-details");
  if (linkDetails) linkDetails.href = vocabDetailsUrl(w.word);
  var linkExamples = $("cloze-link-examples");
  if (linkExamples) linkExamples.href = vocabExamplesUrl(w.word);
  setHidden("cloze-result", false);
  speak(w.word);
}

on("cloze-start-btn", "click", function () {
  startCloze(currentLevel);
});
on("cloze-back", "click", showClozeSetup);
on("cloze-change", "click", showClozeSetup);
on("cloze-next", "click", newClozeItem);
on("cloze-audio", "click", function () {
  if (czItem) speak(czItem.entry.word);
});
wireExample("cloze-hint-btn", "cloze-hint");
wireExample("cloze-example-btn", "cloze-full-sentence");
document.addEventListener("keydown", function (e) {
  var g = $("cloze-game");
  if (!g || g.hidden || !czItem || czDone) return;
  if (e.ctrlKey || e.metaKey || e.altKey) return;
  var map = { 1: 0, 2: 1, 3: 2, 4: 3, a: 0, b: 1, c: 2, d: 3 };
  var k = (e.key || "").toLowerCase();
  if (k in map) {
    var idx = map[k];
    var wrap = $("cloze-options");
    var btns = wrap ? wrap.querySelectorAll(".option") : [];
    if (btns[idx] && !btns[idx].disabled) {
      e.preventDefault();
      btns[idx].click();
    }
  }
});

onLevelChange(resetCloze);
