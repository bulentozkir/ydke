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

// Throwaway check for the "💡" collapse of the .seo-content prose
// (wireSeoInfoModal in shared.js / moresheet.js + the .is-collapsed rules in
// styles.css). Asserts VISIBILITY, not DOM presence -- the whole point of the
// change is that the markup stays put for crawlers while the app view hides
// it. The collapse applies at EVERY width (it was phone-only originally).
// Same standalone playwright driver as scripts/_diag_shot.js because the
// integrated browser tools are broken in this environment.
//
//   node scripts/_diag_seo_modal.js        (needs a server on UDSP_BASE)
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

(async () => {
  const browser = await chromium.launch({ headless: true, executablePath: findChromium() });

  async function open(viewport, page_) {
    const context = await browser.newContext({
      viewport,
      deviceScaleFactor: 1,
      isMobile: viewport.width <= 720,
      hasTouch: viewport.width <= 720,
    });
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
    await page.goto(BASE + "/" + page_, { waitUntil: "domcontentloaded" });
    await page.waitForTimeout(1200);
    return { context, page };
  }

  // ---- phones: prose collapsed, bulb opens/closes it ----
  for (const p of ["index.html", "quiz.html", "hangman.html", "wordlist.html", "games.html", "listening.html"]) {
    console.log("\n[phone 412x915] " + p);
    const { context, page } = await open({ width: 412, height: 915 }, p);

    check("prose hidden", await page.isVisible(".seo-content"), false);
    check("bulb visible", await page.isVisible(".brand-actions .seo-info-btn"), true);
    check("modal hidden", await page.isVisible(".seo-modal"), false);
    if (p === "games.html") check("game tiles visible", await page.isVisible(".game-tiles"), true);
    if (p === "listening.html") check("resource links visible", await page.isVisible(".links-hub .cert-link"), true);

    await page.click(".seo-info-btn");
    await page.waitForTimeout(350);
    check("modal open", await page.isVisible(".seo-modal"), true);
    check("prose in modal", await page.isVisible(".seo-modal-body > .seo-inner"), true);
    check(
      "exactly one .seo-inner",
      await page.evaluate(() => document.querySelectorAll(".seo-inner").length),
      1
    );

    await page.click(".seo-modal-close");
    await page.waitForTimeout(350);
    check("modal closed", await page.isVisible(".seo-modal"), false);
    check(
      "prose back in .seo-content",
      await page.evaluate(() => !!document.querySelector(".seo-content > .seo-inner")),
      true
    );

    // Escape also closes.
    await page.click(".seo-info-btn");
    await page.waitForTimeout(250);
    await page.keyboard.press("Escape");
    await page.waitForTimeout(250);
    check("escape closes", await page.isVisible(".seo-modal"), false);
    await context.close();
  }

  // ---- phones: info pages must NOT be collapsed ----
  // privacy/terms stay readable without interaction on purpose: they are the
  // footer's legal links and a privacy policy should be visible on load, not
  // hidden behind a button.
  for (const p of ["about.html", "help.html", "privacy.html", "terms.html"]) {
    console.log("\n[phone 412x915] " + p + " (must stay expanded)");
    const { context, page } = await open({ width: 412, height: 915 }, p);
    check("prose still visible", await page.isVisible(".seo-content .seo-inner"), true);
    check("no bulb injected", await page.evaluate(() => !document.querySelector(".seo-info-btn")), true);
    await context.close();
  }

  // ---- desktop: same collapse, and the modal opens/closes there too ----
  for (const p of ["index.html", "quiz.html", "clozetest.html", "games.html", "listening.html"]) {
    console.log("\n[desktop 1280x900] " + p);
    const { context, page } = await open({ width: 1280, height: 900 }, p);
    check("prose hidden", await page.isVisible(".seo-content"), false);
    check("bulb visible", await page.isVisible(".brand-actions .seo-info-btn"), true);
    if (p === "games.html") check("game tiles visible", await page.isVisible(".game-tiles"), true);
    if (p === "listening.html") check("resource links visible", await page.isVisible(".links-hub .cert-link"), true);

    await page.click(".seo-info-btn");
    await page.waitForTimeout(350);
    check("modal open", await page.isVisible(".seo-modal"), true);
    check("prose in modal", await page.isVisible(".seo-modal-body > .seo-inner"), true);
    check(
      "exactly one .seo-inner",
      await page.evaluate(() => document.querySelectorAll(".seo-inner").length),
      1
    );

    await page.keyboard.press("Escape");
    await page.waitForTimeout(350);
    check("escape closes", await page.isVisible(".seo-modal"), false);
    check(
      "prose back in .seo-content",
      await page.evaluate(() => !!document.querySelector(".seo-content > .seo-inner")),
      true
    );
    await context.close();
  }

  // ---- desktop: info pages must NOT be collapsed either ----
  for (const p of ["about.html", "help.html", "privacy.html", "terms.html"]) {
    console.log("\n[desktop 1280x900] " + p + " (must stay expanded)");
    const { context, page } = await open({ width: 1280, height: 900 }, p);
    check("prose still visible", await page.isVisible(".seo-content .seo-inner"), true);
    check("no bulb injected", await page.evaluate(() => !document.querySelector(".seo-info-btn")), true);
    await context.close();
  }
  // ---- the 💡 is a FIFTH icon in .brand-actions: styles.css's <=400px block
  // exists so the full "Yabancı Dil Kelime Ezberleme" wordmark still fits
  // beside them, so guard that it is not being ellipsized again. Desktop
  // widths are in the list too -- the bulb only became visible there when the
  // collapse stopped being phone-only, and the desktop header packs the
  // wordmark, the .bottom-nav toolbar AND the icon row onto one line. ----
  for (const w of [360, 390, 412, 1024, 1280, 1440]) {
    console.log("\n[header " + w + "px] index.html");
    const { context, page } = await open({ width: w, height: 800 }, "index.html");
    check(
      "wordmark not ellipsized",
      await page.evaluate(() => {
        const t = document.querySelector(".brand-title");
        return t.scrollWidth > t.clientWidth;
      }),
      false
    );
    check(
      "header row not overflowing",
      await page.evaluate(() => {
        const h = document.querySelector(".site-header");
        return h.scrollWidth > h.clientWidth + 1;
      }),
      false
    );
    await context.close();
  }

  await browser.close();
  console.log("\n" + (failures ? failures + " FAILURE(S)" : "all checks passed"));
  process.exit(failures ? 1 : 0);
})();
