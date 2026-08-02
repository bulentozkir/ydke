# Android — Packaging Strategy

Decision record for **how** to get `udsp` onto Google Play.

**Decision: Trusted Web Activity (TWA), built with Bubblewrap.**

---

## 1. Options considered

### Option A — Trusted Web Activity (recommended)

A thin Android app whose only job is to launch Chrome in full-screen, verified
mode against a URL you control. Google's officially supported way to publish a
PWA to Play.

**Build tooling:** [Bubblewrap CLI](https://github.com/GoogleChromeLabs/bubblewrap)
(`@bubblewrap/cli`) generates and maintains the Android project.
[PWABuilder](https://www.pwabuilder.com) is a GUI front-end over the same
`android-browser-helper` library; it is easier for a one-shot package, harder to
version-control and re-build.

| Pros | Cons |
| --- | --- |
| One codebase. Web deploy = app update, no store review. | Requires `assetlinks.json` and a stable origin. |
| Runs in **Chrome**, so behaviour matches the web exactly (TTS, service worker, storage quotas). | Requires Chrome or a compatible browser installed. Negligible on modern Android. |
| Shares `localStorage` with the mobile site — existing users keep their progress. | Origin is compiled in; changing domains later breaks that continuity. |
| Tiny APK (~1 MB before icons). | Content is unavailable if the host is down and uncached. |
| Officially blessed by Play; not treated as a "webview wrapper". | Cannot use native Android APIs without extra plumbing. |

### Option B — Capacitor

Bundles the HTML/CSS/JS **inside** the APK and runs it in a `WebView`, with a
native bridge for device APIs.

| Pros | Cons |
| --- | --- |
| True offline from first launch — no 36 MB cold download. | ~36 MB of `data/` must ship in the bundle; every word-list correction needs a store release (or a remote-fetch fallback, which reintroduces the cold-download problem). |
| Enables **AdMob** native ads — the policy-correct app monetisation path. | Forks the build. Two things to keep in sync forever. |
| Enables **native Google Sign-In** (Credential Manager + `signInWithCredential`), cleanly solving the popup problem. | Runs in Android System WebView, not Chrome. TTS voice availability and behaviour differ subtly from Option A. |
| Survives Vercel downtime. | Does **not** inherit existing web users' `localStorage`. Everyone starts from zero unless they sign in. |
| No `assetlinks.json`, no domain dependency. | More review surface: a WebView app is closer to the "repackaged content" pattern Play scrutinises. |

### Option C — Native rewrite (Kotlin / Compose)

Not seriously considered. The app is ~25 JS modules of game logic plus a bespoke
progress engine and 36 MB of data pipelines. Rewriting natively multiplies
maintenance cost with no user-visible benefit for this app class.

### Option D — Plain `WebView` wrapper

Explicitly rejected. This is the pattern Play's minimum-functionality policy
targets, it lacks Digital Asset Links verification, and it inherits none of the
TWA's Chrome-parity guarantees. Strictly worse than both A and B.

---

## 2. Decision matrix

| Criterion | Weight | TWA | Capacitor | Native |
| --- | --- | --- | --- | --- |
| Maintenance cost (one codebase) | High | ✅ Best | ⚠️ Fork | ❌ Full rewrite |
| Time to first submission | High | ✅ Days | ⚠️ Weeks | ❌ Months |
| Existing users keep progress | Medium | ✅ Yes | ❌ No | ❌ No |
| Offline from install | Medium | ⚠️ After first use | ✅ Yes | ✅ Yes |
| Policy-clean ad monetisation | Medium | ❌ Needs AdMob anyway | ✅ AdMob | ✅ AdMob |
| Clean Google Sign-In | Medium | ⚠️ Redirect flow | ✅ Native | ✅ Native |
| Content updates without review | Medium | ✅ Instant | ❌ Store release | ❌ Store release |
| Web-platform parity (TTS, SW) | Medium | ✅ Chrome | ⚠️ WebView | n/a |
| Review risk | High | ✅ Low | ⚠️ Moderate | ✅ Low |

---

## 3. Decision and rationale

**Choose the TWA.**

The two criteria that dominate are *maintenance cost* and *time to first
submission*, and the TWA wins both decisively. The app is a single-maintainer
static site; introducing a second build target with its own release cadence is a
disproportionate cost for the benefits gained.

The two things Capacitor is genuinely better at — AdMob and native sign-in —
are both addressable within the TWA path:

- Ads are being **removed** from packaged builds for v1 anyway (decision D2), so
  AdMob is not on the critical path.
- Sign-in is fixable with `signInWithRedirect`, which is a small, contained
  change to `firebase-client.js` that also improves the mobile web experience.

Offline-from-install is a real Capacitor advantage, but it is mitigated by the
existing service worker plus the proposed explicit "download for offline"
action.

---

## 4. Conditions that would flip this decision

Revisit and move to **Capacitor** if any of the following become true:

1. **Ad revenue from the app is required.** AdMob needs a native SDK; a TWA
   cannot host it. If in-app monetisation becomes a goal rather than a
   nice-to-have, Capacitor becomes the right answer.
2. **A custom domain proves unobtainable or unstable.** The TWA hard-depends on
   a verified origin. If the origin cannot be stabilised, remove the dependency.
3. **Offline-from-install becomes a headline feature.** If "works with no
   internet, ever" is something the listing promises, bundling the data is the
   honest way to deliver it.
4. **A native capability is needed** that the web platform does not expose —
   for example, scheduled local notifications for daily-streak reminders. This
   is a plausible near-term feature request for a study-streak app, and it is
   the most likely trigger of the four.

Item 4 deserves attention now: the app already has a daily-goal ring and a
streak counter, which are exactly the features that benefit from a reminder
notification. Web push on Android is possible from a TWA, but requires a push
service and notification permission plumbing. Scope this deliberately rather
than discovering it after launch.

---

## 5. Chosen toolchain

| Concern | Choice | Note |
| --- | --- | --- |
| Generator | Bubblewrap CLI | Checked into source control so the build is reproducible and re-runnable for updates. |
| Artifact | Android App Bundle (`.aab`) | Mandatory for new Play submissions. |
| Signing | **Play App Signing** enabled | Google holds the app signing key; you hold the upload key. Both fingerprints must appear in `assetlinks.json` — see the blockers document. |
| Application ID | Derived from the **custom domain**, reversed | Not `app.vercel.udsp`. The application ID is permanent and cannot be changed after publication. |
| Min SDK | Bubblewrap default | The `android-browser-helper` fallback covers devices without a TWA-capable browser. |
| Target SDK | Latest Play requirement at submission time | Verify against Play's target API level policy on the day you submit; it moves annually. |

`android-browser-helper` provides a Custom Tabs fallback for the small number of
devices lacking a TWA-capable browser, so no separate handling is required.
