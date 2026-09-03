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

/* Top Words — Boss Rush game page logic. Requires shared.js.
 * Same proven "word -> pick the correct definition" engine as Speed Round
 * and Survival Streak, wrapped in an RPG battle presentation: three bosses
 * in a row, each with an HP bar that drops on a correct answer and a
 * floating damage number; a wrong answer costs one of three hearts instead
 * of ending the run immediately, and lets the boss "hit back" with a brief
 * shake. This is deliberately the most visually reactive game in the app --
 * the point is a battle feel that the plain multiple-choice games don't
 * attempt.
 */
"use strict";

var BR_BOSS_COUNT = 3;
var BR_BOSS_HP = 5;
var BR_START_HEARTS = 3;
var BR_BOSS_EMOJI = ["👹", "🐺", "🐉"];

var brActive = false;
var brLevel = null;
var brBossIndex = 0;
var brBossHp = 0;
var brHearts = 0;
var brDone = false;
var brCleared = false;
var brAnswered = false;
var brQuestion = null;
var brAskedKeys = {}; // dedupes questions within a single run

function brBestKey(level) {
  return "udsp_bossrush_best_" + currentLang + "_" + level + "_v1";
}
function brLoadBest(level) {
  try {
    return JSON.parse(localStorage.getItem(brBestKey(level))) || null;
  } catch (e) {
    return null;
  }
}
function brSaveBest(level, rec) {
  try {
    localStorage.setItem(brBestKey(level), JSON.stringify(rec));
  } catch (e) {
    /* ignore storage errors (private mode) */
  }
}
// Higher is better: a full clear always beats a loss, and within each
// outcome more hearts/bosses remaining wins -- one comparable integer, same
// spirit as memory.js's structured {time, moves} best record.
function brScoreOf(rec) {
  if (!rec) return -1;
  return (rec.cleared ? 1000 : 0) + (rec.bossesDefeated || 0) * 10 + (rec.hearts || 0);
}
function brEligible(w) {
  return !!(w && w.word && w.definition);
}

function refreshBossRushStart() {
  var pool = (WORD_SETS[currentLevel] || []).filter(brEligible);
  var ok = pool.length >= 4;
  setText("bossrush-start-level", levelLabel(currentLevel));
  setText(
    "bossrush-start-count",
    ok ? pool.length + (pool.length === 1 ? " word" : " words") + " available" : ""
  );
  setHidden("bossrush-start-warning", ok);
  if (!ok) {
    setText(
      "bossrush-start-warning",
      "Not enough words at this level (needs 4+) — pick another above."
    );
  }
  var btn = $("bossrush-start-btn");
  if (btn) btn.disabled = !ok;
  var best = brLoadBest(currentLevel);
  setText(
    "bossrush-start-best",
    best ? "🏆 Best: " + (best.cleared ? "Cleared, " : "") + best.bossesDefeated + " / " + BR_BOSS_COUNT + " bosses" : ""
  );
}

function showBossRushSetup() {
  brActive = false;
  setPlayHeader(false);
  refreshBossRushStart();
  setHidden("bossrush-game", true);
  setHidden("bossrush-setup", false);
}
function resetBossRush() {
  brActive = false;
  showBossRushSetup();
}
function enterBossRush() {
  if (!brActive) showBossRushSetup();
}

function brRenderHearts() {
  var s = "";
  for (var i = 0; i < BR_START_HEARTS; i++) s += i < brHearts ? "❤️" : "🖤";
  setText("bossrush-hearts", s);
}

function brRenderHp() {
  var fill = $("bossrush-hp-fill");
  if (fill) fill.style.width = Math.max(0, (brBossHp / BR_BOSS_HP) * 100) + "%";
  setText("bossrush-hp-text", Math.max(0, brBossHp) + " / " + BR_BOSS_HP);
}

function brRenderBossAvatar() {
  var el = $("bossrush-boss-avatar");
  if (el) el.textContent = BR_BOSS_EMOJI[brBossIndex % BR_BOSS_EMOJI.length];
  setText("bossrush-boss-counter", "Boss " + (brBossIndex + 1) + " / " + BR_BOSS_COUNT);
}

function startBossRush(level) {
  brLevel = level;
  brActive = true;
  brDone = false;
  brCleared = false;
  brBossIndex = 0;
  brBossHp = BR_BOSS_HP;
  brHearts = BR_START_HEARTS;
  brAskedKeys = {};
  setPlayHeader(true);
  setHidden("bossrush-setup", true);
  setHidden("bossrush-game", false);
  setHidden("bossrush-result", true);
  setText("bossrush-level-badge", levelLabel(level));
  brRenderHearts();
  brRenderHp();
  brRenderBossAvatar();
  brNextQuestion();
}

function brBuildQuestion(level) {
  var pool = (WORD_SETS[level] || []).filter(brEligible);
  if (pool.length < 4) return null;
  var fresh = pool.filter(function (w) {
    return !brAskedKeys[wordKey(w)];
  });
  var usable = fresh.length >= 4 ? fresh : pool; // pool exhausted -- allow repeats rather than stall the run
  var idx = Math.floor(Math.random() * usable.length);
  var correct = usable[idx];
  brAskedKeys[wordKey(correct)] = true;
  var distractors = shuffle(
    pool.filter(function (w) {
      return w !== correct;
    })
  ).slice(0, 3);
  var options = shuffle([correct].concat(distractors));
  return {
    word: correct.word,
    entry: correct,
    pos: correct.pos,
    answer: correct.definition,
    options: options.map(function (o) {
      return o.definition;
    }),
  };
}

