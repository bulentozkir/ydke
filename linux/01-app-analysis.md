# Linux — App Analysis

Analysis of <https://udsp.vercel.app> (source: `qlupala9p/udsp`) as a candidate
for Linux desktop distribution.

See [../README.md](../README.md) for the shared source-app inventory. This
document covers the **Linux-specific** verdict on each feature and the desktop
integration scorecard.

---

## 1. Verdict summary

**The app ports to Linux more easily than to Windows, with one serious
exception: text-to-speech is likely to be silent out of the box.**

The layout problem that dominated the Windows track is already solved — the fix
lives in `bootstrap.js` as platform-agnostic JavaScript and carries over
untouched. Storage isolation behaves the same way. Icons and manifest gaps are
the same shared work.

What is genuinely new on Linux is the runtime. **WebKitGTK is not Chromium.** It
is the GTK port of WebKit, so its behaviour tracks Safari rather than Chrome, and
its version is dictated by whichever distribution the user happens to run. Two
consequences dominate this document:

1. **`speechSynthesis` commonly exposes zero voices.** The Listen button,
   `dictation.html`, and `listening.html` are core features that degrade to
   nothing without a fix. This is the headline Linux finding.
2. **The engine version is not yours to choose.** A feature that works on
   Fedora 40 may not parse on a Debian 11 base. This directly affects the
   `:has()` selector the current layout fix depends on.

The distribution story is the other half of the problem, and it is unusually
political. Both gatekept Linux stores object to web wrappers on principle — see
[02-packaging-strategy.md](02-packaging-strategy.md). The recommendation is
therefore to lead with an **ungated channel** and treat Flathub as optional.

---

## 2. Runtime: WebKitGTK, not Chromium

A Tauri app on Linux renders in **WebKitGTK** (`webkit2gtk-4.1`), the GTK port
of WebKit. This is the same engine family as macOS `WKWebView` and Safari — and
a different family from Android Chrome and Windows WebView2, both of which are
Blink.

| Property | Android (TWA) | Windows (WebView2) | Linux (WebKitGTK) |
| --- | --- | --- | --- |
| Engine | Blink | Blink | **WebKit** |
| JS engine | V8 | V8 | **JavaScriptCore** |
| Updated by | Google Play | Microsoft | **The distro** |
| Version you get | Recent | Recent | **Whatever is installed** |
| TTS backend | Google TTS | Windows SAPI / Edge | **Flite / speech-dispatcher, often absent** |

The version point is the one that bites. WebView2 is evergreen and serviced by
Microsoft; WebKitGTK is a distro package. A user on a conservative LTS release
can be running an engine two or three years behind, and there is no mechanism to
push them forward. **AppImage sidesteps this** by bundling its own WebKitGTK —
one of the strongest arguments for that format, and a point in its favour that
has nothing to do with store policy.

### Practical differences from Chromium

- **Same engine as the macOS track.** Anything verified in WKWebView is very
  likely to behave identically here. Test WebKit once, benefit twice.
- **Media codecs come from GStreamer**, not from the browser. Codec coverage
  depends on which `gst-plugins-*` packages the user has. The app does not play
  video, so this is currently irrelevant — note it only because it becomes
  relevant the moment audio or video content is added.
- **Service workers do not register on non-HTTP schemes.** Loading the site
  from a custom `tauri://` or `file://` origin silently disables `sw.js`. This
  does not matter while the app loads the live site over HTTPS, but it becomes
  a design constraint if the content is ever bundled locally — see
  [../macosx/03-blockers-and-fixes.md](../macosx/03-blockers-and-fixes.md) §M7,
  where the macOS track is forced down exactly that path.
- **`localStorage` works normally** and persists across launches, provided the
  app uses a persistent data directory rather than an ephemeral one.

---

## 3. Per-feature Linux verdict

| Feature | Implementation | Linux verdict | Notes |
| --- | --- | --- | --- |
| Flashcards | DOM + CSS 3D flip | **Works** | Click to flip; Space and arrow keys already bound. |
| Quiz | DOM | **Works** | |
| 20 games | DOM + `localStorage` | **Works** | Layout already handled by the shared `bootstrap.js` fix. |
| "🔊 Listen" | `speechSynthesis` | **Likely silent** | The main Linux defect. See §4. |
| Listening / dictation | Same TTS path | **Likely silent** | These two pages are *entirely* TTS-driven — they do not degrade, they stop working. |
| Progress persistence | `localStorage` | **Works, but isolated** | Same consequence as Windows. See §5. |
| Cloud sync | Firestore | **Works** | Plain HTTPS + WebSockets; no platform dependency. |
| Google sign-in | `signInWithPopup` | **Broken** | `window.open` has no handler in a bare WebKitGTK host. See [03-blockers-and-fixes.md](03-blockers-and-fixes.md) §L4. |
| Offline study | Service worker | **Works** | Registers normally over HTTPS. |
| AdSense | `<head>` script | **Policy risk** | Same account-level risk as the other tracks. |
| Theme (dark/light) | `data-theme` attr | **Works** | Should follow the desktop colour scheme via the `org.freedesktop.appearance` portal. |
| Desktop layout | `styles.css` | **Fixed** | `bootstrap.js` ports verbatim — see §7. |
| Keyboard navigation | Space / arrows | **Partial** | Same unaudited state as the Windows track. |
| App menu / shortcuts | — | **Missing** | No `.desktop` entry, no accelerators. See §6. |

