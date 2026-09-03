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

// Throwaway visual check: screenshots a page (optionally after clicking
// something) so layout changes can be eyeballed. The integrated browser tools
// are broken in this environment, hence the standalone playwright driver --
// same findChromium() reason as scripts/_diag_perf.js.
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
  const page_ = process.argv[2] || "index.html";
  const out = process.argv[3] || "shot.png";
  const clicks = (process.argv[4] || "").split(",").filter(Boolean);
  const select = process.argv[5] || "";
  // Optional "1280x900"; defaults to the Pixel-ish phone this was written for.
  const vp = /^(\d+)x(\d+)$/.exec(process.argv[6] || "");
  const viewport = vp
    ? { width: Number(vp[1]), height: Number(vp[2]) }
    : { width: 412, height: 915 };
  const phone = viewport.width <= 720;

  const browser = await chromium.launch({
    headless: true,
    executablePath: findChromium(),
  });
  const context = await browser.newContext({
    viewport,
    deviceScaleFactor: phone ? 2 : 1,
    isMobile: phone,
    hasTouch: phone,
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
  await page.waitForTimeout(2500);
  if (select) {
    const [sel, val] = select.split("=");
    await page.selectOption(sel, val);
    await page.waitForTimeout(600);
  }
  for (const c of clicks) {
    await page.click(c);
    await page.waitForTimeout(500);
  }
  await page.screenshot({ path: out, fullPage: false });
  console.log("wrote " + out);
  await browser.close();
})();
