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

/**
 * Top Words -- service worker.
 *
 * Why this exists: the vocabulary data is ~36 MB of JavaScript that changes
 * a handful of times a year, and before this worker every visit re-validated
 * all of it over the network and studying offline was impossible. The worker
 * splits traffic into three lanes:
 *
 *   1. /data/*.js  -- cache-first. These files are big and effectively
 *                     immutable, so a cache hit is served instantly with no
 *                     network at all. A background refresh is kicked off only
 *                     when the cached copy is older than DATA_MAX_AGE, which
 *                     keeps steady-state visits at zero data requests while
 *                     still picking up corrections within a day.
 *   2. navigations -- network-first, cache fallback. HTML is small and is the
 *                     thing most likely to change, so online users always get
 *                     the current page; offline users get the last copy they
 *                     saw. This is also what makes "airplane mode" study work.
 *   3. static assets -- stale-while-revalidate. CSS, the per-mode JS bundles
 *                      and icons paint immediately from cache and refresh
 *                      quietly in the background.
 *
 * Everything cross-origin (Firebase, Google Fonts) is left alone --
 * the worker does not touch requests it has no business caching.
 *
 * Bump CACHE_VERSION to force every client to drop its caches; that is the
 * escape hatch if a bad data file or stylesheet ever ships.
 */
"use strict";

const CACHE_VERSION = "v1";
const SHELL_CACHE = "udsp-shell-" + CACHE_VERSION;
const DATA_CACHE = "udsp-data-" + CACHE_VERSION;
const CURRENT_CACHES = [SHELL_CACHE, DATA_CACHE];

// How stale a cached word list may get before the worker quietly refreshes it
// in the background. The user never waits for this -- they already got the
// cached copy -- so the only cost is one conditional request per file per day.
const DATA_MAX_AGE_MS = 24 * 60 * 60 * 1000;
const DATA_FETCH_TIMEOUT_MS = 15000;
const NAVIGATION_TIMEOUT_MS = 3500;

/**
 * The minimum set needed to open the app offline from a cold start. Everything
 * else (the other 20 pages, the per-mode JS) is cached the first time it is
 * visited, which keeps install cheap -- a first-time visitor should not pay for
 * 20 pages they may never open.
 */
const SHELL_ASSETS = [
  "/",
  "/index.html",
  "/home.html",
  "/styles.css",
  "/shared.js",
  "/flashcards.js",
  "/home.js",
  "/moresheet.js",
  "/i18n.js",
  "/icon.svg",
  "/site.webmanifest",
];

const OFFLINE_HTML =
  '<!doctype html><html lang="tr"><head><meta charset="utf-8">' +
  '<meta name="viewport" content="width=device-width,initial-scale=1">' +
  "<title>Çevrimdışı · Offline</title><style>" +
  "body{margin:0;min-height:100vh;display:flex;align-items:center;justify-content:center;" +
  "background:#0f172a;color:#e2e8f0;font:16px/1.6 system-ui,-apple-system,Segoe UI,Roboto,sans-serif;" +
  "text-align:center;padding:24px}p{margin:8px 0;color:#94a3b8}" +
  "h1{font-size:1.25rem;margin:0 0 4px}</style></head><body><div>" +
  "<h1>Çevrimdışısınız · You're offline</h1>" +
  "<p>Bu sayfa henüz indirilmedi.</p>" +
  "<p>This page hasn't been downloaded yet. Pages you've already visited still work offline.</p>" +
  "</div></body></html>";

self.addEventListener("install", (event) => {
  event.waitUntil(
    caches
      .open(SHELL_CACHE)
      // addAll() is all-or-nothing: one 404 would abort the whole install and
      // leave the site with no worker at all. Cache each asset independently so
      // a single missing file degrades rather than breaks.
      .then((cache) =>
        Promise.all(
          SHELL_ASSETS.map((url) =>
            cache.add(new Request(url, { cache: "reload" })).catch(() => {})
          )
        )
      )
      .then(() => self.skipWaiting())
  );
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) =>
        Promise.all(
          keys
            .filter((k) => k.startsWith("udsp-") && CURRENT_CACHES.indexOf(k) === -1)
            .map((k) => caches.delete(k))
        )
      )
      .then(() => self.clients.claim())
  );
});

