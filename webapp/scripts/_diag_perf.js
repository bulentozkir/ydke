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
 * Top Words -- headless load/behaviour probe.
 *
 * Two jobs:
 *   1. Smoke-test a page (any JS errors? does a card actually render?).
 *   2. Measure first-load cost on a throttled profile, so the lazy-loading
 *      work in shared.js can be reported as real before/after numbers.
 *
 * Usage:
 *   node scripts/_diag_perf.js                      # smoke-test the default pages
 *   node scripts/_diag_perf.js --perf               # throttled measurement of index.html
 *   node scripts/_diag_perf.js --pages a.html,b.html
 *   node scripts/_diag_perf.js --interact           # also exercise level/lang switching
 *   node scripts/_diag_perf.js --sw                 # first visit vs. warm visit vs. offline
 *
 * Needs a static server on BASE (see below) and playwright-core with a
 * Chromium already in the local browser cache.
 */
"use strict";

const fs = require("fs");
const path = require("path");
const { chromium } = require("playwright-core");

/**
 * playwright-core ships no browser of its own, and the version pinned here
 * rarely matches whatever Chromium build is already sitting in the shared
 * ms-playwright cache. Rather than force a fresh ~150 MB download just to
 * time a page load, reuse any cached chrome.exe we can find.
 */
function findChromium() {
  const roots = [
    process.env.PLAYWRIGHT_BROWSERS_PATH,
    path.join(process.env.LOCALAPPDATA || "", "ms-playwright"),
  ].filter(Boolean);
  const candidates = [];
  for (const root of roots) {
    let entries = [];
    try {
      entries = fs.readdirSync(root);
    } catch (e) {
      continue;
    }
    for (const e of entries) {
      const m = /^chromium(_headless_shell)?-(\d+)$/.exec(e);
      if (!m) continue;
      const exe = m[1]
        ? path.join(root, e, "chrome-headless-shell-win64", "chrome-headless-shell.exe")
        : path.join(root, e, "chrome-win64", "chrome.exe");
      if (fs.existsSync(exe)) candidates.push({ build: Number(m[2]), exe });
    }
  }
  candidates.sort((a, b) => b.build - a.build);
  return candidates.length ? candidates[0].exe : undefined;
}

const BASE = process.env.UDSP_BASE || "http://127.0.0.1:8902";

const DEFAULT_PAGES = [
  "index.html",
  "quiz.html",
  "wordlist.html",
  "wordmorph.html",
  "readingcomprehension.html",
  "stats.html",
];

function arg(name, fallback) {
  const i = process.argv.indexOf("--" + name);
  return i >= 0 && process.argv[i + 1] && !process.argv[i + 1].startsWith("--")
    ? process.argv[i + 1]
    : fallback;
}
const has = (name) => process.argv.includes("--" + name);

// Regular 4G, matching the DevTools preset, so numbers are comparable to a
// hand-run Lighthouse pass.
const THROTTLE_4G = {
  offline: false,
  downloadThroughput: (4 * 1024 * 1024) / 8,
  uploadThroughput: (3 * 1024 * 1024) / 8,
  latency: 20,
};
const CPU_SLOWDOWN = 4;

/** Attaches error/transfer collectors to a fresh page. */
function instrument(page) {
  const state = {
    errors: [],
    requests: [],
    bytes: 0,
    dataBytes: 0,
    networkBytes: 0,
    swResponses: 0,
  };
  page.on("pageerror", (e) => state.errors.push("pageerror: " + e.message));
  page.on("console", (m) => {
    if (m.type() === "error") state.errors.push("console.error: " + m.text());
  });
  page.on("requestfailed", (r) => {
    // favicon noise is not interesting
    if (!/favicon/.test(r.url()))
      state.errors.push("requestfailed: " + r.url() + " " + (r.failure() || {}).errorText);
  });
  page.on("response", async (r) => {
    const url = r.url();
    if (!url.startsWith(BASE)) return;
    let size = 0;
    try {
      size = Number((await r.headerValue("content-length")) || 0);
    } catch (e) {
      /* response may be gone */
    }
    state.bytes += size;
    // Playwright reports service-worker-fulfilled responses too, so separate
    // "came off the wire" from "came out of the Cache API".
    const fromSW = typeof r.fromServiceWorker === "function" && r.fromServiceWorker();
    if (fromSW) state.swResponses++;
    else state.networkBytes += size;
    if (url.includes("/data/")) {
      state.dataBytes += size;
      state.requests.push(url.slice(BASE.length + 1));
    }
  });
  return state;
}

