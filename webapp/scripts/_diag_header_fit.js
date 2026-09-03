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

// Throwaway probe: is the desktop .brand-title ellipsized because the 💡
// button became a FIFTH icon in .brand-actions, or was it already clipped
// before? Measures the same page twice -- once as shipped, once with the bulb
// forced display:none -- so the delta is attributable.
//
//   node scripts/_diag_header_fit.js       (needs a server on UDSP_BASE)
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

(async () => {
  const browser = await chromium.launch({ headless: true, executablePath: findChromium() });
  for (const w of [1024, 1280, 1440, 1680]) {
    const context = await browser.newContext({ viewport: { width: w, height: 900 } });
    await context.addInitScript(() => {
      try {
        localStorage.setItem("udsp_welcomed_v1", "1");
        localStorage.setItem("udsp_intro_seen_v1", "1");
      } catch (e) {}
    });
    const page = await context.newPage();
    await page.goto(BASE + "/index.html", { waitUntil: "domcontentloaded" });
    await page.waitForTimeout(900);
    const out = await page.evaluate(() => {
      const t = document.querySelector(".brand-title");
      const bulb = document.querySelector(".seo-info-btn");
      const now = { scroll: t.scrollWidth, client: t.clientWidth, text: t.textContent.trim() };
      const prev = bulb ? bulb.style.display : null;
      if (bulb) bulb.style.display = "none";
      const without = { scroll: t.scrollWidth, client: t.clientWidth };
      if (bulb) bulb.style.display = prev;
      const acts = document.querySelector(".brand-actions");
      return {
        now,
        without,
        icons: acts ? acts.children.length : 0,
        actsW: acts ? Math.round(acts.getBoundingClientRect().width) : 0,
      };
    });
    console.log(
      "[" + w + "] icons=" + out.icons +
        " actions=" + out.actsW + "px" +
        "  title " + out.now.scroll + "/" + out.now.client +
        (out.now.scroll > out.now.client ? " CLIPPED" : " ok") +
        "   without bulb " + out.without.scroll + "/" + out.without.client +
        (out.without.scroll > out.without.client ? " CLIPPED" : " ok") +
        "   text=" + JSON.stringify(out.now.text)
    );
    await context.close();
  }
  await browser.close();
})();
