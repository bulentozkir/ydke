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

/*
 * Verifies the UX-#5 fixes in a real browser:
 *   - Word List: everything past the first page is reachable via "Show more"
 *     (it used to be a hard 15,000 cap -- those words simply did not exist
 *     unless you already knew them and searched)
 *   - Feedback regions announce to assistive tech (role="status")
 *   - Word Morph's footer count reports ITS pool, not the CEFR total
 *
 *   node scripts/_diag_a11y_paging.js
 */
const fs = require("fs");
const path = require("path");
const { chromium } = require("playwright-core");

const BASE = process.env.UDSP_BASE || "http://127.0.0.1:8902";

function findChromium() {
  const root = path.join(
    process.env.LOCALAPPDATA || path.join(process.env.HOME || "", ".cache"),
    "ms-playwright"
  );
  if (!fs.existsSync(root)) return null;
  const candidates = [];
  for (const dir of fs.readdirSync(root)) {
    if (!/^chromium/.test(dir)) continue;
    for (const rel of [
      "chrome-win64/chrome.exe",
      "chrome-win/chrome.exe",
      "chrome-headless-shell-win64/chrome-headless-shell.exe",
    ]) {
      const exe = path.join(root, dir, rel);
      if (fs.existsSync(exe)) candidates.push(exe);
    }
  }
  return candidates.find((c) => c.includes("chrome-win")) || candidates[0] || null;
}

function ok(pass, label, detail) {
  console.log(`${pass ? "PASS" : "FAIL"}  ${label.padEnd(24)} ${detail}`);
  return pass;
}

