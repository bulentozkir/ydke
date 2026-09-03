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

// Throwaway check for the phone-only promotion of Word Morph out of the Games
// hub and into the header toolbar (2026-08-05).
//
// Asserts VISIBILITY, not DOM presence -- the tab and the hub tile both exist
// in the markup at every width (crawlers still see both links); CSS decides
// which one is shown. Same standalone playwright driver as
// scripts/_diag_seo_modal.js because the integrated browser tools are broken
// in this environment.
//
//   node scripts/_diag_morph_tab.js        (needs a server on UDSP_BASE)
const { chromium } = require("playwright-core");
const fs = require("fs");
const path = require("path");

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
  if (!candidates.length) throw new Error("no cached chromium found");
  return candidates[0].exe;
}

const BASE = process.env.UDSP_BASE || "http://127.0.0.1:8902";

let failures = 0;
function check(name, got, want) {
  const ok = got === want;
  if (!ok) failures++;
  console.log((ok ? "  ok   " : "  FAIL ") + name + "  got=" + got + " want=" + want);
}

// Every page carries the toolbar, so every page must carry the new tab.
const PAGES = [
  "about.html", "bingo.html", "clozetest.html", "dictation.html", "games.html",
  "hangman.html", "help.html", "history.html", "home.html", "index.html",
  "listening.html", "matrix.html", "memory.html", "oddoneout.html",
  "privacy.html", "profile.html", "quiz.html", "readingcomprehension.html",
  "scramble.html", "sentencescramble.html", "speedround.html", "stats.html",
  "survival.html", "terms.html", "truefalse.html", "wordclass.html",
  "wordlist.html", "wordmorph.html", "wordrace.html",
];

