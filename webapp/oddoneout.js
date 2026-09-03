/* Top Words — Odd One Out game page logic. Requires shared.js. */
"use strict";

var OOO_ROUNDS = 15;
var oooActive = false;
var oooLevel = null;
var oooRound = 0;
var oooScore = 0;
var oooDone = false;
var oooAnswered = false;
var oooCurrent = null; // { options: [w], intruder: w, theme: "Medicine" }
var oooHintUsed = false;

function oooBestKey(level) {
  return "udsp_oddoneout_best_" + currentLang + "_" + level + "_v1";
}
function oooLoadBest(level) {
  var v = parseInt(localStorage.getItem(oooBestKey(level)), 10);
  return isNaN(v) ? 0 : v;
}
function oooSaveBest(level, score) {
  try {
    localStorage.setItem(oooBestKey(level), String(score));
  } catch (e) {
    /* ignore storage errors (private mode) */
  }
}

// "General" is excluded on purpose. It is the catch-all domain holding ~82% of
// every word list, so a round built from it would ask "which of these four is
// not general?" -- a question with no defensible answer. Only the specific
// domains (Medicine, Sports, Law...) make the puzzle solvable.
function oooEligible(w) {
  return !!(
    w &&
    w.word &&
    w.definition &&
    w.category &&
    w.category !== "General"
  );
}

// category -> [word]. Built from rawWordsForLevel() rather than WORD_SETS so
// the header's Category filter cannot empty the pool: this game is ABOUT
// mixing domains, and honouring a filter of "Medicine only" would leave
// nothing to contrast against.
function oooGroups(level) {
  var raw = rawWordsForLevel(level).filter(oooEligible);
  var groups = {};
  for (var i = 0; i < raw.length; i++) {
    var c = raw[i].category;
    if (!groups[c]) groups[c] = [];
    groups[c].push(raw[i]);
  }
  return groups;
}

// A round needs one domain with 3 spare words plus any second domain for the
// intruder, so report both halves of that requirement.
function oooPoolInfo(level) {
  var groups = oooGroups(level);
  var names = Object.keys(groups);
  var themes = names.filter(function (c) {
    return groups[c].length >= 3;
  });
  return { groups: groups, names: names, themes: themes };
}

function refreshOddOneOutStart() {
  var info = oooPoolInfo(currentLevel);
  var ok = info.themes.length >= 1 && info.names.length >= 2;
  setText("oddoneout-start-level", levelLabel(currentLevel));
  setText(
    "oddoneout-start-count",
    ok
      ? info.names.length + " topic" + (info.names.length === 1 ? "" : "s") + " available"
      : ""
  );
  setHidden("oddoneout-start-warning", ok);
  if (!ok) {
    setText(
      "oddoneout-start-warning",
      "Not enough topic variety at this level (needs 2+ topics, one with 3+ words) — pick another above."
    );
  }
  var btn = $("oddoneout-start-btn");
  if (btn) btn.disabled = !ok;
}

function showOddOneOutSetup() {
  oooActive = false;
  setPlayHeader(false);
  refreshOddOneOutStart();
  setHidden("oddoneout-game", true);
  setHidden("oddoneout-setup", false);
}

function resetOddOneOut() {
  oooActive = false;
  showOddOneOutSetup();
}

function enterOddOneOut() {
  if (!oooActive) showOddOneOutSetup();
}

function startOddOneOut(level) {
  oooLevel = level;
  oooActive = true;
  oooDone = false;
  setPlayHeader(true);
  setHidden("oddoneout-setup", true);
  setHidden("oddoneout-game", false);
  setHidden("oddoneout-result", true);
  setText("oddoneout-level-badge", levelLabel(level));
  oooRound = 0;
  oooScore = 0;
  setText("oddoneout-score", "0");
  oooNextRound();
}

function oooBuildRound(info) {
  if (!info.themes.length || info.names.length < 2) return null;
  // Weight the theme by size so a domain with 200 words is picked more often
  // than one with exactly 3 -- otherwise the same handful of tiny domains
  // would dominate every session.
  var weighted = [];
  for (var i = 0; i < info.themes.length; i++) {
    var name = info.themes[i];
    var weight = Math.min(info.groups[name].length, 40);
    for (var j = 0; j < weight; j++) weighted.push(name);
  }
  var theme = weighted[Math.floor(Math.random() * weighted.length)];
  var family = shuffle(info.groups[theme].slice()).slice(0, 3);
  if (family.length < 3) return null;

  var otherNames = info.names.filter(function (c) {
    return c !== theme;
  });
  if (!otherNames.length) return null;
  var otherName = otherNames[Math.floor(Math.random() * otherNames.length)];
  var others = info.groups[otherName];
  var intruder = others[Math.floor(Math.random() * others.length)];

  // A duplicate headword across two domains would make both answers defensible.
  for (var k = 0; k < family.length; k++) {
    if (family[k].word === intruder.word) return null;
  }

  return {
    options: shuffle(family.concat([intruder])),
    intruder: intruder,
    theme: theme,
    otherName: otherName,
  };
}

