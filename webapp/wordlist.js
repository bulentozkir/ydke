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

/* Top Words — Word List page logic. Requires shared.js to be loaded first. */
"use strict";

var listEl = $("word-list");
var searchEl = $("list-search");
var listFilter = "all";

function wordMatchesFilter(w) {
  var k = wordKey(w);
  if (listFilter === "fav") return !!fav[k];
  if (listFilter === "known") return !!known[k];
  if (listFilter === "unknown") return !known[k];
  return true;
}

function wordListItemHtml(w) {
  var k = wordKey(w);
  return (
    "<li>" +
    '<div class="wl-head"><span class="wl-word">' +
    escapeHtml(w.word) +
    '</span><span class="wl-pos">' +
    escapeHtml(w.pos) +
    "</span>" +
    '<span class="wl-marks">' +
    '<button class="mark-btn mark-audio" data-act="audio" data-key="' +
    escapeHtml(k) +
    '" title="Listen">🔊</button>' +
    '<button class="mark-btn mark-known' +
    (known[k] ? " is-on" : "") +
    '" data-act="known" data-key="' +
    escapeHtml(k) +
    '" title="Mark as known">✓</button>' +
    '<button class="mark-btn mark-fav' +
    (fav[k] ? " is-on" : "") +
    '" data-act="fav" data-key="' +
    escapeHtml(k) +
    '" title="Favorite">★</button>' +
    "</span></div>" +
    '<div class="wl-def">' +
    escapeHtml(w.definition) +
    "</div>" +
    (w.example && !isPlaceholderExample(w.example)
      ? '<div class="wl-example">' + escapeHtml(w.example) + "</div>"
      : "") +
    '<div class="wl-link">' +
    vocabLinkHtml(w.word) +
    "</div>" +
    "</li>"
  );
}

// Guards chunked rendering below: bumped on every renderList() call so an
// in-progress chunk loop from a now-stale call (e.g. the user kept typing
// in the search box) can detect it's obsolete and stop appending, instead
// of two renders' chunks interleaving in the list.
var _wlRenderToken = 0;
var _wlInputFrame = 0;
// First chunk is small so *something* appears on screen almost instantly;
// later chunks are bigger since by then the user already sees a populated,
// scrollable list and small variations in per-chunk time are far less
// noticeable than the initial wait.
var WL_FIRST_CHUNK = 40;
var WL_CHUNK_SIZE = 100;
// Page size for the list. This used to be a hard cap (WL_RENDER_CAP = 15,000):
// anything past it was simply unreachable unless you already knew the word and
// searched for it. It is now a PAGE boundary with a "Show more" control, so the
// whole list stays reachable by browsing.
//
// Lowered from 15,000 at the same time: each item is a richly-nested <li> (3
// mark buttons + 2 dictionary links), so a 15,000-item page put ~120,000 nodes
// on screen and left the page janky to scroll on a phone -- measurably so, it
// stayed busy long enough that a headless browser could not settle on it.
// 3,000 still renders the common single-level views (e.g. English B2 = 2,905)
// in one page, so nothing regresses for the everyday case, while the huge
// aggregates ("Mix", TOEFL) now arrive in fast, responsive pages.
var WL_PAGE_SIZE = 3000;
// Matches of the current filter, and how many of them are on screen. Held at
// module scope so the "Show more" button can resume where the last page
// stopped without re-filtering.
var wlMatches = [];
var wlShown = 0;

function wlRemoveFooter() {
  var el = listEl && listEl.querySelector(".wl-more");
  if (el) el.parentNode.removeChild(el);
}