async function newPage(context, { throttle }) {
  const page = await context.newPage();
  const client = await context.newCDPSession(page);
  await client.send("Network.setCacheDisabled", { cacheDisabled: true });
  await client.send("Network.clearBrowserCache");
  if (throttle) {
    await client.send("Network.emulateNetworkConditions", THROTTLE_4G);
    await client.send("Emulation.setCPUThrottlingRate", { rate: CPU_SLOWDOWN });
  }
  return page;
}
/** First Contentful Paint via the paint timeline, in ms. */
function readTimings(page) {
  return page.evaluate(() => {
    const fcp = performance.getEntriesByName("first-contentful-paint")[0];
    const nav = performance.getEntriesByType("navigation")[0] || {};
    return {
      fcp: fcp ? Math.round(fcp.startTime) : null,
      domContentLoaded: Math.round(nav.domContentLoadedEventEnd || 0),
      load: Math.round(nav.loadEventEnd || 0),
    };
  });
}

/**
 * The one thing a learner actually waits for. Every page that uses shared.js
 * shows the pool size in the header (#word-total, written by setLevel), so a
 * non-zero value there proves the level's data file arrived and parsed. Pages
 * with a start screen rather than an immediate card are checked on their own
 * "how many items are in this pool" readout instead.
 */
const CONTENT_PROBE = () => {
  const txt = (id) => {
    const el = document.getElementById(id);
    return el ? (el.textContent || "").trim() : "";
  };
  const num = (id) => {
    const m = /[\d,.]+/.exec(txt(id));
    return m ? Number(m[0].replace(/[^\d]/g, "")) : 0;
  };
  // Header pool count -- present on every shared.js page.
  if (num("word-total") <= 0) return false;
  // Page-specific "real content is on screen" check.
  const specific = [
    "fc-word", // flashcards
    "picker-word-total", // quiz setup screen
    "wordmorph-start-count", // word morph setup screen
    "rc-start-count", // reading comprehension setup screen
    "stats-grid",
  ];
  for (const id of specific) {
    if (!document.getElementById(id)) continue;
    const t = txt(id);
    return t.length > 0 && !/^(loading|—|-)$/i.test(t);
  }
  // Word List renders rows rather than a single element.
  const list = document.getElementById("word-list");
  if (list) return list.children.length > 0;
  return true;
};

async function waitForContent(page, timeout) {
  try {
    await page.waitForFunction(CONTENT_PROBE, null, { timeout });
    return true;
  } catch (e) {
    return false;
  }
}

async function probe(context, path, opts) {
  const page = await newPage(context, opts);
  const state = instrument(page);
  const t0 = Date.now();
  await page.goto(BASE + "/" + path, { waitUntil: "domcontentloaded", timeout: 60000 });
  const contentOk = await waitForContent(page, opts.throttle ? 60000 : 15000);
  const timeToContent = Date.now() - t0;
  const timings = await readTimings(page);
  // Let the idle prefetch settle so it shows up in the request list.
  await page.waitForTimeout(opts.throttle ? 500 : 1200);

  const result = {
    page: path,
    fcp: timings.fcp,
    domContentLoaded: timings.domContentLoaded,
    contentRendered: contentOk ? timeToContent : null,
    totalKB: Math.round(state.bytes / 1024),
    dataKB: Math.round(state.dataBytes / 1024),
    dataFiles: state.requests.slice(),
    errors: state.errors.slice(),
  };

  if (opts.interact) {
    result.interactions = await interact(page, state);
  }

  await page.close();
  return result;
}

