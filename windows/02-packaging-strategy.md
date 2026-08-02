# Windows — Packaging Strategy

Decision record for **how** to get `udsp` onto the Microsoft Store.

**Decision: MSIX packaged web app, generated with PWABuilder, running on
WebView2.**

---

## 1. Options considered

### Option A — MSIX packaged web app via PWABuilder (recommended)

PWABuilder consumes the live manifest and emits an MSIX that hosts the site in
**WebView2**. Microsoft's own supported path for putting a PWA in the Store.

| Pros | Cons |
| --- | --- |
| One codebase. Web deploys reach users with no Store review. | Storage is isolated from the user's Edge profile — see [01-app-analysis.md](01-app-analysis.md) §5. |
| Tiny package (a few MB — assets and manifest only). | Requires the WebView2 runtime (present on all supported Windows 11). |
| WebView2 is serviced by Microsoft; no Chromium to maintain. | Content unavailable if the host is down and uncached. |
| Full PWA feature set: service worker, `localStorage`, Web Speech. | No native OS APIs beyond what the web platform exposes. |
| No Digital Asset Links equivalent — nothing to verify. | Package identity must be reserved in Partner Center first. |
| Store listing can be submitted immediately; no tester gate. | |

### Option B — Electron

Ships its own Chromium and Node runtime; content can be bundled locally.

| Pros | Cons |
| --- | --- |
| Full offline from install; bundle the 36 MB of `data/`. | Package balloons to ~100–150 MB versus a few MB. |
| Complete control over window chrome, menus, tray. | **You own Chromium security updates.** Every Chromium CVE becomes your release obligation. |
| Native APIs: notifications, file system, auto-update. | Store review is stricter on Electron apps; more surface to justify. |
| Native Google Sign-In flows possible. | Forks the build permanently. |

Electron is the right answer when an app needs deep OS integration or must work
entirely offline. Neither applies here, and the security-maintenance burden is
a poor trade for a single-maintainer static site.

### Option C — WinUI 3 + WebView2 host

A native Windows app shell embedding WebView2 manually.

Strictly more work than Option A for the same runtime, with the only gain being
custom native chrome. Worth revisiting **only** if the app needs Windows-native
features PWABuilder cannot express — for example, jump lists beyond manifest
`shortcuts`, or a system tray presence. Not needed for v1.

### Option D — Native rewrite (WinUI / .NET MAUI)

Not seriously considered, for the same reasons as the Android track: ~25 JS
modules of game logic plus a bespoke progress engine, rewritten for no
user-visible gain.

---

## 2. Decision matrix

| Criterion | Weight | MSIX/PWABuilder | Electron | WinUI+WebView2 |
| --- | --- | --- | --- | --- |
| Maintenance cost | High | ✅ Best | ❌ Fork + Chromium CVEs | ❌ Fork |
| Time to first submission | High | ✅ Days | ⚠️ Weeks | ❌ Weeks+ |
| Package size | Medium | ✅ ~Few MB | ❌ ~100+ MB | ⚠️ Moderate |
| Security maintenance burden | High | ✅ Microsoft services WebView2 | ❌ Yours | ✅ Microsoft |
| Offline from install | Medium | ⚠️ After first use | ✅ Yes | ⚠️ Same as A |
| Content updates without review | Medium | ✅ Instant | ❌ Store release | ✅ Instant |
| Store review risk | Medium | ✅ Low | ⚠️ Higher | ✅ Low |
| Native OS integration | Low | ⚠️ Manifest-level only | ✅ Full | ✅ Full |

---

## 3. Decision and rationale

**Choose the MSIX packaged web app.**

The deciding factor is the **security maintenance burden**. Electron's headline
advantage — bundled offline content — is worth real money to this app, but it
comes with a standing obligation to ship a release every time Chromium patches a
vulnerability. For a project with one maintainer and no existing native release
pipeline, that obligation is the largest hidden cost on the table, and it never
goes away.

WebView2 delegates that entire problem to Microsoft while providing the same
Blink engine and the same web platform features the app already depends on.

The offline gap is partially closed by the existing service worker, and further
closed by the proposed explicit "download for offline" action shared with the
Android track.

---

## 4. Relationship to the Android track

The two tracks share **all four blocking fixes**:

| Fix | Android | Windows |
| --- | --- | --- |
| PNG icons in manifest | Required (≥512 maskable) | Required (full tile set) |
| Auth redirect instead of popup | Required | Required |
| Ad gating in packaged builds | Required | Required |
| Custom domain | Required (asset links) | Recommended, not blocking |

Only the **custom domain** requirement differs in severity: Android compiles
the origin into the APK and verifies it cryptographically, so changing it later
is destructive. Windows has no equivalent binding, so the domain is a branding
and consistency concern rather than a technical blocker.

**Sequencing implication:** because Windows has no closed-testing gate and no
asset-links dependency, it can ship first. Doing so validates the shared
manifest and auth fixes against a real store review before the Android track
commits to a permanent, unchangeable application ID and origin.

---

## 5. Chosen toolchain

| Concern | Choice | Note |
| --- | --- | --- |
| Generator | PWABuilder (<https://www.pwabuilder.com>) | Produces the MSIX plus a test-signed variant for sideloading. |
| Runtime | WebView2 (Evergreen) | Declared as an MSIX dependency; serviced by Microsoft. |
| Artifact | `.msixbundle` | Submitted to Partner Center. |
| Signing | **Microsoft signs the production package** | You only need a self-signed certificate for local sideload testing. Do not buy a code-signing certificate for Store distribution. |
| Package identity | Reserved in Partner Center **before** generating | Publisher, Publisher display name, and Package identity Name are baked in. A mismatch is the top rejection cause. |
| Certification check | Windows App Certification Kit (WACK) | Run locally before submitting. |

PWABuilder also offers a "Store-ready" versus "sideload/test" download. They
differ in signing. Use the test package for local verification and the
Store-ready package for submission — mixing them up wastes a review cycle.
