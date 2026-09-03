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

/* Top Words — mobile "More" sheet toggle, for pages that don't load
 * shared.js (about.html, help.html, games.html, listening.html). The 18
 * study-mode pages already get this same behaviour from shared.js. */
(function () {
  "use strict";
  var moreBtn = document.getElementById("more-btn");
  var sheet = document.getElementById("more-sheet");
  var closeBtn = document.getElementById("more-close");
  if (!moreBtn || !sheet) return;
  function openSheet() {
    sheet.hidden = false;
    document.body.classList.add("more-open");
  }
  function closeSheet() {
    sheet.hidden = true;
    document.body.classList.remove("more-open");
  }
  moreBtn.addEventListener("click", openSheet);
  if (closeBtn) closeBtn.addEventListener("click", closeSheet);
  sheet.addEventListener("click", function (e) {
    if (e.target === sheet) closeSheet();
  });
  document.addEventListener("keydown", function (e) {
    if (e.key === "Escape" && !sheet.hidden) closeSheet();
  });
})();

// Header profile icon "is-linked" dot -- mirrors the same tiny check in
// shared.js/home.js (these 3 scripts never load together, so it's a small
// deliberate duplication rather than a shared include; see shared.js's
// PROFILE_LINKED_KEY comment for the full rationale).
(function () {
  var link = document.getElementById("profile-icon-link");
  if (!link) return;
  try {
    if (localStorage.getItem("udsp_profile_linked_v1") === "1") {
      link.classList.add("is-linked");
    }
  } catch (e) {
    /* ignore storage errors (private mode) */
  }
})();

// "💡" info modal for the .seo-content prose -- an identical copy of
// wireSeoInfoModal() in shared.js (see the long comment there for why the
// prose is MOVED rather than cloned, and why it is collapsed at every width).
//
// OPT-IN here, unlike shared.js. shared.js only ever loads on study/game
// pages, where collapsing is always right, so it just takes the first
// .seo-content it finds. moresheet.js loads on BOTH kinds of page, so the
// markup has to say which is which: games.html and listening.html tag their
// prose `.seo-content.seo-collapsible`, while about/help/privacy/terms/home/
// profile/history do not -- there the long text IS the page, and privacy +
// terms additionally have to stay readable without interaction (footer legal
// links). Gating on a class rather than body[data-mode] means
// a new page opts in by saying so in its own markup.
(function () {
  "use strict";
  var section = document.querySelector(".seo-content.seo-collapsible");
  var actions = document.querySelector(".brand-actions");
  if (!section || !actions) return;
  var inner = section.querySelector(".seo-inner");
  if (!inner || actions.querySelector(".seo-info-btn")) return;

  var btn = document.createElement("button");
  btn.type = "button";
  btn.className = "about-btn seo-info-btn";
  btn.textContent = "💡";
  btn.setAttribute("aria-haspopup", "dialog");
  btn.setAttribute("aria-expanded", "false");
  btn.setAttribute("aria-label", "Bu sayfa hakkında · About this page");
  btn.setAttribute("data-tip", "Bu sayfa hakkında · About this page");
  actions.insertBefore(btn, actions.firstElementChild);
  section.classList.add("is-collapsed");

  var modal = document.createElement("div");
  modal.className = "seo-modal";
  modal.hidden = true;
  modal.innerHTML =
    '<div class="seo-modal-card" role="dialog" aria-modal="true" ' +
    'aria-label="Bu sayfa hakkında · About this page">' +
    '<div class="seo-modal-head">' +
    '<span class="seo-modal-title">💡 Bu sayfa hakkında · About this page</span>' +
    '<button type="button" class="seo-modal-close" aria-label="Kapat · Close">✕</button>' +
    "</div>" +
    '<div class="seo-modal-body"></div>' +
    "</div>";
  document.body.appendChild(modal);
  var modalBody = modal.querySelector(".seo-modal-body");

  function openSeoModal() {
    modalBody.appendChild(inner);
    modal.hidden = false;
    btn.setAttribute("aria-expanded", "true");
    modalBody.scrollTop = 0;
    modal.querySelector(".seo-modal-close").focus();
  }
  function closeSeoModal() {
    modal.hidden = true;
    section.appendChild(inner); // the one real copy goes back where it was
    btn.setAttribute("aria-expanded", "false");
    btn.focus();
  }
  btn.addEventListener("click", openSeoModal);
  modal.addEventListener("click", function (e) {
    if (e.target === modal || (e.target.closest && e.target.closest(".seo-modal-close")))
      closeSeoModal();
  });
  document.addEventListener("keydown", function (e) {
    if (e.key === "Escape" && !modal.hidden) closeSeoModal();
  });
})();

// Service worker registration -- an identical copy of the block in shared.js,
// duplicated for the same reason as the profile dot above: these pages never
// load shared.js, and registering from every page (rather than only the study
// pages) means the app installs and starts caching no matter where a visitor
// lands. Keep in sync with shared.js.
(function registerServiceWorker() {
  if (!("serviceWorker" in navigator)) return;
  if (
    location.protocol !== "https:" &&
    location.hostname !== "localhost" &&
    location.hostname !== "127.0.0.1"
  )
    return;
  window.addEventListener("load", function () {
    navigator.serviceWorker.register("/sw.js").catch(function () {
      /* an unavailable worker is never fatal */
    });
  });
})();
