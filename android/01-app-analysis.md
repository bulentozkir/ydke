# Android — App Analysis

Analysis of <https://udsp.vercel.app> (source: `qlupala9p/udsp`) as a candidate
for Google Play distribution.

See [../README.md](../README.md) for the shared source-app inventory. This
document covers the **Android-specific** verdict on each feature and the Trusted
Web Activity readiness scorecard.

---

## 1. Verdict summary

**The app is a good TWA candidate.** It is a real PWA with 28 distinct study
screens, a working offline mode, and a service worker that was clearly written
with intent rather than copy-pasted. It is not a thin content wrapper, which is
the main thing Play's minimum-functionality policy screens for.

Four blockers stand between the current state and a submittable build. Three are
mechanical (icons, asset links, ad gating); one requires a real code change
(auth). None require rewriting the app.

---

## 2. Per-feature Android verdict

A TWA renders the site in **Chrome** (via `CustomTabsSession`), not in a
`WebView`. Web platform behaviour is therefore identical to the user's Chrome
browser, including origin trials, storage quotas, and codec support. That single
fact resolves most of the compatibility questions below.

| Feature | Implementation | Android verdict | Notes |
| --- | --- | --- | --- |
| Flashcards (`index.html`) | DOM + CSS 3D flip | **Works** | Tap-to-flip is already touch-first. |
| Quiz (`quiz.html`) | DOM | **Works** | |
| 12 games (hangman, matrix, memory, scramble, sentencescramble, speedround, survival, truefalse, wordmorph, wordrace, clozetest, dictation) | DOM + `localStorage` best scores | **Works** | Several already carry `mobile` commits, suggesting touch layouts were addressed. |
| "🔊 Listen" pronunciation | Web Speech API (`speechSynthesis`) | **Works, with a caveat** | Chrome on Android exposes Google TTS voices. Voice availability for `it`/`pt` varies by device and by which Google TTS language packs the user has downloaded. Degrade gracefully; do not assume a voice exists. |
| Listening / dictation modes | Same TTS path | **Works, same caveat** | These modes are *harder blocked* by a missing voice than the optional Listen button. Consider a pre-flight voice check. |
| Progress persistence | `localStorage` (`udsp_*`) | **Works** | Shares the origin's storage with mobile Chrome — see §4. |
| Cloud sync | Firestore, `users/{uid}` | **Works** | Firestore's HTTP/WebChannel transport is fine in Chrome. |
| Google sign-in | `signInWithPopup` | **BREAKS** | See [03-blockers-and-fixes.md](03-blockers-and-fixes.md) §B3. |
| Offline study | Service worker, 3 cache lanes | **Works — and is a review asset** | Explicitly cite this when applying for production access. |
| AdSense | `<head>` script, all pages | **Policy risk** | See §5 and [03-blockers-and-fixes.md](03-blockers-and-fixes.md) §B4. |
| Theme (dark/light) | `data-theme` attr + `theme-color` meta | **Works** | The pre-paint inline script avoids a flash. `theme_color` also drives the TWA splash and status bar. |
| Deep links between pages | Relative `.html` hrefs, Vercel `cleanUrls` | **Works** | All within `scope: "/"`, so navigations stay inside the TWA. |
| External "Details ↗" / "Examples ↗" links | `target="_blank"` to third-party dictionaries | **Leaves the app** | Opens a Custom Tab. This is correct and expected behaviour, but see §6. |

---

## 3. TWA readiness scorecard

Requirements for a Trusted Web Activity that launches **without a browser
address bar**.

| Requirement | Status | Severity | Detail |
| --- | --- | --- | --- |
| Served over HTTPS | Pass | — | Vercel default. |
| Web app manifest present and linked | Pass | — | `<link rel="manifest" href="site.webmanifest">` on all pages. |
| `name` / `short_name` | Pass | — | |
| `start_url` within `scope` | Pass | — | Both `/`. |
| `display: standalone` | Pass | — | |
| `theme_color` / `background_color` | Pass | — | Drives splash screen and status bar. |
| **Maskable PNG icon ≥ 512×512** | **FAIL** | **Blocker** | Manifest contains only `icon.svg`. Bubblewrap cannot generate launcher icons or the splash screen from SVG. |
| **`/.well-known/assetlinks.json`** | **FAIL** | **Blocker** | Returns HTTP 404. Without it the TWA falls back to showing a Custom Tab with a visible URL bar. |
| Registered service worker | Pass | — | Strong three-lane implementation. |
| Offline navigation fallback | Pass | — | `OFFLINE_HTML` plus cached-page fallback. |
| `orientation` suitable for phones **and tablets** | Partial | Medium | `portrait-primary` locks out landscape. Play's tablet quality guidelines expect landscape support on large screens. |
| Manifest `id` | Missing | Low | Recommended for stable app identity across manifest edits. |
| Manifest `screenshots` | Missing | Low | Not required for TWA function, but PWABuilder scores on it and it feeds richer install UI. |
| Manifest `shortcuts` | Missing | Low | Would surface long-press launcher shortcuts (Flashcards / Quiz / Games). |
| Content is not a thin wrapper | Pass | — | 28 screens, offline mode, local progress engine. Strong minimum-functionality case. |
| Privacy policy reachable | Pass | — | `privacy.html`, linked from the footer and the More sheet. |
| In-app account deletion | Pass | — | `deleteAccountAndProfile()` in `firebase-client.js`. |
| Web-accessible account deletion request URL | Missing | Medium | Play requires a **web URL** for deletion requests in addition to the in-app path. |