function oooNextRound() {
  if (oooDone) return;
  if (oooRound >= OOO_ROUNDS) {
    endOddOneOut();
    return;
  }
  var info = oooPoolInfo(oooLevel);
  var round = null;
  // Rejected rounds (duplicate headword across domains) are rare; a few
  // retries beat threading a failure state through the whole game.
  for (var attempt = 0; attempt < 12 && !round; attempt++) {
    round = oooBuildRound(info);
  }
  if (!round) {
    endOddOneOut();
    return;
  }
  oooRound++;
  oooCurrent = round;
  oooAnswered = false;
  oooHintUsed = false;
  setText("oddoneout-progress", "Round " + oooRound + " / " + OOO_ROUNDS);
  setHidden("oddoneout-hint", true);
  setText("oddoneout-hint", "");
  var hintBtn = $("oddoneout-hint-btn");
  if (hintBtn) hintBtn.disabled = false;
  setText("oddoneout-feedback", "");
  oooRenderOptions();
}

function oooRenderOptions() {
  var box = $("oddoneout-options");
  if (!box) return;
  box.innerHTML = "";
  oooCurrent.options.forEach(function (w, i) {
    var btn = document.createElement("button");
    btn.type = "button";
    btn.className = "option";
    btn.innerHTML =
      '<span class="key">' + (i + 1) + "</span><span>" + escapeHtml(w.word) + "</span>";
    btn.addEventListener("click", function () {
      oooAnswer(i);
    });
    box.appendChild(btn);
  });
}

function oooAnswer(index) {
  if (oooDone || oooAnswered || !oooCurrent) return;
  oooAnswered = true;
  var picked = oooCurrent.options[index];
  var correct = picked === oooCurrent.intruder;
  var buttons = $("oddoneout-options").querySelectorAll(".option");
  for (var i = 0; i < buttons.length; i++) {
    buttons[i].disabled = true;
    var w = oooCurrent.options[i];
    if (w === oooCurrent.intruder) buttons[i].classList.add("correct");
    else if (i === index) buttons[i].classList.add("wrong");
    // Showing every word's domain turns a wrong answer into a lesson rather
    // than just a red border.
    var tag = document.createElement("span");
    tag.className = "ooo-tag";
    tag.textContent = w.category;
    buttons[i].appendChild(tag);
  }
  if (correct) {
    oooScore++;
    setText("oddoneout-score", oooScore);
    setText(
      "oddoneout-feedback",
      "✅ Correct — the other three are " + oooCurrent.theme + "."
    );
  } else {
    setText(
      "oddoneout-feedback",
      "❌ “" +
        oooCurrent.intruder.word +
        "” was the odd one (" +
        oooCurrent.otherName +
        "); the rest are " +
        oooCurrent.theme +
        "."
    );
  }
  answeredWord(oooCurrent.intruder, correct);
  setTimeout(function () {
    if (!oooDone) oooNextRound();
  }, 1500);
}

// Hint: name the shared domain. That still leaves the learner to work out
// which word does not belong to it, so it is a nudge and not the answer.
function oooHint() {
  if (oooDone || oooAnswered || !oooCurrent || oooHintUsed) return;
  oooHintUsed = true;
  var text = "Three of these belong to: " + oooCurrent.theme;
  setText("oddoneout-hint", text);
  setHidden("oddoneout-hint", false);
  var btn = $("oddoneout-hint-btn");
  if (btn) btn.disabled = true;
  showPopover("<p class=\"example\">" + escapeHtml(text) + "</p>");
}

function endOddOneOut() {
  oooDone = true;
  var best = oooLoadBest(oooLevel);
  var isNewBest = oooScore > best;
  if (isNewBest) oooSaveBest(oooLevel, oooScore);
  setText("oddoneout-final-score", oooScore + " / " + OOO_ROUNDS);
  var bestEl = $("oddoneout-final-best");
  if (bestEl) {
    bestEl.textContent = isNewBest
      ? "🏆 New best!"
      : "Best: " + Math.max(best, oooScore) + " / " + OOO_ROUNDS;
  }
  setHidden("oddoneout-result", false);
}

on("oddoneout-hint-btn", "click", oooHint);
on("oddoneout-start-btn", "click", function () {
  startOddOneOut(currentLevel);
});
on("oddoneout-back", "click", showOddOneOutSetup);
on("oddoneout-change", "click", showOddOneOutSetup);
on("oddoneout-restart", "click", function () {
  startOddOneOut(oooLevel);
});

document.addEventListener("keydown", function (e) {
  var g = $("oddoneout-game");
  if (!g || g.hidden || oooDone) return;
  var n = parseInt(e.key, 10);
  if (n >= 1 && n <= 4) oooAnswer(n - 1);
});

onLevelChange(resetOddOneOut);
