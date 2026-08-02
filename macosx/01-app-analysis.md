# macOS — App Analysis

Analysis of <https://udsp.vercel.app> (source: `qlupala9p/udsp`) as a candidate
for macOS distribution.

See [../README.md](../README.md) for the shared source-app inventory. This
document covers the **macOS-specific** verdict on each feature and the Mac App
Store readiness scorecard.

---

## 1. Verdict summary

**Technically the easiest port of the four. Editorially the hardest.**

The engine is excellent, the voices are the best of any platform, the layout fix
carries over unchanged, and the shared Tauri host with the Linux track means
almost no new code. If the only question were "does it run well," macOS would be
the strongest target.

The question is not that. macOS is the only platform in this project with a
**human reviewer applying an editorial standard**, and that standard is aimed
squarely at this class of app:

> Your app should include features, content, and UI that elevate it beyond a
> repackaged website. If your app is not particularly useful, unique, or
> "app-like," it doesn't belong on the App Store.

Two further guidelines compound it. **4.8** requires Sign in with Apple wherever
Google Sign-In is offered — the app is Google-only. **2.4.5** forbids a Mac App
Store app from downloading code or resources that add functionality, and
requires updates to ship through the store, which makes a thin remote-URL
wrapper untenable.

The consequence drives the whole track: **there are two macOS products, not
one.** A directly-distributed notarized app that loads the live site is
straightforward and can ship soon. A Mac App Store app must bundle its content
locally, add Sign in with Apple, and make a genuine case under 4.2. See
[02-packaging-strategy.md](02-packaging-strategy.md).

---

## 2. Runtime: WKWebView

A Tauri app on macOS renders in **`WKWebView`** — the same engine as Safari, and
the same engine *family* as the Linux track's WebKitGTK.

| Property | Windows (WebView2) | Linux (WebKitGTK) | macOS (WKWebView) |
| --- | --- | --- | --- |
| Engine | Blink | WebKit | **WebKit** |
| JS engine | V8 | JavaScriptCore | **JavaScriptCore** |
| Updated by | Microsoft | The distro | **macOS updates** |
| Version you get | Recent | Whatever is installed | **Tied to the OS version** |
| TTS backend | Windows SAPI | Flite / speech-dispatcher, often absent | **`AVSpeechSynthesizer` — best of the four** |

**This is the same engine family as Linux**, which is the single most useful fact
in this document. Anything verified in WebKitGTK almost certainly behaves the
same in `WKWebView` and vice versa. Test WebKit once and both tracks benefit —
including the `:has()` question raised in
[../linux/03-blockers-and-fixes.md](../linux/03-blockers-and-fixes.md) §L8,
though macOS is far less exposed to it because the engine ships with the OS
rather than with the distribution.

### Practical characteristics

- **Text-to-speech is genuinely good.** `speechSynthesis` is backed by
  `AVSpeechSynthesizer` with system voices covering English, German, French,
  Italian, Spanish, and Portuguese out of the box. This is the opposite of the
  Linux situation. One caveat: the default compact voices are noticeably
  robotic, and the high-quality variants are an **opt-in download** under
  System Settings → Accessibility → Spoken Content. The app should not assume
  the good voices are present.
- **Storage is persistent by default** under `~/Library/WebKit/<bundle-id>/`,
  or inside `~/Library/Containers/<bundle-id>/` when sandboxed. Confirm the
  host uses the persistent data store, not an ephemeral one — an ephemeral
  store silently discards `localStorage` on every quit, which would destroy all
  progress and look like a catastrophic bug.
- **Service workers do not register on custom schemes.** Loading bundled
  content from `tauri://localhost` disables `sw.js` entirely. This matters
  because §M7 pushes the Mac App Store build toward exactly that. It is
  survivable — the service worker is purely a cache layer, and bundled content
  needs no cache — but the `/data/*` 24-hour refresh logic becomes dead code,
  and offline behaviour must be re-verified rather than assumed.
- **Intelligent Tracking Prevention** applies. It is not a concern for
  first-party `localStorage` on a site users deliberately install, but it is
  worth confirming empirically rather than assuming — see §8.

---

## 3. Per-feature macOS verdict