// Renders the next WL_PAGE_SIZE matches, in small chunks across animation
// frames, then re-adds the "Show more" control if anything is still left.
function wlRenderPage(myToken) {
  var start = wlShown;
  var end = Math.min(start + WL_PAGE_SIZE, wlMatches.length);
  var idx = start;
  wlRemoveFooter();
  setText("list-count", end + " / " + WORDS.length);

  // Render in small batches across animation frames instead of building
  // every <li> in one giant innerHTML write. Inserting many thousands of
  // richly-nested list items (each with 3 mark buttons + 2 dictionary
  // links) in a single DOM operation is what caused the page to visibly
  // hang/freeze for several seconds on load, and again on every keystroke
  // while searching. Chunking keeps every individual frame fast, so
  // content appears almost immediately and the page stays responsive/
  // scrollable while the rest streams in, regardless of list size.
  function renderChunk(size) {
    if (myToken !== _wlRenderToken) return; // superseded by a newer renderList() call
    var stop = Math.min(idx + size, end);
    var html = [];
    for (; idx < stop; idx++) {
      html.push(wordListItemHtml(wlMatches[idx]));
    }
    listEl.insertAdjacentHTML("beforeend", html.join(""));
    wlShown = idx;
    if (idx < end) {
      requestAnimationFrame(function () {
        renderChunk(WL_CHUNK_SIZE);
      });
      return;
    }
    var left = wlMatches.length - wlShown;
    if (left > 0) {
      listEl.insertAdjacentHTML(
        "beforeend",
        '<li class="wl-more"><button class="nav-btn" id="wl-more-btn" type="button">Show ' +
          Math.min(left, WL_PAGE_SIZE).toLocaleString() +
          " more</button><span>" +
          left.toLocaleString() +
          " of " +
          wlMatches.length.toLocaleString() +
          " matching words still to come — or search above to jump straight to one.</span></li>"
      );
    }
  }
  renderChunk(start === 0 ? WL_FIRST_CHUNK : WL_CHUNK_SIZE);
}

function renderList(filter) {
  if (!listEl) return;
  var myToken = ++_wlRenderToken;
  filter = (filter || "").trim().toLowerCase();

  // Filtering itself (no HTML/DOM work yet) is cheap even for tens of
  // thousands of words, so it's fine to do it eagerly, all at once.
  var matches = WORDS.filter(function (w) {
    if (!wordMatchesFilter(w)) return false;
    if (
      filter &&
      w.word.toLowerCase().indexOf(filter) === -1 &&
      w.definition.toLowerCase().indexOf(filter) === -1
    ) {
      return false;
    }
    return true;
  });

  wlMatches = matches;
  wlShown = 0;

  if (!matches.length) {
    setText("list-count", "0 / " + WORDS.length);
    listEl.innerHTML = '<li class="empty">No words match.</li>';
    return;
  }

  listEl.innerHTML = "";
  wlRenderPage(myToken);
}

on("list-search", "input", function () {
  // Cancel stale chunks immediately, but keep the current rows visible until
  // the next paint. Rapid input then produces one replacement instead of a
  // clear/rebuild flash for every key event.
  _wlRenderToken++;
  if (_wlInputFrame) cancelAnimationFrame(_wlInputFrame);
  _wlInputFrame = requestAnimationFrame(function () {
    _wlInputFrame = 0;
    renderList(searchEl.value);
  });
});
if (listEl) {
  listEl.addEventListener("click", function (e) {
    if (e.target.closest && e.target.closest("#wl-more-btn")) {
      // Same token: this continues the current filter's render, it doesn't
      // supersede it, so an in-flight chunk loop must not be cancelled.
      wlRenderPage(_wlRenderToken);
      return;
    }
    var btn = e.target.closest ? e.target.closest(".mark-btn") : null;
    if (!btn) return;
    var act = btn.getAttribute("data-act");
    var k = btn.getAttribute("data-key");
    if (act === "audio") {
      speak(k.split("|")[1]);
      return;
    }
    var store = act === "known" ? known : fav;
    var sk = act === "known" ? KNOWN_KEY : FAV_KEY;
    if (store[k]) delete store[k];
    else store[k] = 1;
    lsSet(sk, store);
    btn.classList.toggle("is-on", !!store[k]);
    if (listFilter !== "all") renderList(searchEl.value);
  });
}
document.querySelectorAll("#list-filters .chip-btn").forEach(function (b) {
  b.addEventListener("click", function () {
    listFilter = b.getAttribute("data-filter");
    document.querySelectorAll("#list-filters .chip-btn").forEach(function (x) {
      x.classList.toggle("is-on", x === b);
    });
    renderList(searchEl ? searchEl.value : "");
  });
});

onLevelChange(function () {
  if (searchEl) searchEl.value = "";
  renderList("");
});