---

## 4. Text-to-speech — the main Linux-specific problem

The app calls `speechSynthesis.speak()` for the Listen button and drives two
entire study modes from it. On Windows and macOS this is backed by a rich,
pre-installed voice set. On Linux there is frequently **no voice at all**.

WebKitGTK's `SpeechSynthesis` implementation is a compile-time option backed by
Flite or `speech-dispatcher`. Distributions differ in whether they enable it,
and even where the API is present, `speechSynthesis.getVoices()` can return an
empty array because no voice packages are installed. The failure mode is the
worst kind: **`speak()` resolves without error and nothing is audible.** There
is no exception to catch and no console warning.

Inside a Flatpak or Snap sandbox the problem compounds — the app must also be
granted access to the audio server and, if `speech-dispatcher` is used, to its
socket on the session bus.

**This is not a polish item.** `listening.html` and `dictation.html` have no
non-audio fallback; a user launching them with no voices sees a study screen
that never says anything.

**Recommended fix: bridge to a native TTS service from the host process** rather
than relying on the web API. The host exposes a command the injected script
calls in place of `speechSynthesis`, and the host talks to `speech-dispatcher`
(which fronts espeak-ng, Festival, Pico, and others) over its native protocol.

This has three benefits beyond fixing the bug:

1. Voice quality and language coverage become a packaging decision rather than
   a lottery — the AppImage can ship a known-good engine.
2. It is exactly the kind of **native integration** that both Flathub and the
   Mac App Store demand from a web-backed app. The same bridge answers the
   anti-wrapper objection on both platforms.
3. It fixes the identical latent risk on macOS, where system voices exist but
   the high-quality ones are an opt-in download.

Details and a fallback ladder are in
[03-blockers-and-fixes.md](03-blockers-and-fixes.md) §L5.

---

## 5. Storage isolation — a real user-facing consequence

Identical in shape to the Windows track. The packaged app gets its own
WebKitGTK data directory under `~/.local/share/<app-id>/`, entirely separate
from the user's Firefox or Chrome profile. A user who has been studying at
`udsp.vercel.app` in a browser and then installs the desktop app sees:

- streak reset to 0
- zero known words
- zero favourites
- all game best scores gone

Under Flatpak the isolation is stronger still — the data directory lives inside
`~/.var/app/<app-id>/`, and is removed when the app is uninstalled with
`--delete-data`.

**Mitigation is the same and depends on the same prerequisite:** make sign-in
prominent on first run so the existing Firestore sync adopts the cloud profile
onto the new device. That path is blocked until
[03-blockers-and-fixes.md](03-blockers-and-fixes.md) §L4 is fixed, because
sign-in itself does not currently work in the host.

A first-run notice is the cheap fallback, and `bootstrap.js` already contains
one written for the Windows track.

---

## 6. Desktop integration scorecard

Linux has no single certification authority, but there is a well-defined set of
freedesktop.org conventions that every distribution, app store, and desktop
environment reads. Failing them does not block a download — it blocks the app
from appearing correctly anywhere.