| Feature | Implementation | macOS verdict | Notes |
| --- | --- | --- | --- |
| Flashcards | DOM + CSS 3D flip | **Works** | Space and arrow keys already bound. |
| Quiz | DOM | **Works** | |
| 12 games | DOM + `localStorage` | **Works** | Layout handled by the shared `bootstrap.js` fix. |
| "🔊 Listen" | `speechSynthesis` | **Works well** | Best voice coverage of any track. See §2. |
| Listening / dictation | Same TTS path | **Works well** | No Linux-style silent failure. |
| Progress persistence | `localStorage` | **Works, but isolated** | See §5. |
| Cloud sync | Firestore | **Works** | |
| Google sign-in | `signInWithPopup` | **Broken** | Plus a policy problem, not just a technical one — see §4 and [03-blockers-and-fixes.md](03-blockers-and-fixes.md) §M1. |
| Offline study | Service worker | **Works over HTTPS; disabled if bundled** | See §2. |
| AdSense | `<head>` script | **Policy risk, escalated** | Apple reviews this directly. Guideline 4.2.2 names advertisements explicitly. |
| Theme (dark/light) | `data-theme` attr | **Works** | Should follow the system appearance and respond to live changes. |
| Desktop layout | `styles.css` | **Fixed** | `bootstrap.js` ports verbatim. |
| Menu bar | — | **Missing** | A macOS app without a real menu bar reads as non-native immediately. See §7. |
| Keyboard shortcuts | Space / arrows | **Partial** | No ⌘ accelerators at all. |
| Window restoration | — | **Missing** | Expected macOS behaviour. |

---

## 4. Guideline 4.2 — "beyond a repackaged website"

This is the defining constraint of the macOS track and the reason it is
sequenced last.

Apple's App Review Guidelines state, in **4.2 Minimum Functionality**:

> Your app should include features, content, and UI that elevate it beyond a
> repackaged website. If your app is not particularly useful, unique, or
> "app-like," it doesn't belong on the App Store.

and in **4.2.2**:

> Other than catalogs, apps shouldn't primarily be marketing materials,
> advertisements, web clippings, content aggregators, or a collection of links.

### What helps

The app is **not** a content aggregator or a collection of links, which is what
4.2.2 is principally aimed at. It has:

- 28 distinct interactive study screens and 12 games
- a real spaced-repetition engine with persistent per-user state
- offline capability via an existing, well-built service worker
- ~36 MB of curated vocabulary data

That is a substantive application by any reasonable reading. A reviewer
encountering it is not looking at a bookmark.

### What hurts

- Every page loads **AdSense**, and 4.2.2 names advertisements explicitly.
- The UI is unmistakably mobile-first in origin.
- With no menu bar, no ⌘ shortcuts, and no window restoration, it *reads* as a
  web page in a frame even though it is not.
- **2.4.5(iv)** — a Mac App Store app "may not download or install standalone
  apps, kexts, additional code, or resources to add functionality or
  significantly change the app from what we see during the review process" —
  and **2.4.5(vii)**, "they must use the Mac App Store to distribute updates;
  other update mechanisms are not allowed." A wrapper that fetches its entire
  UI and logic from a web server at launch is in obvious tension with both.

### The consequence

For the **Mac App Store**, the app must bundle its content locally and add real
native capability. For **direct distribution**, none of this applies — Apple
notarizes for malware, not for editorial quality, and 4.2 is never evaluated.

That asymmetry is why [02-packaging-strategy.md](02-packaging-strategy.md)
recommends shipping the direct build first.

### Native additions that answer 4.2

Each is also a genuine product improvement, and each is shared with the Linux
track's Flathub answer — build once:

| Addition | Why it counts | Also fixes |
| --- | --- | --- |
| Full macOS menu bar with ⌘ accelerators | The clearest single signal of a native app | Nothing — new capability |
| Content bundled in-app, fully offline | Directly answers 2.4.5(iv) and (vii) | §M7 |
| Native TTS via `AVSpeechSynthesizer` | Real system integration, better voices | Linux §L5 |
| Notification Centre streak reminders | Native capability the web cannot match | New capability |
| Dock badge showing daily progress | Visible, macOS-specific | New capability |
| Native export/import via `NSSavePanel` | Real file system integration | No export path exists today |
| Window restoration and state preservation | Expected macOS behaviour | §M10 |
| Follows system appearance live | Expected macOS behaviour | §M10 |

---

## 5. Storage isolation — a real user-facing consequence

Identical in shape to the Windows and Linux tracks. The packaged app has its
own `WKWebView` data store under `~/Library/WebKit/<bundle-id>/`, separate from
Safari. A user arriving from the website sees a zeroed streak, no known words,
no favourites, and no game bests.

Under the App Sandbox — mandatory for the Mac App Store per **2.4.5(i)** — the
data moves inside `~/Library/Containers/<bundle-id>/` and is removed with the
app.

**Mitigation is the same** and depends on the same prerequisite: make sign-in
prominent on first run so the existing Firestore sync adopts the cloud profile.
On macOS that path is blocked twice over — `signInWithPopup` does not work in
the host (§M6), *and* offering Google sign-in at all triggers the Sign in with
Apple requirement (§M1).

`bootstrap.js` already contains the first-run notice written for the Windows
track and needs no change.

---

## 6. Mac App Store readiness scorecard