(async () => {
  const browser = await chromium.launch({ headless: true, executablePath: findChromium() });

  async function open(viewport, file) {
    const context = await browser.newContext({
      viewport,
      deviceScaleFactor: 1,
      isMobile: viewport.width <= 720,
      hasTouch: viewport.width <= 720,
      // sw.js caches HTML aggressively; without this the run can assert
      // against a previous build's markup and "fail" a correct change.
      serviceWorkers: "block",
    });
    // Without these, a first-run visitor is bounced to the welcome screen and
    // every assertion below silently measures the WRONG page.
    await context.addInitScript(() => {
      try {
        localStorage.setItem("udsp_welcomed_v1", "1");
        localStorage.setItem("udsp_intro_seen_v1", "1");
      } catch (e) {}
    });
    const page = await context.newPage();
    const client = await context.newCDPSession(page);
    await client.send("Network.setCacheDisabled", { cacheDisabled: true });
    await client.send("Network.clearBrowserCache");
    await page.goto(BASE + "/" + file, { waitUntil: "domcontentloaded" });
    await page.waitForTimeout(250);
    check("landed on " + file, page.url().endsWith("/" + file), true);
    return { context, page };
  }

  const visible = (sel) =>
    `(() => { const el = document.querySelector(${JSON.stringify(sel)});
       if (!el) return "missing";
       const r = el.getBoundingClientRect();
       return (r.width > 0 && r.height > 0) ? "visible" : "hidden"; })()`;

  // ---- 1. Games hub: 15 visible tiles on phones, 16 on desktop -------------
  for (const [label, vp, wantTiles, wantTile, wantTab] of [
    ["phone 390", { width: 390, height: 844 }, 15, "hidden", "visible"],
    ["phone 360", { width: 360, height: 780 }, 15, "hidden", "visible"],
    ["phone 320", { width: 320, height: 568 }, 15, "hidden", "visible"],
    ["tablet 721", { width: 721, height: 900 }, 16, "visible", "hidden"],
    ["desktop 1280", { width: 1280, height: 900 }, 16, "visible", "hidden"],
  ]) {
    const { context, page } = await open(vp, "games.html");
    console.log("games.html @ " + label);
    const shown = await page.evaluate(`
      Array.from(document.querySelectorAll(".game-tile"))
        .filter((el) => el.getBoundingClientRect().height > 0).length`);
    check("visible game tiles", shown, wantTiles);
    check("total tiles in DOM (crawlers)",
      await page.evaluate(`document.querySelectorAll(".game-tile").length`), 16);
    check("Word Morph tile", await page.evaluate(visible(".game-tile.is-morph")), wantTile);
    check("Word Morph tab", await page.evaluate(visible(".bottom-nav-item.bn-morph")), wantTab);
    check("toolbar items visible", await page.evaluate(`
      Array.from(document.querySelectorAll(".bottom-nav-item"))
        .filter((el) => el.getBoundingClientRect().height > 0).length`),
      wantTab === "visible" ? 6 : 5);
    await context.close();
  }

  // ---- 2. Six tabs must not overflow the header on the narrowest phones ----
  // The toolbar sits on its OWN full-bleed row below the wordmark on phones,
  // so a sixth tab must not cost the wordmark any width -- assert that
  // directly (measure, then hide the tab and re-measure) rather than assuming
  // the wordmark fits, which it already did not at 320px before this change.
  for (const w of [320, 360, 390, 412]) {
    const { context, page } = await open({ width: w, height: 780 }, "games.html");
    console.log("overflow/fit @ " + w + "px");
    check("no horizontal scroll", await page.evaluate(
      `document.documentElement.scrollWidth <= document.documentElement.clientWidth`), true);
    check("no label wraps to 2 lines", await page.evaluate(`
      Array.from(document.querySelectorAll(".bn-label"))
        .every((el) => el.getClientRects().length === 1)`), true);
    check("wordmark width unchanged by the 6th tab", await page.evaluate(`
      (() => { const t = document.querySelector(".brand-title");
        const before = t.getBoundingClientRect().width;
        const s = document.createElement("style");
        s.textContent = ".bottom-nav-item.bn-morph{display:none!important}";
        document.head.appendChild(s);
        const after = t.getBoundingClientRect().width;
        s.remove();
        return Math.abs(before - after) < 0.5; })()`), true);
    await context.close();
  }

  // ---- 3. Active tab on wordmorph.html ------------------------------------
  const activeBg = (sel) =>
    `(() => { const el = document.querySelector(${JSON.stringify(sel)});
       return el ? getComputedStyle(el).backgroundColor : "missing"; })()`;
  {
    const { context, page } = await open({ width: 390, height: 844 }, "wordmorph.html");
    console.log("wordmorph.html @ phone 390");
    check("Morph tab is active", await page.evaluate(`
      document.querySelector(".bn-morph").classList.contains("is-active")`), true);
    check("Games tab active look muted", await page.evaluate(
      activeBg('.bottom-nav-item[href="games.html"]')), "rgba(0, 0, 0, 0)");
    await context.close();
  }
  {
    const { context, page } = await open({ width: 1280, height: 900 }, "wordmorph.html");
    console.log("wordmorph.html @ desktop 1280");
    check("Morph tab hidden", await page.evaluate(visible(".bn-morph")), "hidden");
    check("Games tab keeps gradient", await page.evaluate(`
      getComputedStyle(document.querySelector('.bottom-nav-item[href="games.html"]'))
        .backgroundImage.includes("gradient")`), true);
    await context.close();
  }

  // ---- 4. The tab exists (and links correctly) on every page ---------------
  {
    const { context, page } = await open({ width: 390, height: 844 }, "index.html");
    console.log("Morph tab present + visible on all " + PAGES.length + " pages");
    for (const file of PAGES) {
      await page.goto(BASE + "/" + file, { waitUntil: "domcontentloaded" });
      await page.waitForTimeout(150);
      const got = await page.evaluate(`
        (() => { if (!location.pathname.endsWith("/${file}")) return "redirected";
          const el = document.querySelector(".bottom-nav-item.bn-morph");
          if (!el) return "missing";
          if (!el.getAttribute("href").endsWith("wordmorph.html")) return "bad href";
          const r = el.getBoundingClientRect();
          return (r.width > 0 && r.height > 0) ? "visible" : "hidden"; })()`);
      check(file, got, "visible");
    }
    await context.close();
  }

  await browser.close();
  console.log(failures ? "\n" + failures + " CHECK(S) FAILED" : "\nall checks passed");
  process.exit(failures ? 1 : 0);
})();
