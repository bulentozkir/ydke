# Windows — App Analysis

Analysis of <https://udsp.vercel.app> (source: `qlupala9p/udsp`) as a candidate
for Microsoft Store distribution.

See [../README.md](../README.md) for the shared source-app inventory. This
document covers the **Windows-specific** verdict on each feature and the MSIX
readiness scorecard.

---

## 1. Verdict summary

**The app is a viable MSIX packaged web app, but it needs desktop layout work
that the Android track does not.**

The technical packaging story is simpler than Android's — there is no Digital
Asset Links equivalent, no 12-tester gate, and no address-bar failure mode. The
harder problem is that this is an unambiguously **mobile-first** design being
placed on desktop screens: a bottom navigation bar, `portrait-primary`
orientation, and touch-sized tap targets.

The Windows track can reach the store **considerably faster** than Android
because it has no closed-testing requirement. Consider shipping it first to
validate the packaging approach.

---

## 2. Runtime: WebView2, not Chrome

An MSIX packaged web app runs the site in **WebView2** — the Chromium-based Edge
runtime — hosted in an app window. This is close to, but not identical to,
Chrome:

- Same Blink engine, same JS engine, same service worker implementation.
- **Different TTS voices.** `speechSynthesis` is backed by Windows SAPI /
  Edge voices, not Google TTS. Voice names, quality, and language coverage all
  differ from Android.
- **Different storage partition.** The packaged app gets its own storage,
  isolated from the user's Edge browser profile.
- WebView2 runtime is present on all supported Windows 11 installs and is
  serviced by Microsoft; the MSIX declares it as a dependency.

---

## 3. Per-feature Windows verdict

| Feature | Implementation | Windows verdict | Notes |
| --- | --- | --- | --- |
| Flashcards | DOM + CSS 3D flip | **Works** | Tap-to-flip becomes click. Keyboard Space/Arrow already supported — good for desktop. |
| Quiz | DOM | **Works** | |
| 20 games | DOM + `localStorage` | **Works** | Desktop host uses a readable five-column grid; 1000×800 and 390×844 layouts are verified. |
| "🔊 Listen" | `speechSynthesis` | **Works, different voices** | Edge voices cover EN/DE/FR/IT/ES/PT well. Verify Turkish is not needed for playback. |
| Listening / dictation | Same TTS path | **Works** | Better than Android — desktop voice coverage is more consistent. |
| Progress persistence | `localStorage` | **Works, but isolated** | See §5 — this is the most consequential Windows-specific behaviour. |
| Cloud sync | Firestore | **Works** | |
| Google sign-in | `signInWithPopup` | **Unreliable** | See [03-blockers-and-fixes.md](03-blockers-and-fixes.md) §W3. |
| Offline study | Service worker | **Works** | Same three-lane worker; satisfies Store offline expectations. |
| AdSense | `<head>` script | **Policy risk** | Clearer violation than Android — an MSIX is unambiguously an app. |
| Theme (dark/light) | `data-theme` attr | **Works** | Consider following the Windows system theme on first run. |
| Keyboard navigation | Space to flip, arrows to advance | **Partial** | Already better than most PWAs. Needs an audit for full keyboard reachability of the bottom nav and More sheet. |
| Window resizing | — | **Untested** | Primary risk area. See §4. |

---

## 4. Desktop layout — the main Windows-specific problem

The app's chrome is built for a phone:

- A `.bottom-nav` element sits inside `.brand` in the header, using icon+label
  pill buttons sized for thumbs.
- A `.more-sheet` bottom sheet with a drag handle — a mobile interaction
  pattern that has no desktop equivalent and will look out of place in a
  1280×800 window.
- `.selectors-row` holds four `select-pill` controls that were laid out to wrap
  on narrow screens.
- `site.webmanifest` declares `orientation: portrait-primary`, which is
  meaningless-to-hostile on desktop.

None of this **breaks**, but a reviewer opening a maximised window on a 1080p
monitor will see a phone UI stretched across 1920 px. Microsoft Store policy
does not mandate a specific layout, but poor desktop adaptation is the most
likely source of a quality complaint or low ratings.