/** Exercise the level and language selectors, which is where lazy loading bites. */
async function interact(page, state) {
  const out = [];
  const snapshot = () =>
    page.evaluate(() => ({
      level: typeof currentLevel === "string" ? currentLevel : null,
      lang: typeof currentLang === "string" ? currentLang : null,
      words: typeof WORDS !== "undefined" && WORDS ? WORDS.length : null,
      total: (document.getElementById("word-total") || {}).textContent || null,
    }));

  const levelSel = page.locator("#levels-nav");
  if (await levelSel.count()) {
    for (const level of ["C1", "MIX", "A1"]) {
      const options = await page.$$eval("#levels-nav option", (o) =>
        o.map((x) => x.value)
      );
      if (!options.includes(level)) continue;
      const before = state.requests.length;
      await levelSel.selectOption(level);
      await page.waitForTimeout(2500);
      const s = await snapshot();
      out.push({
        action: "level=" + level,
        ...s,
        newFiles: state.requests.slice(before),
      });
    }
  }

  const langSel = page.locator("#langs-nav");
  if (await langSel.count()) {
    for (const lang of ["de", "fr", "en"]) {
      const options = await page.$$eval("#langs-nav option", (o) =>
        o.map((x) => x.value)
      );
      if (!options.includes(lang)) continue;
      const before = state.requests.length;
      await langSel.selectOption(lang);
      await page.waitForTimeout(3000);
      const s = await snapshot();
      out.push({
        action: "lang=" + lang,
        ...s,
        newFiles: state.requests.slice(before),
      });
    }
  }
  return out;
}

function pad(s, n) {
  s = String(s === null || s === undefined ? "-" : s);
  return s + " ".repeat(Math.max(0, n - s.length));
}

/**
 * Service-worker lane check: cold visit, warm visit, then offline. The HTTP
 * cache stays disabled throughout, so anything that still renders on the warm
 * and offline passes came from the Cache API and nowhere else.
 */
async function swReport(context, path) {
  const rows = [];

  const cold = await newPage(context, { throttle: false });
  const coldState = instrument(cold);
  await cold.goto(BASE + "/" + path, { waitUntil: "domcontentloaded" });
  await waitForContent(cold, 20000);
  // Give the worker time to install, activate and populate its caches.
  await cold
    .waitForFunction(() => navigator.serviceWorker.controller !== null, null, { timeout: 20000 })
    .catch(() => {});
  await cold.waitForTimeout(4000);
  rows.push({ pass: "cold (no SW yet)", ...summarise(coldState) });
  await cold.close();

  const warm = await newPage(context, { throttle: false });
  const warmState = instrument(warm);
  await warm.goto(BASE + "/" + path, { waitUntil: "domcontentloaded" });
  const warmOk = await waitForContent(warm, 20000);
  await warm.waitForTimeout(2500);
  rows.push({ pass: "warm (SW active)", rendered: warmOk, ...summarise(warmState) });
  await warm.close();

  await context.setOffline(true);
  const off = await newPage(context, { throttle: false });
  const offState = instrument(off);
  let offOk = false;
  try {
    await off.goto(BASE + "/" + path, { waitUntil: "domcontentloaded", timeout: 20000 });
    offOk = await waitForContent(off, 20000);
  } catch (e) {
    /* recorded as not rendered */
  }
  rows.push({ pass: "offline", rendered: offOk, ...summarise(offState) });
  await off.close();
  await context.setOffline(false);

  return rows;
}

function summarise(state) {
  return {
    fromNetworkKB: Math.round(state.networkBytes / 1024),
    fromSW: state.swResponses,
    dataFiles: state.requests.length,
    // Offline, the third-party tags (Firebase, Google Fonts) fail by
    // design and log a generic "Failed to load resource". Those are not the
    // app breaking, so only real script errors count.
    errors: state.errors.filter(
      (e) => !/requestfailed|Failed to load resource/.test(e)
    ),
  };
}

