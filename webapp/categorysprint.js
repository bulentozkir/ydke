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

/* Top Words — Category Sprint active-recall game. Requires shared.js. */
"use strict";

var CS_DURATION = 60;
var CS_MIN_WORDS = 3;
var categorySprintActive = false;
var csLevel = null;
var csCategory = null;
var csEntries = [];
var csAnswers = {};
var csFound = {};
var csTimeLeft = 0;
var csTimerInterval = null;
var csScore = 0;
var csDone = false;

function csBestKey(level, category) {
  return (
    "udsp_categorysprint_best_" +
    currentLang +
    "_" +
    level +
    "_" +
    String(category || "mix").toLowerCase().replace(/\s+/g, "-") +
    "_v1"
  );
}
function csLoadBest(level, category) {
  var value = parseInt(localStorage.getItem(csBestKey(level, category)), 10);
  return isNaN(value) ? 0 : value;
}
function csSaveBest(level, category, score) {
  try {
    localStorage.setItem(csBestKey(level, category), String(score));
  } catch (e) {
    /* ignore storage errors (private mode) */
  }
}

function csStripArticle(word) {
  var patterns = {
    de: /^(der|die|das)\s+/i,
    fr: /^(?:(le|la|les)\s+|l['’])/i,
    it: /^(?:(il|lo|la|i|gli|le)\s+|l['’])/i,
    es: /^(el|la|los|las)\s+/i,
    pt: /^(o|a|os|as)\s+/i,
    nl: /^(de|het)\s+/i,
  };
  var pattern = patterns[currentLang];
  return pattern ? String(word || "").replace(pattern, "").trim() : String(word || "").trim();
}

function csNormalize(text) {
  return foldText(text)
    .replace(/[’']/g, "'")
    .replace(/\s+/g, " ");
}

function csEligible(entry) {
  return !!(
    entry &&
    entry.word &&
    entry.definition &&
    entry.category &&
    entry.category !== "General"
  );
}

function csGroups(level) {
  var groups = {};
  rawWordsForLevel(level)
    .filter(csEligible)
    .forEach(function (entry) {
      if (!groups[entry.category]) groups[entry.category] = [];
      groups[entry.category].push(entry);
    });
  return groups;
}

function csAvailable(level) {
  var groups = csGroups(level);
  return Object.keys(groups)
    .filter(function (category) {
      return groups[category].length >= CS_MIN_WORDS;
    })
    .sort(function (a, b) {
      return groups[b].length - groups[a].length || a.localeCompare(b);
    });
}

function csSelection(level) {
  var groups = csGroups(level);
  if (currentCategory !== "Mix") {
    return groups[currentCategory] && groups[currentCategory].length >= CS_MIN_WORDS
      ? { category: currentCategory, entries: groups[currentCategory] }
      : null;
  }
  var categories = csAvailable(level);
  if (!categories.length) return null;
  var category = categories[Math.floor(Math.random() * categories.length)];
  return { category: category, entries: groups[category] };
}

function refreshCategorySprintStart() {
  var groups = csGroups(currentLevel);
  var categories = csAvailable(currentLevel);
  var selectedCount = currentCategory !== "Mix" && groups[currentCategory]
    ? groups[currentCategory].length
    : 0;
  var ok = currentCategory === "Mix"
    ? categories.length > 0
    : selectedCount >= CS_MIN_WORDS;

  setText("categorysprint-start-level", levelLabel(currentLevel));
  setText(
    "categorysprint-start-count",
    currentCategory === "Mix"
      ? categories.length + " playable topic" + (categories.length === 1 ? "" : "s")
      : selectedCount + " words in " + currentCategory
  );
  setHidden("categorysprint-start-warning", ok);
  if (!ok) {
    setText(
      "categorysprint-start-warning",
      currentCategory === "Mix"
        ? "No topic has 3 words at this level — pick another level above."
        : "This topic needs at least 3 words — choose All or another category."
    );
  }
  var button = $("categorysprint-start-btn");
  if (button) button.disabled = !ok;
}

function showCategorySprintSetup() {
  categorySprintActive = false;
  setPlayHeader(false);
  stopCategorySprintTimer();
  refreshCategorySprintStart();
  setHidden("categorysprint-game", true);
  setHidden("categorysprint-setup", false);
}

function resetCategorySprint() {
  categorySprintActive = false;
  stopCategorySprintTimer();
  showCategorySprintSetup();
}

function startCategorySprint(level) {
  var selected = csSelection(level);
  if (!selected) {
    refreshCategorySprintStart();
    return;
  }

  csLevel = level;
  csCategory = selected.category;
  csEntries = selected.entries.slice();
  csAnswers = {};
  csFound = {};
  csEntries.forEach(function (entry) {
    var full = csNormalize(entry.word);
    var bare = csNormalize(csStripArticle(entry.word));
    csAnswers[full] = entry;
    csAnswers[bare] = entry;
  });

  categorySprintActive = true;
  csDone = false;
  csScore = 0;
  csTimeLeft = CS_DURATION;
  setPlayHeader(true);
  setHidden("categorysprint-setup", true);
  setHidden("categorysprint-game", false);
  setHidden("categorysprint-result", true);
  setText("categorysprint-level-badge", levelLabel(level));
  setText("categorysprint-category", csCategory);
  setText("categorysprint-target", csEntries.length + " possible words");
  setText("categorysprint-score", "0");
  setText("categorysprint-found", "");
  setText("categorysprint-feedback", "");
  renderCategorySprintTimer();

  var input = $("categorysprint-input");
  if (input) {
    input.value = "";
    input.disabled = false;
    input.focus();
  }
  var hintButton = $("categorysprint-hint-btn");
  if (hintButton) hintButton.disabled = false;

  startCategorySprintTimer();
}

function startCategorySprintTimer() {
  stopCategorySprintTimer();
  csTimerInterval = setInterval(function () {
    csTimeLeft--;
    if (csTimeLeft <= 0) {
      csTimeLeft = 0;
      renderCategorySprintTimer();
      endCategorySprint();
      return;
    }
    renderCategorySprintTimer();
  }, 1000);
}
function stopCategorySprintTimer() {
  if (csTimerInterval) {
    clearInterval(csTimerInterval);
    csTimerInterval = null;
  }
}
function renderCategorySprintTimer() {
  var timer = $("categorysprint-timer");
  if (!timer) return;
  timer.textContent = csTimeLeft + "s";
  timer.classList.toggle("is-low", csTimeLeft <= 10);
}

function csSubmit() {
  if (csDone) return;
  var input = $("categorysprint-input");
  if (!input || !input.value.trim()) return;

  var key = csNormalize(input.value);
  var entry = csAnswers[key];
  var feedback = $("categorysprint-feedback");
  input.value = "";
  input.focus();

  if (!entry) {
    if (feedback) {
      feedback.textContent = "Not in this topic · Bu konuda değil";
      feedback.className = "feedback no";
    }
    return;
  }

  var entryKey = wordKey(entry);
  if (csFound[entryKey]) {
    if (feedback) {
      feedback.textContent = "Already found · Zaten bulundu";
      feedback.className = "feedback";
    }
    return;
  }

  csFound[entryKey] = true;
  csScore++;
  answeredWord(entry, true);
  setText("categorysprint-score", String(csScore));
  renderCategorySprintFound();
  if (feedback) {
    feedback.textContent = "✓ " + entry.word;
    feedback.className = "feedback ok";
  }

  if (csScore >= csEntries.length) endCategorySprint();
}

function renderCategorySprintFound() {
  var found = $("categorysprint-found");
  if (!found) return;
  var entries = csEntries.filter(function (entry) {
    return csFound[wordKey(entry)];
  });
  found.innerHTML = entries
    .map(function (entry) {
      return '<span class="cs-found-word">' + escapeHtml(entry.word) + "</span>";
    })
    .join("");
}

function csHint() {
  if (csDone) return;
  var remaining = csEntries.filter(function (entry) {
    return !csFound[wordKey(entry)];
  });
  if (!remaining.length) return;
  var entry = remaining[Math.floor(Math.random() * remaining.length)];
  var bare = csStripArticle(entry.word);
  var text = bare.slice(0, 1).toUpperCase() + "… · " + bare.length + " letters";
  showPopover('<p class="example">' + escapeHtml(text) + "</p>");
}

function endCategorySprint() {
  if (csDone) return;
  csDone = true;
  stopCategorySprintTimer();
  var input = $("categorysprint-input");
  if (input) input.disabled = true;
  var best = csLoadBest(csLevel, csCategory);
  var isNewBest = csScore > best;
  if (isNewBest) csSaveBest(csLevel, csCategory, csScore);
  setText("categorysprint-final-score", csScore + " / " + csEntries.length);
  setText("categorysprint-final-category", csCategory);
  var bestElement = $("categorysprint-final-best");
  if (bestElement) {
    bestElement.textContent = isNewBest ? "🏆 New best!" : "Best: " + Math.max(best, csScore);
  }
  setHidden("categorysprint-result", false);
}

on("categorysprint-start-btn", "click", function () {
  startCategorySprint(currentLevel);
});
on("categorysprint-submit", "click", csSubmit);
on("categorysprint-hint-btn", "click", csHint);
on("categorysprint-back", "click", showCategorySprintSetup);
on("categorysprint-change", "click", showCategorySprintSetup);
on("categorysprint-restart", "click", function () {
  startCategorySprint(csLevel);
});

document.addEventListener("keydown", function (event) {
  var input = $("categorysprint-input");
  if (event.key === "Enter" && input && document.activeElement === input) {
    event.preventDefault();
    csSubmit();
  }
});

document.addEventListener("visibilitychange", function () {
  if (!categorySprintActive || csDone) return;
  if (document.visibilityState === "hidden") stopCategorySprintTimer();
  else startCategorySprintTimer();
});

onLevelChange(resetCategorySprint);