| Requirement | Status | Severity | Detail |
| --- | --- | --- | --- |
| Served over HTTPS | Pass | — | |
| Privacy policy URL | Pass | — | `privacy.html`. |
| **Account deletion in-app** | **Pass** | — | `deleteAccountAndProfile()` already exists. Satisfies **5.1.1(v)**, which many apps fail. |
| Offline behaviour | Pass | — | Service worker, or bundled content. |
| **Sign in with Apple** | **FAIL** | **Blocker** | **4.8**. Google is the only provider. |
| **4.2 minimum functionality** | **At risk** | **Blocker** | See §4. |
| **`.icns` app icon** | **FAIL** | **Blocker** | Manifest ships only `icon.svg`. Shared with W1 / B1 / L2. |
| **Apple Developer Program membership** | **Not held** | **Blocker** | $99/yr. Required for both signing and notarization. |
| **Ads absent from packaged build** | Fail | **Blocker** | 4.2.2 names advertisements explicitly. |
| **App Sandbox enabled** | Not set | **Blocker for MAS** | **2.4.5(i)**. Not required for direct distribution. |
| Hardened Runtime enabled | Not set | Blocker | Required for notarization. |
| Content bundled rather than fetched | Fail | High | **2.4.5(iv)**, **2.4.5(vii)**. MAS only. |
| Working sign-in in `WKWebView` | Fail | High | `signInWithPopup`. |
| Download-size disclosure | Missing | Medium | **4.2.3(ii)** — the ~36 MB of word data must be disclosed and prompted for if downloaded after launch. |
| Native menu bar and ⌘ shortcuts | Missing | Medium | Strongly influences the 4.2 judgement. |
| Window state restoration | Missing | Medium | Expected macOS behaviour. |
| `PrivacyInfo.xcprivacy` | Missing | Medium | Privacy manifest; required for required-reason API use. |
| App Store privacy labels | Not filled | Medium | Must match the Play Data Safety answers. |
| Universal binary (arm64 + x86_64) | Not built | Medium | Apple Silicon is the majority of the installed base. |
| Desktop-adapted layout | **Pass** | — | Fixed in `bootstrap.js`. |
| Runs on the current shipping OS | Untested | Medium | **2.4.5(viii)**. |
| Keyboard accessibility | Partial | Medium | Unaudited, same as the other tracks. |

---

## 7. Window sizing and macOS conventions

Adopt the same **1000×800** default and ~360×640 minimum established by the
Windows track. The layout fix behaves identically.

Beyond sizing, macOS has a set of conventions users notice by their absence.
None is individually large; collectively they are what separates an app from a
web page in a frame — which is precisely the 4.2 judgement.

| Convention | Expected behaviour |
| --- | --- |
| Menu bar | Real menus: Top Words, File, Edit, View, Window, Help |
| ⌘W / ⌘Q | Close window / quit — distinct actions, not the same one |
| ⌘, | Opens settings |
| ⌘1…⌘5 | Jump to the primary sections |
| ⌘F | Focus search on `wordlist.html` |
| Window restoration | Size, position, and current view survive a relaunch |
| Full-screen | Native green-button behaviour, not a maximised window |
| Appearance | Follows system light/dark **and responds to live changes** |
| Edit menu | Cut/Copy/Paste/Select All wired to the web view |

The Edit menu is the one most often forgotten. Without it, ⌘C does nothing
anywhere in the app, which users notice within minutes.

---

## 8. What to verify before trusting this document

None of these can be checked from Windows; all need Mac hardware or a macOS CI
runner.

1. **The 4.2 judgement itself is the largest unknown in this repository.** It
   is a human decision, not a checklist. Before investing in the Mac App Store
   path, consider submitting the direct build first and using **App Review
   Support** or a pre-review enquiry to test the reading. Everything in §4 is
   an informed prediction, not a guarantee.
2. **Voice coverage and quality.** Run
   `speechSynthesis.getVoices().map(v => v.lang + " " + v.name)` on a clean
   macOS install — one that has *not* had enhanced voices downloaded — and
   confirm en/de/fr/it/es/pt are all present and intelligible.
3. **Persistent storage.** Confirm the host uses a persistent `WKWebView` data
   store: study a few cards, quit fully, relaunch, and check `udsp_streak_v1`
   survives. An ephemeral store would look like total data loss.
4. **Service worker registration** under both loading strategies. Over HTTPS it
   should register; from `tauri://localhost` it will not. Confirm nothing
   depends on it beyond caching.
5. **Bundled-content viability.** Build with all ~36 MB of `data/` in the
   bundle and measure the resulting `.app` size, launch time, and memory. This
   decides whether §M7 is comfortable or painful.
6. **Sign in with Apple end to end** once §M1 is implemented — including the
   private-relay email path, which is the part most likely to break the
   Firestore profile logic.
7. **Universal binary** on both Apple Silicon and Intel. Do not assume the
   x86_64 slice works because the arm64 one does.
8. **Notarization on a clean machine.** Download the signed `.dmg` through a
   browser so it carries the quarantine attribute, then open it. This is the
   only way to test the real Gatekeeper path; copying the file over the network
   or via `scp` does not reproduce it.
