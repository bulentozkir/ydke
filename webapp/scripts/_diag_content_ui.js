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
 * Proves the UX-#4 content guards actually hold in a real browser.
 *
 * Measured by scripts/_diag_ui_leaks.py, the corpus carries three defects that
 * used to reach the screen unfiltered:
 *   - 20,240 placeholder examples ("No example sentence available")
 *   -  2,769 synonym-list stubs standing in for a definition (all synantde.js)
 *   -    330 definitions long enough to overflow a phone card
 *
 * This walks the live app to a word exhibiting each defect and asserts the UI
 * handles it, rather than trusting the regexes in isolation -- the whole point
 * of #4 was that the guards existed but nothing called them.
 *
 *   node scripts/_diag_content_ui.js
 */
const fs = require("fs");
const path = require("path");
const { chromium } = require("playwright-core");

const BASE = process.env.UDSP_BASE || "http://127.0.0.1:8902";

// playwright-core 1.62 asks for chromium-1234; only 1223 is in the local
// browser cache, so point at whatever revision is actually present.
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
  console.log(`${pass ? "PASS" : "FAIL"}  ${label.padEnd(26)} ${detail}`);
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
  // shared.js sends first-time visitors to home.html; pretend we've been here.
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

  // ---- Flashcards: placeholder example + over-long definition ----
  await page.goto(`${BASE}/index.html`, { waitUntil: "networkidle" });
  await page.waitForFunction(() => typeof WORDS !== "undefined" && WORDS.length > 0);

  const jump = async (predicate) =>
    page.evaluate((src) => {
      const match = new Function("w", `return (${src})(w);`);
      const i = WORDS.findIndex(match);
      if (i === -1) return null;
      // flashcards.js walks WORDS through fcOrder[fcPos]; pin the deck to the
      // single word we care about.
      fcOrder = [i];
      fcPos = 0;
      flashcard.classList.remove("is-flipped");
      renderFlashcard();
      return WORDS[i].word;
    }, predicate.toString());

  const phWord = await jump((w) => /^No example sentence available/.test(w.example || ""));
  const phBtn = await page.evaluate(() => {
    const b = document.getElementById("fc-example-btn");
    return { hidden: b.hidden, visible: b.offsetParent !== null };
  });
  allPass &= ok(
    phWord && phBtn.hidden && !phBtn.visible,
    "placeholder example",
    `word=${phWord}, Example button hidden=${phBtn.hidden}, on screen=${phBtn.visible}`
  );

  const longWord = await jump((w) => (w.definition || "").split(" - ")[0].length > 200);
  const clampBefore = await page.evaluate(() => {
    const el = document.getElementById("fc-definition");
    const native = el.querySelector(".bi-native");
    return {
      clamped: el.classList.contains("is-clamped"),
      btn: !!el.querySelector(".def-more"),
      shown: native.clientHeight,
      full: native.scrollHeight,
    };
  });
  allPass &= ok(
    clampBefore.clamped && clampBefore.btn && clampBefore.shown < clampBefore.full,
    "long definition clamped",
    `word=${longWord}, ${clampBefore.shown}px shown of ${clampBefore.full}px, control=${clampBefore.btn}`
  );

  // #fc-definition sits on the card's BACK face, so it is only reachable once
  // flipped -- which is also the moment the learner reads it. Clicking the
  // expand control must not flip the card back (shared.js stops propagation).
  await page.click("#flashcard");
  await page.click("#fc-definition .def-more");
  const clampAfter = await page.evaluate(() => {
    const el = document.getElementById("fc-definition");
    const native = el.querySelector(".bi-native");
    return {
      clamped: el.classList.contains("is-clamped"),
      shown: native.clientHeight,
      full: native.scrollHeight,
      flipped: document.getElementById("flashcard").classList.contains("is-flipped"),
    };
  });
  allPass &= ok(
    !clampAfter.clamped && clampAfter.shown >= clampAfter.full && clampAfter.flipped === true,
    "expand shows all",
    `${clampAfter.shown}px of ${clampAfter.full}px, card stayed flipped=${clampAfter.flipped}`
  );

  // ---- Word Morph (German): synonym-list stub labelled honestly ----
  await page.goto(`${BASE}/wordmorph.html`, { waitUntil: "networkidle" });
  await page.evaluate(() => setLang("de"));
  await page.waitForFunction(() => (window.SYN_ANT_DE || []).length > 0);
  const stub = await page.evaluate(() => {
    const pool = window.SYN_ANT_DE;
    const i = pool.findIndex((w) => /^\s*Ähnlich wie\s*:/i.test(w.definition || ""));
    if (i === -1) return null;
    const el = document.getElementById("wordmorph-definition");
    renderWordMorphHint(
      "wordmorph-definition",
      isStubDefinition(pool[i].definition) ? "Similar words" : "Definition",
      pool[i].definition
    );
    return { word: pool[i].word, label: el.querySelector(".hm-answer-label").textContent };
  });
  allPass &= ok(
    stub && stub.label === "Similar words:",
    "synonym stub labelled",
    stub ? `word=${stub.word}, label="${stub.label}"` : "no stub found"
  );

  console.log(errors.length ? `\n${errors.length} JS error(s):\n${errors.join("\n")}` : "\nno JS errors");
  await browser.close();
  process.exit(allPass ? 0 : 1);
})();