**Minimum viable fix:** a `max-width` container on `main.container` plus a
media query above ~1024 px that keeps content centred at a readable measure.
This is a `styles.css` change that also benefits desktop web visitors — it is
not Windows-only work.

**Preferred fix:** at desktop widths, promote the bottom nav to a horizontal top
nav and replace the bottom sheet with a dropdown. Larger change, better result.

---

## 5. Storage isolation — a real user-facing consequence

Unlike the Android TWA, which shares an origin and therefore shares
`localStorage` with mobile Chrome, **the MSIX packaged app gets its own storage
partition**. A user who has been studying at `udsp.vercel.app` in Edge and then
installs the Store app sees:

- streak reset to 0
- zero known words
- zero favourites
- all game best scores gone

This looks like data loss and is a predictable source of one-star reviews.

**Mitigations, in order of preference:**

1. **Make sign-in prominent on first run in the packaged app.** The Firestore
   sync already exists and already does exactly the right thing — the
   `signIn` path adopts an existing cloud profile onto a new device. This
   converts the problem into a feature, but it depends on
   [03-blockers-and-fixes.md](03-blockers-and-fixes.md) §W3 being fixed first.
2. **A first-run notice** in the packaged app explaining that progress is
   per-device unless signed in.
3. Do nothing and absorb the reviews. Not recommended.

This is the single most important Windows-specific product decision, and it is
easy to miss because it is invisible during development on a machine that has
never used the website.

---

## 6. MSIX readiness scorecard

| Requirement | Status | Severity | Detail |
| --- | --- | --- | --- |
| Served over HTTPS | Pass | — | |
| Manifest present and linked | Pass | — | |
| `name` / `short_name` / `description` | Pass | — | |
| `start_url` / `scope` | Pass | — | Both `/`. |
| `display: standalone` | Pass | — | |
| `theme_color` / `background_color` | Pass | — | Drives the app window title bar and splash. |
| **Full PNG tile asset set** | **FAIL** | **Blocker** | Manifest has only `icon.svg`. MSIX requires Square44x44, Square150x150, Wide310x150, StoreLogo and their scale variants, all PNG. |
| **Package identity matches Partner Center** | **Not yet set** | **Blocker** | Publisher, Publisher display name, and Package identity Name must be reserved in Partner Center and baked into the MSIX. |
| Registered service worker | Pass | — | |
| Offline behaviour | Pass | — | Degrades gracefully rather than showing a network error. |
| Privacy policy URL | Pass | — | `privacy.html`. |
| `screenshots` in manifest | Missing | Low | Separate from Store listing screenshots. |
| `orientation` suitable for desktop | Fail | Medium | `portrait-primary`. See §4. |
| Desktop-adapted layout | Fail | Medium | Mobile-first chrome. See §4. |
| Keyboard accessibility | Partial | Medium | Needs an audit; Store accessibility expectations are higher on desktop. |
| Sign-in works in WebView2 | Fail | High | `signInWithPopup`. |
| Ads declared consistently | Pending | High | Depends on the ad-gating fix. |

---

## 7. Window sizing

The MSIX manifest can declare an initial window size. The app's content is
comfortable at roughly **480–900 px wide**; forcing a 1920 px window would
expose the layout weaknesses in §4 immediately.

Recommendation: launch at approximately **1000×800**, with a sensible minimum
(around 360×640) so the responsive layout still has somewhere to go. Set this
deliberately rather than accepting the packager default.

---

## 8. What to verify before trusting this document

1. Run the **PWABuilder report card** at <https://www.pwabuilder.com> against
   <https://udsp.vercel.app> and record the Windows-specific findings here.
2. Open the live site in Edge at 1280×800, 1920×1080, and 2560×1440 and
   screenshot the flashcards, games grid, and More sheet. Confirm or refute §4.
3. Enumerate available voices in Edge —
   `speechSynthesis.getVoices().map(v => v.lang + " " + v.name)` — and confirm
   coverage for `en`, `de`, `fr`, `it`, `es`, `pt`.
4. Confirm the storage-isolation claim in §5 empirically: build a sideloadable
   MSIX, study a few cards on the website first, then launch the packaged app
   and check whether `udsp_streak_v1` is present.
5. Tab through the app end to end and note anything unreachable by keyboard.
