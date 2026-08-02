# macOS — Packaging Strategy

Decision record for **how** to get `udsp` onto macOS.

**Decision: a Tauri host distributed as a directly-downloaded, notarized `.dmg`,
with a Homebrew Cask for reach. The Mac App Store is a documented stretch goal,
not the launch channel.**

---

## 1. Options considered

macOS is the only platform in this project where the *distribution channel*
changes the *product*. A Mac App Store build and a direct-download build are not
the same app with different wrappers — they differ in how content is loaded,
which authentication providers must exist, and what the sandbox permits.

### Option A — Direct notarized `.dmg` (recommended)

Signed with a Developer ID certificate, notarized by Apple, stapled, and
downloaded from the project's own site or GitHub Releases.

| Pros | Cons |
| --- | --- |
| **No App Review.** Guideline 4.2 is never evaluated. | No App Store discovery. |
| **No Sign in with Apple requirement.** 4.8 applies to App Store apps. | Still requires the $99/yr Apple Developer Program for a Developer ID certificate. |
| Content may load from the live web — no 2.4.5 conflict. | You own the update mechanism. |
| No App Sandbox requirement. | Users see one Gatekeeper prompt on first open. |
| Ships as soon as CI is green; notarization takes minutes. | |
| Same Tauri codebase as the Linux track. | |

Notarization is a **malware scan, not a quality review**. Apple checks for
signing validity, hardened runtime, and known-bad code. It does not evaluate
whether the app is "app-like."

### Option B — Mac App Store

| Pros | Cons |
| --- | --- |
| Genuine discovery; the default place Mac users look. | **Guideline 4.2** must be satisfied — see [01-app-analysis.md](01-app-analysis.md) §4. |
| Handles updates, refunds, and payment. | **4.8 forces Sign in with Apple** — a real change to `firebase-client.js` and the Firebase project. |
| Users trust the channel. | **2.4.5(i)** requires App Sandbox. |
| No Gatekeeper prompt. | **2.4.5(iv)/(vii)** effectively force bundling content locally. |
| | Review is a human judgement with an uncertain outcome and an open-ended timeline. |
| | Every web deploy that changes app behaviour arguably needs a new submission. |

That final row deserves emphasis. The Windows and Linux tracks derive much of
their value from **content updates reaching users with no review**. Guideline
2.4.5(vii) — *"They must use the Mac App Store to distribute updates; other
update mechanisms are not allowed"* — removes that advantage entirely. The Mac
App Store build is a fundamentally more expensive product to operate, not just
to ship.

### Option C — Electron

| Pros | Cons |
| --- | --- |
| Chromium rendering identical to the Windows WebView2 build. | ~150 MB versus roughly 10–15 MB for Tauri. |
| Mature packaging and auto-update ecosystem. | **You own Chromium security updates** — the argument that already ruled Electron out for Windows. |
| | **Guideline 2.5.6**: *"Apps that browse the web must use the appropriate WebKit framework and WebKit JavaScript."* A non-WebKit engine is an unnecessary argument to have with a reviewer. |
| | A third engine to test against, for no gain. |

Rejected for the same reason as the Windows track, with 2.5.6 as an additional
strike.

### Option D — Native SwiftUI rewrite

Not seriously considered. Roughly 25 JS modules of game logic plus a bespoke
progress and SRS engine, rewritten for no user-visible gain and permanently
forked from the website. It would unambiguously satisfy 4.2 — at a cost far
exceeding the value of the channel.

---

## 2. Decision matrix

| Criterion | Weight | A: Direct `.dmg` | B: Mac App Store | C: Electron | D: Native |
| --- | --- | --- | --- | --- | --- |
| Review risk | High | ✅ None | ❌ 4.2 is a judgement call | ⚠️ 2.5.6 friction | ✅ None |
| Time to first release | High | ✅ Days | ❌ Weeks + review | ⚠️ Weeks | ❌ Months |
| Content updates without review | High | ✅ Instant | ❌ Prohibited by 2.4.5(vii) | ✅ Instant | ❌ N/A |
| Engineering cost | High | ✅ Shared with Linux | ❌ +Sign in with Apple, sandbox, bundling | ⚠️ New stack | ❌ Full rewrite |
| Discovery | Medium | ❌ None | ✅ Best | ❌ None | ✅ Best |
| Security maintenance | High | ✅ Apple services WebKit | ✅ Apple | ❌ Yours | ✅ Apple |
| Artefact size | Low | ✅ ~10–15 MB | ⚠️ +36 MB bundled data | ❌ ~150 MB | ✅ Small |
| Annual cost | Medium | ⚠️ $99/yr | ⚠️ $99/yr | ⚠️ $99/yr | ⚠️ $99/yr |
| Gatekeeper friction | Low | ⚠️ One prompt | ✅ None | ⚠️ One prompt | ⚠️ One prompt |

---

## 3. Decision and rationale

**Ship the direct notarized `.dmg` first. Add a Homebrew Cask. Revisit the Mac
App Store once the native additions exist and there is evidence the app is
wanted on macOS at all.**

