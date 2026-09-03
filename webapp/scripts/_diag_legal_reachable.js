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

// A privacy policy has to be clearly labelled and easily accessible.
// styles.css hides .site-footer below 720px (the app shell is height-locked),
// so on phones the link must be reachable through the More bottom sheet.
// This asserts that on BOTH form factors the privacy policy is one tap away.
const { chromium, devices } = require("playwright-core");

const PAGES = [
  "index.html", "quiz.html", "games.html", "about.html", "help.html",
  "privacy.html", "terms.html", "wordlist.html", "stats.html", "profile.html",
];

(async () => {
  const browser = await chromium.launch({ channel: "msedge" });
  let bad = 0;
  for (const [label, opts] of [
    ["desktop 1280", { viewport: { width: 1280, height: 900 } }],
    ["mobile Pixel 7", devices["Pixel 7"]],
  ]) {
    const ctx = await browser.newContext(opts);
    await ctx.addInitScript(() => {
      try { localStorage.setItem("udsp_welcomed_v1", "1"); } catch (e) {}
    });
    console.log("\n=== " + label + " ===");
    for (const f of PAGES) {
      const page = await ctx.newPage();
      const errors = [];
      page.on("pageerror", (e) => errors.push(String(e.message)));
      await page.goto("http://127.0.0.1:8902/" + f, { waitUntil: "domcontentloaded" });
      await page.waitForTimeout(400);
      let via = "footer";
      let visible = await page
        .locator('.footer-links a[href="privacy.html"]')
        .first()
        .isVisible();
      if (!visible) {
        const btn = page.locator("#more-btn");
        if (await btn.isVisible()) {
          await btn.click();
          await page.waitForTimeout(350);
          visible = await page
            .locator('.more-sheet a[href="privacy.html"]')
            .first()
            .isVisible();
          via = "more-sheet";
        }
      }
      const real = errors.filter((m) => !/ERR_|Failed to fetch|net::/i.test(m));
      const ok = visible && real.length === 0;
      if (!ok) bad++;
      console.log(
        (ok ? "OK   " : "FAIL ") + f.padEnd(18) + " privacy reachable via " + via +
        (real.length ? "  ERR: " + real.join(" | ") : "")
      );
      await page.close();
    }
    await ctx.close();
  }
  await browser.close();
  console.log(bad === 0 ? "\nPRIVACY POLICY REACHABLE EVERYWHERE" : "\n" + bad + " FAILURES");
  process.exit(bad === 0 ? 0 : 1);
})();