function brNextQuestion() {
  if (brDone) return;
  var q = brBuildQuestion(brLevel);
  if (!q) {
    endBossRush(false);
    return;
  }
  brQuestion = q;
  brAnswered = false;
  setText("bossrush-word", q.word);
  setText("bossrush-word-pos", q.pos || "");
  var box = $("bossrush-options");
  if (box) {
    box.innerHTML = "";
    q.options.forEach(function (opt) {
      var b = document.createElement("button");
      b.type = "button";
      b.className = "option";
      b.textContent = opt;
      b.addEventListener("click", function () {
        brAnswer(opt, b);
      });
      box.appendChild(b);
    });
  }
}

// Floating "-1" damage number: appended next to the boss HP bar, animated
// via CSS, then removed on its own animationend so nothing accumulates.
function brSpawnDamageNumber(text, kind) {
  var host = $("bossrush-arena");
  if (!host) return;
  var el = document.createElement("span");
  el.className = "br-dmg br-dmg-" + kind;
  el.textContent = text;
  el.addEventListener("animationend", function () {
    el.remove();
  });
  host.appendChild(el);
}

function brAnswer(picked, btnEl) {
  if (brAnswered || brDone) return;
  brAnswered = true;
  var box = $("bossrush-options");
  var correct = picked === brQuestion.answer;
  answeredWord(brQuestion.entry, correct);
  if (box) {
    box.querySelectorAll(".option").forEach(function (b) {
      b.disabled = true;
      if (b.textContent === brQuestion.answer) b.classList.add("correct");
      else if (b === btnEl) b.classList.add("wrong");
    });
  }

  if (correct) {
    brBossHp--;
    brRenderHp();
    brSpawnDamageNumber("-1", "hit");
    var avatar = $("bossrush-boss-avatar");
    if (avatar) {
      avatar.classList.remove("is-hit");
      void avatar.offsetWidth; // restart the animation on back-to-back hits
      avatar.classList.add("is-hit");
    }
    if (brBossHp <= 0) {
      brDefeatBoss();
      return;
    }
  } else {
    brHearts--;
    brRenderHearts();
    brSpawnDamageNumber("MISS", "miss");
    var card = $("bossrush-card");
    if (card) {
      card.classList.remove("is-shaking");
      void card.offsetWidth;
      card.classList.add("is-shaking");
    }
    if (brHearts <= 0) {
      setTimeout(function () {
        endBossRush(false);
      }, 700);
      return;
    }
  }

  setTimeout(function () {
    if (!brDone) brNextQuestion();
  }, 550);
}

function brDefeatBoss() {
  var avatar = $("bossrush-boss-avatar");
  if (avatar) avatar.classList.add("is-defeated");
  setText("bossrush-boss-counter", "💥 Defeated!");
  setTimeout(function () {
    brBossIndex++;
    if (brBossIndex >= BR_BOSS_COUNT) {
      endBossRush(true);
      return;
    }
    brBossHp = BR_BOSS_HP;
    if (avatar) avatar.classList.remove("is-defeated", "is-hit");
    brRenderHp();
    brRenderBossAvatar();
    brNextQuestion();
  }, 750);
}

document.addEventListener("keydown", function (e) {
  var g = $("bossrush-game");
  if (!g || g.hidden || brDone) return;
  if (e.ctrlKey || e.metaKey || e.altKey) return;
  var map = { 1: 0, 2: 1, 3: 2, 4: 3 };
  var k = e.key;
  if (k in map) {
    var idx = map[k];
    var wrap = $("bossrush-options");
    var btns = wrap ? wrap.querySelectorAll(".option") : [];
    if (btns[idx] && !btns[idx].disabled) {
      e.preventDefault();
      btns[idx].click();
    }
  }
});

function endBossRush(cleared) {
  brDone = true;
  brCleared = cleared;
  var record = { bossesDefeated: brBossIndex, hearts: Math.max(0, brHearts), cleared: cleared };
  var best = brLoadBest(brLevel);
  var isNewBest = brScoreOf(record) > brScoreOf(best);
  if (isNewBest) brSaveBest(brLevel, record);

  var resultEl = $("bossrush-final-result");
  if (resultEl) {
    resultEl.classList.toggle("win", cleared);
    resultEl.classList.toggle("lose", !cleared);
  }
  setText(
    "bossrush-final-result",
    cleared ? "🏆 Victory! All bosses defeated" : "💀 Defeated at boss " + (brBossIndex + 1) + " / " + BR_BOSS_COUNT
  );
  setText(
    "bossrush-final-detail",
    record.bossesDefeated + " / " + BR_BOSS_COUNT + " bosses · " + record.hearts + " / " + BR_START_HEARTS + " hearts left"
  );
  var bestEl = $("bossrush-final-best");
  if (bestEl) {
    bestEl.textContent = isNewBest
      ? "🏆 New best!"
      : best
        ? "Best: " + (best.cleared ? "Cleared, " : "") + best.bossesDefeated + " / " + BR_BOSS_COUNT + " bosses"
        : "";
  }
  setHidden("bossrush-result", false);
}

on("bossrush-start-btn", "click", function () {
  startBossRush(currentLevel);
});
on("bossrush-audio", "click", function () {
  if (brQuestion) speak(brQuestion.word);
});
on("bossrush-back", "click", showBossRushSetup);
on("bossrush-change", "click", showBossRushSetup);
on("bossrush-restart", "click", function () {
  startBossRush(brLevel);
});

onLevelChange(resetBossRush);