(async () => {
  const exe = findChromium();
  const browser = await chromium.launch(exe ? { executablePath: exe } : {});
  const context = await browser.newContext({
    viewport: { width: 412, height: 915 },
    deviceScaleFactor: 2,
    isMobile: true,
    hasTouch: true,
  });
  await context.addInitScript(() => {
    try {
      localStorage.setItem("udsp_welcomed_v1", "1");
    } catch (e) {}
  });

  let allPass = true;
  const page = await context.newPage();
  const errors = [];
  page.on("pageerror", (e) => errors.push(String(e)));
  const client = await context.newCDPSession(page);
  await client.send("Network.setCacheDisabled", { cacheDisabled: true });
  await client.send("Network.clearBrowserCache");

  // ---- Word List paging ----
  await page.goto(`${BASE}/wordlist.html`, { waitUntil: "networkidle" });
  await page.waitForFunction(() => typeof WORDS !== "undefined" && WORDS.length > 0);

  // At the real page size, English B2 (2,905 words) fits in one page -- the
  // everyday case must NOT have gained a button.
  // The table renders incrementally, so sample only once it has settled --
  // otherwise this reads a partial count and reports a phantom failure.
  await page.waitForFunction(
    () => wlShown >= Math.min(wlMatches.length, WL_PAGE_SIZE),
    null,
    { timeout: 120000 }
  );
  const single = await page.evaluate(() => ({
    total: wlMatches.length,
    shown: wlShown,
    size: WL_PAGE_SIZE,
    moreBtn: !!document.getElementById("wl-more-btn"),
  }));
  allPass &= ok(
    single.shown === single.total && !single.moreBtn,
    "single level unpaged",
    `${single.shown.toLocaleString()} words, page size ${single.size.toLocaleString()}, no button=${!single.moreBtn}`
  );

  // Mix is far bigger than one page, so it must paginate.
  await page.evaluate(() => setLevel("MIX"));
  await page.waitForFunction(() => !!document.getElementById("wl-more-btn"), null, { timeout: 120000 });
  const mix = await page.evaluate(() => ({
    total: wlMatches.length,
    shown: wlShown,
    counter: document.getElementById("list-count").textContent,
    label: document.getElementById("wl-more-btn").textContent,
  }));
  allPass &= ok(
    mix.shown === 3000 && mix.total > 3000,
    "big level paginates",
    `${mix.shown.toLocaleString()} of ${mix.total.toLocaleString()}, counter="${mix.counter}", button="${mix.label}"`
  );

  // Walking 50,000+ words a page at a time is not something a test (or a
  // person) should do, so exercise the paging LOGIC exhaustively at a tiny
  // page size instead: click through to the very end and confirm the final
  // word -- previously unreachable past the old cap -- lands in the DOM.
  const walk = await page.evaluate(async () => {
    WL_PAGE_SIZE = 25;
    renderList("zoo"); // a couple of hundred matches -> several small pages
    const settle = () =>
      new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r)));
    await settle();
    let clicks = 0;
    let btn;
    while ((btn = document.getElementById("wl-more-btn")) && clicks < 50) {
      btn.click();
      await settle();
      clicks++;
    }
    const last = wlMatches[wlMatches.length - 1].word;
    return {
      clicks,
      shown: wlShown,
      total: wlMatches.length,
      moreBtn: !!document.getElementById("wl-more-btn"),
      lastInDom: Array.from(document.querySelectorAll(".wl-word")).some(
        (el) => el.textContent === last
      ),
      last,
      counter: document.getElementById("list-count").textContent,
    };
  });
  allPass &= ok(
    walk.shown === walk.total && !walk.moreBtn && walk.lastInDom,
    "whole list reachable",
    `${walk.clicks} click(s) -> ${walk.shown}/${walk.total}, last word "${walk.last}" in DOM=${walk.lastInDom}, ` +
      `button gone=${!walk.moreBtn}, counter="${walk.counter}"`
  );

  // ---- aria-live on study feedback ----
  const live = {};
  for (const [file, sel] of [
    ["index.html", "#fc-definition"],
    ["quiz.html", "#quiz-feedback"],
    ["wordmorph.html", "#wordmorph-feedback"],
    ["clozetest.html", "#cloze-feedback"],
    ["wordrace.html", "#wordrace-feedback"],
    ["dictation.html", "#dictation-feedback"],
    ["readingcomprehension.html", "#rc-feedback"],
  ]) {
    await page.goto(`${BASE}/${file}`, { waitUntil: "domcontentloaded" });
    live[sel] = await page.evaluate((s) => {
      const el = document.querySelector(s);
      if (!el) return "MISSING";
      return el.getAttribute("aria-live") || el.getAttribute("role") || "none";
    }, sel);
  }
  const announced = Object.values(live).filter((v) => v === "polite" || v === "status");
  allPass &= ok(
    announced.length === Object.keys(live).length,
    "feedback announced",
    `${announced.length}/${Object.keys(live).length} regions: ` +
      Object.entries(live)
        .map(([k, v]) => `${k}=${v}`)
        .join(" ")
  );

  // ---- Word Morph footer count ----
  await page.goto(`${BASE}/wordmorph.html`, { waitUntil: "networkidle" });
  await page.waitForFunction(() => typeof WORDS !== "undefined" && WORDS.length > 0);
  await page.waitForFunction(() => (window.SYN_ANT_EN || []).length > 0);
  await page.evaluate(() => {
    setLevel("B2");
    refreshWordMorphStart();
  });
  const counts = await page.evaluate(() => ({
    footer: parseInt(document.getElementById("word-total").textContent.replace(/\D/g, ""), 10),
    ownPool: wmFilteredPool().length,
    cefr: WORDS.length,
  }));
  allPass &= ok(
    counts.footer === counts.ownPool && counts.footer !== counts.cefr,
    "morph count is own pool",
    `footer=${counts.footer}, own pool=${counts.ownPool}, CEFR total=${counts.cefr}`
  );

  console.log(errors.length ? `\n${errors.length} JS error(s):\n${errors.join("\n")}` : "\nno JS errors");
  await browser.close();
  process.exit(allPass ? 0 : 1);
})();
