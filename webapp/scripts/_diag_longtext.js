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

// Throwaway audit: where is there still a WALL OF TEXT on each page once the
// 💡 collapse has run? Walks the live DOM and reports every visible block that
// holds a lot of prose, grouped by whether it sits inside .seo-content (the
// bit the bulb already hides) or somewhere else (which the bulb does NOT
// cover, so it needs its own decision).
//
//   node scripts/_diag_longtext.js          (needs a server on UDSP_BASE)
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
const PAGES = fs
  .readdirSync(path.join(__dirname, ".."))
  .filter((f) => f.endsWith(".html"))
  .sort();

(async () => {
  const browser = await chromium.launch({ headless: true, executablePath: findChromium() });
  for (const vp of [
    { name: "desktop", width: 1280, height: 900 },
    { name: "phone", width: 412, height: 915 },
  ]) {
    console.log("\n================ " + vp.name + " " + vp.width + "x" + vp.height + " ================");
    const context = await browser.newContext({
      viewport: { width: vp.width, height: vp.height },
      isMobile: vp.width <= 720,
      hasTouch: vp.width <= 720,
    });
    await context.addInitScript(() => {
      try {
        localStorage.setItem("udsp_welcomed_v1", "1");
        localStorage.setItem("udsp_intro_seen_v1", "1");
      } catch (e) {}
    });
    for (const f of PAGES) {
      const page = await context.newPage();
      await page.goto(BASE + "/" + f, { waitUntil: "domcontentloaded" });
      await page.waitForTimeout(900);
      const url = page.url();
      const res = await page.evaluate(() => {
        // A "wall of text" = a paragraph-ish element with enough words that a
        // user has to read rather than scan. 25 words is roughly two lines on
        // a phone.
        const MIN = 25;
        const out = [];
        const seen = new Set();
        for (const el of document.querySelectorAll("p, li, dd, blockquote")) {
          const r = el.getBoundingClientRect();
          const cs = getComputedStyle(el);
          if (cs.display === "none" || cs.visibility === "hidden" || (!r.width && !r.height)) continue;
          const words = (el.innerText || "").trim().split(/\s+/).filter(Boolean).length;
          if (words < MIN) continue;
          // Attribute it to its nearest interesting ancestor.
          let host = el.closest(".seo-content, .more-sheet, .modal, .intro-card, main, body");
          const label = host
            ? host.tagName.toLowerCase() + (host.className ? "." + String(host.className).split(/\s+/)[0] : "")
            : "?";
          const key = label;
          const prev = seen.has(key) ? out.find((o) => o.where === key) : null;
          if (prev) {
            prev.blocks++;
            prev.words += words;
          } else {
            seen.add(key);
            out.push({ where: key, blocks: 1, words });
          }
        }
        return out;
      });
      const redirected = !url.endsWith("/" + f);
      const line = res.length
        ? res.map((r) => r.where + " " + r.words + "w/" + r.blocks + "blk").join("   ")
        : "(none)";
      console.log(
        (res.length ? "TEXT " : "  ok ") + f.padEnd(26) + (redirected ? "[REDIRECTED] " : "") + line
      );
      await page.close();
    }
    await context.close();
  }
  await browser.close();
})();