/** Age of a cached response, in ms, from the server's Date header. */
function ageOf(response) {
  const date = response.headers.get("date");
  if (!date) return Infinity;
  const t = Date.parse(date);
  return isNaN(t) ? Infinity : Date.now() - t;
}

function isCacheable(response) {
  // Opaque responses (no-cors cross-origin) report status 0 and would silently
  // poison the cache with unreadable entries.
  return response && response.status === 200 && response.type === "basic";
}

async function fetchWithTimeout(request, timeoutMs) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), timeoutMs);
  try {
    return await fetch(request, { signal: controller.signal });
  } finally {
    clearTimeout(timeout);
  }
}

/** Lane 1: big, near-immutable word lists. */
async function handleData(request) {
  const cache = await caches.open(DATA_CACHE);
  const cached = await cache.match(request);

  if (cached) {
    if (ageOf(cached) > DATA_MAX_AGE_MS) {
      // Deliberately not awaited: the user already has their words.
      fetchWithTimeout(request, DATA_FETCH_TIMEOUT_MS)
        .then((fresh) => {
          if (isCacheable(fresh)) cache.put(request, fresh);
        })
        .catch(() => {});
    }
    return cached;
  }

  const fresh = await fetchWithTimeout(request, DATA_FETCH_TIMEOUT_MS);
  if (isCacheable(fresh)) cache.put(request, fresh.clone());
  return fresh;
}

/** Lane 2: HTML. Freshness matters more than speed here, and HTML is tiny. */
async function handleNavigation(request) {
  const cache = await caches.open(SHELL_CACHE);
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), NAVIGATION_TIMEOUT_MS);
  try {
    const fresh = await fetch(request, { signal: controller.signal });
    if (isCacheable(fresh)) cache.put(request, fresh.clone());
    return fresh;
  } catch (e) {
    const cached =
      (await cache.match(request)) ||
      // Vercel's cleanUrls means /quiz and /quiz.html are the same page, so a
      // cache stored under one form still answers the other.
      (await cache.match(new URL(request.url).pathname + ".html")) ||
      (await cache.match("/index.html"));
    return (
      cached ||
      new Response(OFFLINE_HTML, {
        status: 503,
        headers: { "Content-Type": "text/html; charset=utf-8" },
      })
    );
  } finally {
    clearTimeout(timeout);
  }
}

/** Lane 3: CSS, per-mode JS, icons. */
async function handleAsset(request) {
  const cache = await caches.open(SHELL_CACHE);
  const cached = await cache.match(request);
  const network = fetch(request)
    .then((fresh) => {
      if (isCacheable(fresh)) cache.put(request, fresh.clone());
      return fresh;
    })
    .catch(() => null);
  if (cached) return cached;
  const fresh = await network;
  if (fresh) return fresh;
  throw new Error("offline and uncached: " + request.url);
}

self.addEventListener("fetch", (event) => {
  const request = event.request;
  if (request.method !== "GET") return;

  const url = new URL(request.url);
  if (url.origin !== self.location.origin) return; // Firebase, fonts
  if (request.headers.has("range")) return; // audio/video seeks

  if (request.mode === "navigate") {
    event.respondWith(handleNavigation(request));
    return;
  }
  if (url.pathname.startsWith("/data/")) {
    event.respondWith(handleData(request));
    return;
  }
  if (/\.(css|js|svg|png|ico|webmanifest|woff2?)$/.test(url.pathname)) {
    event.respondWith(handleAsset(request));
  }
});