---

## 4. Storage and identity consequences

A TWA runs on the **same origin** as the website, in the same Chrome profile.
Practical effects:

- **Progress carries over automatically.** A user who already studied at
  `udsp.vercel.app` in Chrome opens the installed app and finds their streak,
  known words, and favourites intact. No migration code needed. This is a real
  advantage over Capacitor.
- **Clearing Chrome's site data wipes app progress.** Worth a line in the store
  listing FAQ or help page. The existing Firestore sync is the mitigation, which
  strengthens the argument for making sign-in prominent.
- **Changing the origin breaks this.** If a custom domain is adopted *after*
  launch, existing installs point at the old origin and users appear to lose
  progress. This is the strongest argument for decision **D3** in the shared
  decision log: register the domain **before** the first release.

---

## 5. Advertising posture

AdSense for Content is a product for **websites**. Google's app monetisation
product is **AdMob**; AdSense for Mobile Apps was retired years ago and app
inventory is expected to run through AdMob.

A TWA is a genuine grey area — the content is served in Chrome, on the web, from
a URL the AdSense account already owns — but it is distributed and monetised as
an app. The downside is asymmetric:

- Upside of keeping AdSense in the TWA: incremental ad revenue from app users.
- Downside: an AdSense policy action does not scope itself to the app. It can
  affect the **whole publisher account**, including the website that is
  presumably the main revenue source today.

Given that asymmetry, decision **D2** is to **detect the app context and skip
loading the AdSense script**. Revenue from the app can be revisited via AdMob
once the app is live and its traffic is worth the integration.

There is also a review-surface consequence: declaring "contains ads" in Play
Console when the packaged build shows none is a mismatch, and declaring "no ads"
while shipping AdSense is worse. Gating the script makes the declaration honest.

---

## 6. Navigation and scope notes

- `scope: "/"` means every in-app page stays inside the TWA. Good.
- The `target="_blank"` dictionary links (`fc-link-details`,
  `fc-link-examples`) point at third-party origins and will open a Custom Tab
  overlay. The user returns with the back gesture. This is acceptable, but
  verify the back stack behaves — a TWA that traps the user on a third-party
  page is a review risk.
- Vercel's `cleanUrls: true` means both `/quiz` and `/quiz.html` resolve. The
  service worker already handles this in its navigation fallback
  (`cache.match(pathname + ".html")`). Nothing extra needed for the TWA.
- `X-Frame-Options: SAMEORIGIN` is **not** a problem: a TWA performs a top-level
  navigation, not an iframe embed.

---

## 7. First-run data cost

`data/` is approximately **36 MB**, fetched lazily and then cached
aggressively. A fresh install starts with an empty `udsp-data-v1` cache, so the
first session downloads whichever word lists the user opens.

Implications:

- A Play reviewer, or a user on a metered Turkish mobile connection, may
  experience a slow or expensive first run.
- The pre-launch report runs on real devices and may surface this as slow
  startup.
- Recommended mitigation: an explicit, user-initiated **"download for offline
  use"** action that warms the cache for the selected language, with a clear
  size estimate. This turns an invisible cost into an informed choice and reads
  well in a store listing.

This is a quality issue, not a blocker.

---

## 8. What to verify before trusting this document

This analysis was produced by reading the repository and probing the live site.
Confirm against tooling before acting on it:

1. Run **Lighthouse** (Installability / PWA audit) against
   <https://udsp.vercel.app> and record the actual findings here.
2. Run the **PWABuilder report card** at <https://www.pwabuilder.com> for the
   same URL; it emits a distinct Android readiness score.
3. Confirm the asset links 404 still stands:
   `curl -i https://udsp.vercel.app/.well-known/assetlinks.json`
4. Confirm TTS voice availability for `it` and `pt` on at least one real device
   before relying on the listening and dictation modes in store screenshots.