Three factors decide it.

**The Mac App Store changes the product, and the change is expensive.** Sign in
with Apple, App Sandbox, and locally-bundled content are not packaging options —
they are engineering work in the `udsp` repo and the Firebase project, plus a
permanent operating cost from 2.4.5(vii). All three are avoided entirely by
Option A, and all three can be added later if the channel proves worth it. The
reverse is not true: work done for the App Store cannot be un-done cheaply.

**Review risk is unquantifiable and front-loaded.** Every other criterion in the
matrix can be improved after launch. A 4.2 rejection cannot be predicted,
scheduled, or appealed on a known timetable. Leading with a channel that has no
editorial review means macOS users are served while that question stays open.

**The direct build is nearly free given the Linux track.** Option A and the
Linux host are one Tauri codebase, one WebKit engine family, one
`bootstrap.js`. The genuinely macOS-specific work is signing, notarization, an
`.icns`, and a menu bar. That is a small increment on work already planned.

### What would flip this decision

The recommendation is conditional. Reconsider the Mac App Store if:

- **Sign in with Apple gets built anyway.** It is arguably worth having for its
  own sake, and it removes the largest single blocker.
- **Content bundling gets built for offline reasons.** §M7 stops being a cost
  the moment it is something the product wants regardless.
- **Direct-download numbers justify it.** If macOS turns out to be a meaningful
  share of users, the discovery argument gains real weight.
- **Apple's own reading turns out to be favourable.** §8 of
  [01-app-analysis.md](01-app-analysis.md) suggests testing this rather than
  assuming.

Until at least two of those hold, the App Store is not worth its cost.

### Homebrew Cask

Cheap, and worth doing at launch. A cask is a small Ruby file in a community
repository pointing at the same notarized `.dmg`, giving
`brew install --cask topwords`. It costs one pull request, needs no
infrastructure, and reaches the developer-adjacent audience most likely to use
a Turkish-market language trainer on a Mac. It requires the app to be notarized
— which Option A already is.

---

## 4. Relationship to the Linux track

These two tracks are **one codebase**. That is the central planning fact.

| Concern | Linux host | macOS host |
| --- | --- | --- |
| Framework | Tauri v2 | **Same project** |
| Engine | WebKitGTK 4.1 | `WKWebView` — same WebKit family |
| Injected bootstrap | `initialization_script` | **Identical API, identical file** |
| Layout fix (L8 / M9) | `bootstrap.js` | **Identical file, unchanged** |
| Ad blocking (L3 / M4) | Request interception | **Same code** |
| First-run notice (L7 / M8) | `bootstrap.js` | **Identical file, unchanged** |
| Auth (L4 / M6) | Redirect, or system browser | Redirect, or `ASWebAuthenticationSession` |
| TTS (L5 / M2) | `speech-dispatcher` bridge | `AVSpeechSynthesizer` bridge |
| Icons | hicolor PNG set | `.icns` |
| Signing | None | Developer ID + notarization |

Only the last four rows differ, and two of them are thin platform shims behind a
common interface.

**Sequencing implication: build the Linux host first.** It has no signing
identity, no annual fee, no notarization step, and no review queue, so the
shared code can be debugged in the fastest possible loop. By the time macOS
work starts, the Rust host, the navigation policy, the ad blocking, and
`bootstrap.js` are already proven — leaving only genuinely macOS-specific work.

Doing macOS first would mean debugging shared logic through the slowest
toolchain in the project.

---

## 5. Chosen toolchain

| Concern | Choice | Note |
| --- | --- | --- |
| Host framework | **Tauri v2** | Shared with the Linux track. |
| Web engine | `WKWebView` | System-provided; Apple ships security updates. |
| Target | **Universal binary** (`aarch64` + `x86_64`) | `--target universal-apple-darwin`. Apple Silicon is the majority; Intel Macs are still supported. |
| Minimum OS | macOS 11 Big Sur or later | Verify against **2.4.5(viii)**; do not set it lower than can be tested. |
| Icon | `.icns` generated from `icon.svg` | `cargo tauri icon` produces the full set. |
| Signing | **Developer ID Application** certificate | Direct distribution. A *Mac App Distribution* certificate is a different cert for a different channel. |
| Hardened Runtime | Enabled | Mandatory for notarization. |
| Notarization | `xcrun notarytool` + `xcrun stapler` | `altool` is retired. |
| Artefact | `.dmg`, stapled | Tauri bundles it; `create-dmg` if more layout control is wanted. |
| Secondary channel | Homebrew Cask | Points at the same `.dmg`. |
| CI | GitHub Actions, `macos-latest` | Free for public repos. Signing certificates go in encrypted secrets. |
| Updates | Tauri updater plugin | Direct channel only — prohibited by 2.4.5(vii) on the App Store. |

**One warning about certificates.** Developer ID Application and Mac App
Distribution are distinct certificates with distinct entitlements, and using
the wrong one produces confusing failures late in the pipeline — typically at
notarization or upload, after everything appeared to succeed. Decide the
channel before generating certificates, and label them clearly in the keychain.