| Requirement | Status | Severity | Detail |
| --- | --- | --- | --- |
| Served over HTTPS | Pass | — | |
| Registered service worker | Pass | — | Works over HTTPS. |
| Offline behaviour | Pass | — | Three-lane worker degrades gracefully. |
| Privacy policy URL | Pass | — | `privacy.html`. |
| **`.desktop` entry** | **FAIL** | **Blocker** | Without it the app has no launcher entry, no icon in the dock, and no MIME association. Nothing lists it. |
| **AppStream MetaInfo file** | **FAIL** | **Blocker** | Required by Flathub, Snap, GNOME Software, KDE Discover, and every distro software centre. Absent means the app is invisible in graphical package managers. |
| **Icon theme assets (PNG + SVG)** | **FAIL** | **Blocker** | `icon.svg` exists and is a good start, but the hicolor theme wants sized PNGs as well. Shared with Windows W1. |
| **Reverse-DNS application ID** | **Not set** | **Medium** | Needed for the `.desktop` name, MetaInfo `<id>`, D-Bus name, and `StartupWMClass`. Must be a domain or code-host you control. |
| Working sign-in | Fail | Blocker | `signInWithPopup`. |
| Ads absent from packaged build | Fail | Blocker | Shared with W4 / B4. |
| Audible TTS | Fail | High | See §4. |
| OARS content rating | Missing | Medium | Required in MetaInfo by Flathub; also used by Snap and software centres. |
| Screenshots in MetaInfo | Missing | Medium | Software centres show a grey placeholder without them. |
| English UI localisation | Fail | Medium | The app UI is Turkish (`lang: "tr"`). Flathub requires complete English. |
| Desktop-adapted layout | **Pass** | — | Fixed in `bootstrap.js`; see §7. |
| Keyboard accessibility | Partial | Medium | Unaudited, same as the other tracks. |
| Follows system colour scheme | Missing | Low | Read `org.freedesktop.appearance` `color-scheme` and set `data-theme` on first run. |
| Wayland support | Untested | Medium | Must be verified on both Wayland and X11; Wayland is the default on current GNOME and KDE. |

---

## 7. Window sizing and layout — already solved

The W7 defect found and fixed during the Windows build was **not** a Windows
problem. It was a defect in `styles.css` that appears at any viewport wider than
720 px, on any platform, including the desktop website itself.

The site's entire app shell — non-scrolling body, pinned header, internally
scrolling content area, hidden footer — is declared **only** inside
`@media (max-width: 720px)`. Above that width the layout falls back to plain
document flow and the whole page scrolls, dragging the header and navigation
off-screen.

The fix in `windows/src/TopWords.Windows/Scripts/bootstrap.js` re-applies that
shell at `@media (min-width: 721px)`. It is **pure, platform-agnostic
JavaScript** — no WebView2 API, no .NET interop — and it is injected on Linux
exactly the same way, as a document-start script. Measured on Windows at
1000×800, as page height ÷ viewport:

| Page | Before | After |
| --- | ---: | ---: |
| `/` (cards) | 2.21× | 1.00× |
| `/quiz.html` | 4.86× | 1.00× |
| `/about.html` | 4.75× | 1.00× |
| `/help.html` | 17.16× | 1.00× |
| `/wordlist.html` | 451.95× | 1.00× |

Adopt the same **1000×800** default window with a ~360×640 minimum.

> **One caveat specific to WebKitGTK.** The rule that hides crawler-facing SEO
> copy uses `.container:has(.view.is-active) .seo-content`. The `:has()`
> selector needs a reasonably current WebKit; on an older distro build the
> entire rule is dropped as unparseable and the SEO block reappears at the
> bottom of every study page. This is a silent, cosmetic-looking regression
> with an unobvious cause. See [03-blockers-and-fixes.md](03-blockers-and-fixes.md)
> §L8 for the feature-detected fallback.

---

## 8. What to verify before trusting this document

The claims below are the ones most likely to be wrong, ordered by how much
rework a wrong answer causes. All of them need a real Linux machine or
container; none can be checked from Windows.

1. **TTS, first and most important.** On a stock Ubuntu, Fedora, and Debian
   install, open the app and run
   `speechSynthesis.getVoices().map(v => v.lang + " " + v.name)`. Record the
   result per distro. If any returns a non-empty list, confirm audio actually
   plays — the API being present does not mean a voice is installed. This
   single result determines whether §4's native bridge is mandatory or merely
   advisable.
2. **`:has()` support.** Evaluate `CSS.supports('selector(:has(*))')` on the
   oldest WebKitGTK you intend to support. This decides whether §7's caveat is
   theoretical or real.
3. **WebKitGTK versions in the wild.** Check `webkit2gtk-4.1` package versions
   on the current Debian stable, Ubuntu LTS, and Fedora releases to establish
   the actual floor you must support.
4. **Wayland and X11 both.** Launch under each and confirm window sizing,
   HiDPI scaling, and the system tray or indicator behave. Fractional scaling
   on Wayland is a common source of blurry or mis-sized windows.
5. **AppImage portability.** Build one and run it on a distro *older* than the
   build host. Confirm whether `libfuse2` is required — recent Ubuntu releases
   do not install it by default, which produces a confusing "cannot mount
   AppImage" error for the user.
6. **Storage isolation**, empirically: study a few cards in Firefox, then
   launch the packaged app and check whether `udsp_streak_v1` is present.
   Repeat inside a Flatpak to confirm the `~/.var/app` path.
7. **Sandbox audio.** If Flatpak or Snap is pursued, verify TTS *inside* the
   sandbox, not just outside it. Audio and `speech-dispatcher` access are
   separate permissions and are easy to get wrong.