// Drives the study loop the way a person does -- flip, grade, then switch the
// source selector -- and reads back localStorage. Grading and mistake capture
// are pure side effects with no visible output of their own, so asserting on
// the persisted state is the only way to know they actually happened.
async function srsReport(context) {
  const out = [];
  const page = await newPage(context, {});
  const state = instrument(page);
  await page.goto(BASE + "/index.html", { waitUntil: "domcontentloaded" });
  await waitForContent(page, 20000);

  const sources = await page.$$eval("#source-nav option", (o) =>
    o.map((x) => x.value + "=" + x.textContent.trim())
  );
  out.push({ check: "source selector", got: sources.join(" | ") });

  const word = await page.textContent("#fc-word");
  await page.click("#flashcard");
  await page.waitForTimeout(200);
  const persisted = await page.evaluate(() => ({
    mistakes: JSON.parse(localStorage.getItem("udsp_mistakes_v1") || "{}"),
    srs: JSON.parse(localStorage.getItem("udsp_srs_v1") || "{}"),
  }));
  const mKeys = Object.keys(persisted.mistakes);
  const sKeys = Object.keys(persisted.srs);
  out.push({
    check: "flip does not schedule",
    got:
      "word=" + word +
      ", mistakes=" + mKeys.length + " (" + (mKeys[0] || "-") + ")" +
      ", srs cards=" + sKeys.length,
  });

  // Flashcards no longer grade recall, so the schedule and the mistake list
  // are fed only by objective right/wrong from the quiz and the games. With a
  // fresh profile this pool is therefore empty and must say so.
  await page.selectOption("#source-nav", "mistakes");
  await page.waitForTimeout(300);
  out.push({
    check: "source=mistakes",
    got:
      "count=" + (await page.textContent("#word-total")) +
      ", card hidden=" + (await page.isHidden("#flashcard")),
  });

  // Nothing has been starred, so this pool is legitimately empty and should
  // explain itself rather than showing an unusable "1 / 0" card.
  await page.selectOption("#source-nav", "starred");
  await page.waitForTimeout(300);
  out.push({
    check: "source=starred (empty)",
    got:
      "card hidden=" + (await page.isHidden("#flashcard")) +
      ", message=" + JSON.stringify(await page.textContent("#fc-empty")),
  });

  // The selector must survive a reload, or it is not a mode, just a filter.
  await page.selectOption("#source-nav", "due");
  await page.waitForTimeout(300);
  await page.reload({ waitUntil: "domcontentloaded" });
  await page.waitForTimeout(800);
  out.push({
    check: "persists reload",
    got: "source=" + (await page.inputValue("#source-nav")),
  });

  // Quiz has to feed the same pool, otherwise "my mistakes" only ever knows
  // about flashcards.
  await page.selectOption("#source-nav", "all");
  await page.waitForTimeout(300);
  await page.goto(BASE + "/quiz.html", { waitUntil: "domcontentloaded" });
  await waitForContent(page, 20000);
  const beforeQuiz = await page.evaluate(
    () => Object.keys(JSON.parse(localStorage.getItem("udsp_mistakes_v1") || "{}")).length
  );
  await page.click(".exam-tile");
  await page.waitForTimeout(400);
  // Deliberately pick a wrong option so a mistake is guaranteed to be logged.
  // Matched by INDEX, not by text: options render as bilingual HTML, so
  // comparing textContent against q.answer never matches and the "wrong"
  // button picked would be whichever came first -- correct half the time.
  const picked = await page.evaluate(() => {
    const q = quizState.questions[quizState.current];
    const right = q.options.indexOf(q.answer);
    const btns = Array.from(document.querySelectorAll("#quiz-options .option"));
    const i = btns.length > 1 ? (right === 0 ? 1 : 0) : 0;
    btns[i].click();
    return { options: btns.length, right: right, clicked: i };
  });
  await page.waitForTimeout(300);
  const afterQuiz = await page.evaluate(
    () => Object.keys(JSON.parse(localStorage.getItem("udsp_mistakes_v1") || "{}")).length
  );
  out.push({
    check: "quiz wrong answer",
    got:
      "options=" + picked.options + ", correct idx=" + picked.right +
      ", clicked=" + picked.clicked + ", mistakes " + beforeQuiz + " -> " + afterQuiz,
  });

  // Back on Flashcards, that quiz mistake must be reachable -- this is the
  // whole point: the two surfaces feed one pool.
  await page.goto(BASE + "/index.html", { waitUntil: "domcontentloaded" });
  await waitForContent(page, 20000);
  await page.selectOption("#source-nav", "mistakes");
  await page.waitForTimeout(400);
  out.push({
    check: "quiz mistake -> cards",
    got:
      "count=" + (await page.textContent("#word-total")) +
      ", card=" + (await page.textContent("#fc-word")),
  });

  out.errors = summarise(state).errors;
  await page.close();
  return out;
}

