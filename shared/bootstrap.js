/*
 * Shared host bootstrap — injected into every document *before* any page script
 * runs. One copy, used by every native host:
 *
 *   Windows  CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync
 *   Linux    Tauri WebviewWindowBuilder::initialization_script
 *
 * Deliberately plain JavaScript with no host bindings, so it transfers between
 * hosts unchanged. Anything that needs a native call belongs in a per-platform
 * script, not here.
 *
 * The host substitutes these tokens before injection:
 *   __TW_PLATFORM__   "windows" | "linux" | "macos" | "android"
 *   __TW_IS_MSIX__    true on the MSIX build, false everywhere else
 *
 * Each section maps to a documented blocker. Windows IDs are given first,
 * Linux equivalents in brackets: W4 [L3], W5 [L7], W7 [L8]. The offline
 * warm-up at the end maps to Android B9, which has no Windows/Linux ID.
 */
(function () {
  "use strict";

  var NOTICE_KEY = "udsp_firstrun_notice_v1";
  var PACKAGED_FLAG = "udsp_packaged_v1";
  var PLATFORM = "__TW_PLATFORM__";

  /* ------------------------------------------------------------------ *
   * W4 / B4 / L3 — packaged-app detection surface
   *
   * Matches the window.TWApp contract described in android/03-blockers-and-fixes.md
   * so that once the udsp repo adopts it, the web code and this host agree.
   * ------------------------------------------------------------------ */
  var TWApp = {
    isMsix: function () { return __TW_IS_MSIX__; },
    isTWA: function () { return false; },
    isStandalone: function () { return true; },
    isPackaged: function () { return true; },
    platform: PLATFORM
  };

  try {
    Object.defineProperty(window, "TWApp", {
      value: Object.freeze(TWApp),
      writable: false,
      configurable: false
    });
  } catch (e) {
    window.TWApp = TWApp;
  }

  // Survives across documents even though the host re-injects on every navigation.
  try { sessionStorage.setItem(PACKAGED_FLAG, "1"); } catch (e) { /* opaque origin */ }

  /* ------------------------------------------------------------------ *
   * W4 / L3 — neutralise AdSense in-page.
   *
   * The host also blocks the ad networks before they reach the network; this
   * stops the inline `(adsbygoogle = window.adsbygoogle || []).push({})`
   * snippets from throwing and collapses the empty <ins> placeholders they
   * leave behind.
   * ------------------------------------------------------------------ */
  var adStub = { push: function () { return 0; }, loaded: true, length: 0 };
  try {
    Object.defineProperty(window, "adsbygoogle", {
      configurable: false,
      get: function () { return adStub; },
      set: function () { /* swallow */ }
    });
  } catch (e) { /* already defined */ }

  function addStyle(cssText) {
    var target = document.head || document.documentElement;
    if (!target) { return false; }
    var el = document.createElement("style");
    el.setAttribute("data-topwords-host", "1");
    el.textContent = cssText;
    target.appendChild(el);
    return true;
  }

  var HOST_CSS = [
    /* W4 / L3 — no reserved blank space where ads used to be. */
    "ins.adsbygoogle,.adsbygoogle{display:none !important}",

    /* ---------------------------------------------------------------- *
     * W7 / L8 — excessive vertical scrolling in the desktop window.
     *
     * The site already ships a proper app shell: pinned header, the nav
     * pinned under it, the page body locked at viewport height, and only
     * the content column scrolling. It is gated behind
     * @media (max-width:720px), and the app window is 1000px wide — so
     * the app got the plain document layout instead: the entire page
     * scrolled, carrying the header and nav off-screen, with a tall
     * block of crawler-facing SEO copy trailing every screen.
     *
     * Measured at 1000x800 before this fix (page height / viewport):
     *   home 2.2x   quiz 4.9x   about 4.8x   help 17.2x   wordlist 452x
     *
     * So: adopt that shell at every width. Only the STRUCTURAL rules are
     * taken — the phone sizing rules in the same block (shrunken fonts,
     * hidden button labels, tightened padding) are deliberately not
     * copied, since there is no reason to cramp a desktop window.
     * ---------------------------------------------------------------- */
    "@media (min-width:721px){",
    /* Lock the page; hand scrolling to the content column alone. */
    "  body{height:100vh;height:100svh;overflow:hidden}",
    "  .site-header{position:sticky;top:0;z-index:30}",
    "  .site-footer{display:none}",
    "  .container{flex:1 1 auto;min-height:0;display:flex;flex-direction:column;",
    "    overflow-y:auto;overflow-x:hidden;overscroll-behavior:contain}",
    "  .view.is-active{display:flex;flex-direction:column;flex:1 1 auto;min-height:0}",

    /* Let the flashcard grow into the window instead of pushing its own
       controls below the fold. */
    "  #view-flashcards>.study-status,#view-flashcards>.progress-bar,",
    "  #view-flashcards>.card-actions,#view-flashcards>.nav-row{flex:0 0 auto}",
    "  .flashcard{flex:1 1 auto;height:auto;min-height:160px}",

    /* Same for the quiz and word-morph boards. */
    "  #quiz-active,#wordmorph-game{display:flex;flex-direction:column;flex:1 1 auto;min-height:0}",
    "  #quiz-active[hidden],#wordmorph-game[hidden]{display:none}",
    "  #quiz-active>.toolbar,#quiz-active>.progress-bar,#wordmorph-game>.toolbar{flex:0 0 auto}",
    "  #quiz-active>.quiz-card,#wordmorph-game>.quiz-card{flex:1 1 auto;min-height:140px;overflow-y:auto}",

    /* The layout is capped at 760px, so a 1000px window wasted a quarter
       of its width and stacked what could sit side by side. The site's
       grids are all auto-fill, so widening the cap gives them extra
       columns — and fewer rows — for free. */
    "  :root{--maxw:1040px}",

     /* Five readable columns at the app's 1000px default width. The site's
       104px minimum creates eight narrow cards and truncates most labels. */
     "  .game-tiles{grid-template-columns:repeat(auto-fill,minmax(160px,1fr));gap:12px}",
     "  .game-tile{padding:16px 12px}",
     "  .game-tile-desc{-webkit-line-clamp:3}",

    /* SEO copy is written for crawlers and appended below the real UI.
       There are no crawlers in a packaged app, and on the interactive
       pages it was the single largest source of scrolling. Hidden only
       where an app view is present, so the pages whose actual content
       is prose — About, Help — keep theirs.

       L8: :has() needs WebKitGTK 2.42+ (GNOME 45, autumn 2023). On an
       older engine this one selector is dropped by the CSS parser and
       the SEO copy simply stays visible; nothing else in the block is
       affected, because invalid selectors do not invalidate the rules
       around them. */
    "  .container:has(.view.is-active) .seo-content{display:none}",
    "}",

    /* Host-owned first-run notice (W5 / L7). */
    ".tw-host-notice{position:fixed;left:16px;right:16px;bottom:16px;z-index:2147483000;",
    "  max-width:640px;margin:0 auto;padding:14px 16px;border-radius:12px;",
    "  background:#0f172a;color:#f8fafc;border:1px solid rgba(248,250,252,.18);",
    "  box-shadow:0 10px 30px rgba(0,0,0,.35);font:14px/1.5 system-ui,Segoe UI,sans-serif}",
    ".tw-host-notice a{color:#7dd3fc;font-weight:600}",
    ".tw-host-notice button{margin-left:12px;background:transparent;color:inherit;",
    "  border:1px solid rgba(248,250,252,.35);border-radius:8px;padding:4px 10px;cursor:pointer}"
  ].join("\n");

  if (!addStyle(HOST_CSS)) {
    document.addEventListener("DOMContentLoaded", function () { addStyle(HOST_CSS); }, { once: true });
  }

  /* ------------------------------------------------------------------ *
   * W5 / L7 — storage isolation looks like data loss.
   *
   * The packaged app has its own storage partition, so someone who studied on
   * the website arrives to streak 0 and no known words. Explain it once, and
   * only to users who genuinely have nothing stored yet.
   * ------------------------------------------------------------------ */
  var PROGRESS_KEYS = ["udsp_known_v1", "udsp_streak_v1", "udsp_fav_v1"];

  function hasExistingProgress() {
    for (var i = 0; i < PROGRESS_KEYS.length; i++) {
      var raw = null;
      try { raw = localStorage.getItem(PROGRESS_KEYS[i]); } catch (e) { return true; }
      if (raw && raw !== "{}" && raw !== "[]" && raw !== "0" && raw !== "null") {
        return true;
      }
    }
    return false;
  }

  function showFirstRunNotice() {
    if (document.querySelector(".tw-host-notice")) { return; }

    var box = document.createElement("div");
    box.className = "tw-host-notice";
    box.setAttribute("role", "status");

    var text = document.createElement("span");
    text.textContent =
      "Web sitesindeki ilerlemen bu uygulamaya otomatik aktarılmaz. " +
      "Giriş yaparak buluttan geri yükleyebilirsin. ";

    var link = document.createElement("a");
    link.href = "/profile";
    link.textContent = "Giriş yap";

    var dismiss = document.createElement("button");
    dismiss.type = "button";
    dismiss.textContent = "Kapat";
    dismiss.addEventListener("click", function () { box.remove(); });

    box.appendChild(text);
    box.appendChild(link);
    box.appendChild(dismiss);
    document.body.appendChild(box);
  }

  function maybeNotify() {
    var seen = null;
    try { seen = localStorage.getItem(NOTICE_KEY); } catch (e) { return; }
    if (seen || hasExistingProgress()) { return; }

    try { localStorage.setItem(NOTICE_KEY, "1"); } catch (e) { return; }
    showFirstRunNotice();
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", maybeNotify, { once: true });
  } else {
    maybeNotify();
  }

  /* ------------------------------------------------------------------ *
   * B9 — 36 MB cold-start data download.
   *
   * sw.js caches /data/*.js cache-first, but only lazily: a word list is
   * stored the first time the user opens it. A fresh install that goes
   * offline before studying therefore has no words at all, and the first
   * study session on a slow link stalls on a multi-megabyte file.
   *
   * B9 rules out the obvious fix — adding data/ to SHELL_ASSETS — because
   * that would bill every *website* visitor 36 MB on first paint to solve an
   * app-only problem. This script is injected only by the native hosts, so
   * warming the cache here is exactly that app-only fix: the website is
   * untouched.
   *
   * Source is the mirror in this repo (scripts/sync-data.ps1 keeps it in step
   * with the udsp repo), not the live site, so a cold start does not depend on
   * the app's own origin being reachable.
   *
   * This fills the cache once. Keeping it *current* is still sw.js's job: it
   * re-fetches anything older than 24 h from the real origin.
   * ------------------------------------------------------------------ */

  var DATA_REPO = "bulentozkir/ydke";

  // A branch is a moving target: whatever it points at is what every install
  // downloads and then executes on the udsp origin. Release builds should pin
  // an immutable commit SHA here, which is the only thing that makes the
  // manifest's hashes worth anything — hashes fetched from the same mutable
  // ref as the files they describe verify integrity, not authenticity.
  var DATA_REF = "main";

  var DATA_BASE = "https://raw.githubusercontent.com/" + DATA_REPO + "/" + DATA_REF + "/data/";

  // Must match CACHE_VERSION in the udsp repo's sw.js, or the warm-up fills a
  // cache nothing ever reads.
  var DATA_CACHE = "udsp-data-v1";

  var PRECACHE_KEY = "udsp_data_precache_v1";
  var PRECACHE_CONCURRENCY = 3;

  // The manifest chooses which cache keys get written. Anything that is not a
  // bare filename could escape /data/ and overwrite app code — a manifest
  // entry named "../shared.js" would replace the site's own script. Names are
  // therefore matched, not sanitised.
  var DATA_NAME_RE = /^[A-Za-z0-9_-]+\.js$/;

  var FETCH_OPTS = { cache: "no-store", credentials: "omit", referrerPolicy: "no-referrer" };

  function toHex(buffer) {
    var bytes = new Uint8Array(buffer);
    var out = "";
    for (var i = 0; i < bytes.length; i++) {
      out += (bytes[i] < 16 ? "0" : "") + bytes[i].toString(16);
    }
    return out;
  }

  function dataCacheKey(name) {
    return new URL("/data/" + name, location.origin).href;
  }

  function loadManifest() {
    return fetch(DATA_BASE + "manifest.json", FETCH_OPTS)
      .then(function (res) {
        if (!res.ok) { throw new Error("manifest HTTP " + res.status); }
        return res.json();
      })
      .then(function (manifest) {
        var files = (manifest && manifest.files) || [];
        return files.filter(function (e) {
          return e &&
            typeof e.name === "string" && DATA_NAME_RE.test(e.name) &&
            typeof e.sha256 === "string" &&
            typeof e.bytes === "number";
        });
      });
  }

  function storeEntry(cache, entry, buffer) {
    // Rebuilt rather than cached as received: raw.githubusercontent.com serves
    // .js as text/plain *and* sends X-Content-Type-Options: nosniff, so
    // replaying its response for a <script src="/data/..."> would be refused
    // by the browser. Date is set because sw.js reads it to decide staleness.
    var response = new Response(buffer, {
      status: 200,
      statusText: "OK",
      headers: {
        "Content-Type": "text/javascript; charset=utf-8",
        "Date": new Date().toUTCString(),
        "Cache-Control": "public, max-age=3600"
      }
    });
    return cache.put(dataCacheKey(entry.name), response);
  }

  function fetchAndStore(cache, entry) {
    return fetch(DATA_BASE + entry.name, FETCH_OPTS)
      .then(function (res) {
        if (!res.ok) { throw new Error(entry.name + ": HTTP " + res.status); }
        return res.arrayBuffer();
      })
      .then(function (buffer) {
        if (buffer.byteLength !== entry.bytes) {
          throw new Error(entry.name + ": expected " + entry.bytes + " bytes, got " + buffer.byteLength);
        }
        return crypto.subtle.digest("SHA-256", buffer).then(function (digest) {
          if (toHex(digest) !== entry.sha256.toLowerCase()) {
            // Never cache unverified content: this buffer becomes script that
            // runs with the app's own origin privileges.
            throw new Error(entry.name + ": SHA-256 mismatch");
          }
          return storeEntry(cache, entry, buffer);
        });
      });
  }

  function drainQueue(cache, entries) {
    var next = 0;
    var failures = 0;

    function worker() {
      if (next >= entries.length) { return Promise.resolve(); }
      var entry = entries[next++];
      return fetchAndStore(cache, entry)
        .catch(function (err) {
          failures++;
          if (window.console && console.debug) { console.debug("topwords: " + err.message); }
        })
        .then(worker);
    }

    var workers = [];
    for (var i = 0; i < PRECACHE_CONCURRENCY && i < entries.length; i++) {
      workers.push(worker());
    }
    return Promise.all(workers).then(function () { return failures; });
  }

  function completionFlag(count) {
    return DATA_REF + "|" + count;
  }

  function warmDataCache() {
    // Secure-context APIs. The app loads over https, but a host pointed at a
    // local build would silently have neither.
    if (!window.caches || !window.fetch || !window.crypto || !crypto.subtle) { return; }
    if (navigator.connection && navigator.connection.saveData) { return; }
    if (navigator.onLine === false) { return; }

    var cache = null;

    caches.open(DATA_CACHE)
      .then(function (opened) {
        cache = opened;
        return cache.keys();
      })
      .then(function (keys) {
        var flag = null;
        try { flag = localStorage.getItem(PRECACHE_KEY); } catch (e) { /* opaque origin */ }

        // The cache itself is the source of truth, so a CACHE_VERSION bump or a
        // user clearing site data re-arms the warm-up instead of being masked
        // by a stale "already done" flag.
        if (flag === completionFlag(keys.length)) { return null; }

        var have = {};
        for (var i = 0; i < keys.length; i++) { have[keys[i].url] = true; }

        return loadManifest().then(function (files) {
          var missing = files.filter(function (e) { return !have[dataCacheKey(e.name)]; });
          if (missing.length === 0) { return 0; }
          return drainQueue(cache, missing);
        });
      })
      .then(function (failures) {
        if (failures === null) { return; }
        if (failures > 0) {
          // Leave the flag unset so the next launch resumes. Already-cached
          // files are skipped, so a retry only re-fetches what actually failed.
          return;
        }
        return cache.keys().then(function (keys) {
          try { localStorage.setItem(PRECACHE_KEY, completionFlag(keys.length)); } catch (e) { /* ignore */ }
        });
      })
      .catch(function (err) {
        // Non-fatal by design: the app works online, and sw.js still caches
        // each word list the first time it is opened.
        if (window.console && console.info) {
          console.info("topwords: offline word data not pre-loaded:", err && err.message);
        }
      });
  }

  function scheduleWarmUp() {
    // Never in front of first paint, and never in front of the first fetches
    // the page makes for itself.
    var start = function () {
      if (window.requestIdleCallback) {
        requestIdleCallback(warmDataCache, { timeout: 10000 });
      } else {
        setTimeout(warmDataCache, 3000);
      }
    };

    if (document.readyState === "complete") {
      setTimeout(start, 2000);
    } else {
      window.addEventListener("load", function () { setTimeout(start, 2000); }, { once: true });
    }
  }

  scheduleWarmUp();
})();