(async () => {
  const perf = has("perf");
  const sw = has("sw");
  const srs = has("srs");
  const pages = arg("pages", "")
    ? arg("pages", "").split(",")
    : perf || sw
    ? ["index.html"]
    : DEFAULT_PAGES;

  const browser = await chromium.launch({
    headless: true,
    executablePath: findChromium(),
  });
  const context = await browser.newContext({
    viewport: { width: 412, height: 915 },
    deviceScaleFactor: 2,
    isMobile: true,
    hasTouch: true,
  });

  // shared.js sends brand-new visitors to home.html once (the orientation
  // dashboard). Measure the RETURNING-visitor path instead, which is the one
  // people actually spend their time on -- otherwise every probe just times
  // the Home page.
  await context.addInitScript(() => {
    try {
      localStorage.setItem("udsp_welcomed_v1", "1");
    } catch (e) {
      /* ignore */
    }
  });

  if (srs) {
    console.log("learning loop check -- flashcards + quiz");
    console.log("");
    const rows = await srsReport(context);
    for (const r of rows) {
      console.log(pad(r.check, 24) + r.got);
    }
    console.log("");
    if (rows.errors.length) {
      for (const e of rows.errors) console.log("FAIL " + e);
    } else {
      console.log("no JS errors");
    }
    await browser.close();
    process.exit(rows.errors.length ? 1 : 0);
  }

  if (sw) {
    for (const p of pages) {
      console.log("service worker check -- " + p.trim() + " (HTTP cache disabled throughout)");
      console.log("");
      console.log(
        pad("pass", 20) + pad("rendered", 10) + pad("network KB", 12) + pad("from SW", 9) + "data files"
      );
      console.log("-".repeat(64));
      const rows = await swReport(context, p.trim());
      for (const r of rows) {
        console.log(
          pad(r.pass, 20) +
            pad(r.rendered === undefined ? "yes" : r.rendered ? "yes" : "NO", 10) +
            pad(r.fromNetworkKB, 12) +
            pad(r.fromSW, 9) +
            r.dataFiles
        );
      }
      const bad = rows.filter((r) => r.rendered === false || r.errors.length);
      console.log("");
      for (const r of bad) {
        console.log("FAIL " + r.pass + ": " + (r.errors.join("; ") || "content never rendered"));
      }
      console.log(bad.length ? bad.length + " problem(s)" : "offline study works");
    }
    await browser.close();
    process.exit(0);
  }

  const opts = { throttle: perf, interact: has("interact") };
  console.log(
    "profile: " +
      (perf ? "4G + " + CPU_SLOWDOWN + "x CPU throttle, cache disabled" : "unthrottled, cache disabled")
  );
  console.log("");
  console.log(
    pad("page", 30) + pad("FCP", 8) + pad("content", 10) + pad("total KB", 10) + pad("data KB", 9) + "data files"
  );
  console.log("-".repeat(96));

  const results = [];
  for (const p of pages) {
    const r = await probe(context, p.trim(), opts);
    results.push(r);
    console.log(
      pad(r.page, 30) +
        pad(r.fcp !== null ? r.fcp + "ms" : "-", 8) +
        pad(r.contentRendered !== null ? r.contentRendered + "ms" : "TIMEOUT", 10) +
        pad(r.totalKB, 10) +
        pad(r.dataKB, 9) +
        r.dataFiles.length
    );
  }

  console.log("");
  for (const r of results) {
    if (r.dataFiles.length) {
      console.log(r.page + " data files: " + r.dataFiles.join(", "));
    }
    if (r.interactions) {
      for (const i of r.interactions) {
        console.log(
          "  " +
            pad(i.action, 12) +
            pad("lang=" + i.lang, 10) +
            pad("level=" + i.level, 12) +
            pad("words=" + i.words, 14) +
            pad("total=" + String(i.total).trim(), 16) +
            "fetched: " +
            (i.newFiles.length ? i.newFiles.join(", ") : "(none -- cached)")
        );
      }
    }
  }

  console.log("");
  let bad = 0;
  for (const r of results) {
    if (r.contentRendered === null) {
      bad++;
      console.log("FAIL " + r.page + ": content never rendered");
    }
    for (const e of r.errors) {
      bad++;
      console.log("FAIL " + r.page + ": " + e);
    }
  }
  console.log(bad ? bad + " problem(s)" : "no JS errors, all pages rendered content");

  await browser.close();
  process.exit(bad ? 1 : 0);
})().catch((e) => {
  console.error(e);
  process.exit(2);
});
