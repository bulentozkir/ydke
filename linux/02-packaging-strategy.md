# Linux — Packaging Strategy

Decision record for **how** to get `udsp` onto Linux desktops.

**Decision: a Tauri host bundled as an AppImage, plus `.deb` and `.rpm`,
published on GitHub Releases. Flathub is a stretch goal, not the launch
channel.**

---

## 1. Options considered

Linux differs from the other three tracks in one important way: **there is no
single store.** There is a ladder of channels with wildly different gatekeeping,
and the choice of channel matters more than the choice of toolkit.

### Option A — AppImage + `.deb` / `.rpm` on GitHub Releases (recommended)

A single self-contained executable users download and run, plus native packages
for the two dominant packaging families.

| Pros | Cons |
| --- | --- |
| **No gatekeeper.** No review, no policy, no submission queue. | No discovery — users must find the project, not the store. |
| **Bundles its own WebKitGTK**, eliminating the distro-version lottery in [01-app-analysis.md](01-app-analysis.md) §2. | AppImage size grows to roughly 80–120 MB as a result. |
| Ships the moment CI is green. | No automatic updates without adding a mechanism. |
| Same artefacts serve direct download and a future store submission. | `.deb`/`.rpm` still depend on the system WebKitGTK. |
| Nothing about the app has to be justified to a reviewer. | AppImage may need `libfuse2`, absent by default on recent Ubuntu. |

### Option B — Flathub

The de facto Linux app store, surfaced in GNOME Software and KDE Discover on
most modern distributions. By far the best discovery, and by far the strictest.

| Pros | Cons |
| --- | --- |
| Genuine discovery — front and centre in most software centres. | **Explicitly rejects web wrappers.** See the quoted policy below. |
| Sandboxed by default, with portal-based permissions. | **Bans AI-generated code and AI-opened submission PRs.** |
| Handles updates, deltas, and mirroring for free. | Requires complete English UI localisation; this app is Turkish-first. |
| Runtime provides a consistent, current WebKitGTK. | Builds must run offline from source, with every dependency vendored. |
| Free. | App ID requires a domain or code host you demonstrably control. |

Flathub's own requirements state:

> Simple web wrapper applications that embed local or remote content in a web
> engine without providing significant polish, functionality, or meaningful
> desktop integration will not be accepted.

That sentence is aimed precisely at what a naive port of this app would be. It
is not, however, unconditional — the escape clause is *"without providing
significant polish, functionality, or meaningful desktop integration."* The
native-integration work described in §3 is what converts a rejection into a
plausible submission.

A second Flathub policy is more awkward and cannot be engineered around:

> Applications containing AI-generated or AI-assisted code, documentation, or
> any other content are not allowed.

…together with a requirement that submission pull requests must not be opened
or automated using AI tools. Given how this analysis and the Windows host were
produced, **that is a direct conflict.** It is called out here rather than
buried, because discovering it after building a Flatpak manifest would be an
expensive surprise. See [03-blockers-and-fixes.md](03-blockers-and-fixes.md)
§L12.

### Option C — Snap Store

Canonical's store, pre-installed on Ubuntu, which is the single largest Linux
desktop base.

| Pros | Cons |
| --- | --- |
| **Automated review** — far more permissive than Flathub about wrappers. | Effectively Ubuntu-centric; other distros range from indifferent to hostile. |
| Pre-installed on Ubuntu, so real discovery with no user setup. | Single proprietary backend store. |
| Handles updates and staged rollouts. | Strict confinement plus audio plus TTS is a fiddly permissions combination. |
| No AI-authorship policy. | Slower first launch than a native package. |

Snap is the pragmatic middle ground: real store distribution without Flathub's
editorial bar. It is the recommended **second** channel once the AppImage is
proven.

### Option D — Native GTK or Qt rewrite

Not seriously considered, for the same reason as the other tracks: roughly 25 JS
modules of game logic plus a bespoke progress and SRS engine, rewritten for no
user-visible gain, and permanently forked from the website.

---

## 2. Decision matrix

| Criterion | Weight | A: AppImage/deb/rpm | B: Flathub | C: Snap | D: Native |
| --- | --- | --- | --- | --- | --- |
| Time to first release | High | ✅ Days | ❌ Weeks + review | ⚠️ ~1 week | ❌ Months |
| Policy risk | High | ✅ None | ❌ Anti-wrapper + AI policy | ⚠️ Low | ✅ None |
| Engine version control | High | ✅ Bundled | ✅ Runtime-provided | ✅ Base-provided | ✅ N/A |
| Discovery | Medium | ❌ None | ✅ Best | ✅ Good on Ubuntu | ❌ None |
| Maintenance cost | High | ✅ One CI job | ⚠️ Manifest + upstream metadata | ⚠️ snapcraft.yaml | ❌ Permanent fork |
| Cross-distro reach | Medium | ✅ Broad | ✅ Broad | ⚠️ Ubuntu-leaning |  ⚠️ Depends |
| Auto-update | Medium | ⚠️ Must be added | ✅ Free | ✅ Free | ❌ Must be built |
| Artefact size | Low | ⚠️ 80–120 MB | ✅ Shared runtime | ⚠️ Moderate | ✅ Small |
| Sandboxing | Low | ❌ None | ✅ Strong | ✅ Strong | ❌ None |

---

## 3. Decision and rationale

**Ship the AppImage plus `.deb` and `.rpm` first. Add Snap second. Treat
Flathub as a stretch goal contingent on the native-integration work and a
resolution to the authorship question.**

Three factors decide it.

**Policy risk is the dominant cost, and it is front-loaded.** Every other
criterion can be improved incrementally after launch; a Flathub rejection
cannot. Leading with an ungated channel means the app reaches real users while
the harder questions are still open, and it produces the screenshots, usage
evidence, and bug reports that make a later store submission stronger.

**The engine-version lottery is a bigger practical threat than package size.**
[01-app-analysis.md](01-app-analysis.md) §2 explains that WebKitGTK arrives via
the distribution and can be years old. An 80–120 MB AppImage that bundles a
known-good engine is a better user experience than a 15 MB `.deb` that renders
incorrectly on an older LTS. Ship both, and treat the AppImage as the
recommended download.

**The work that unlocks Flathub is worth doing regardless.** The native
integrations that answer the anti-wrapper objection — a real TTS bridge, proper
desktop metadata, keyboard accelerators, system notifications — are the same
items that fix genuine defects. §4 lists them. None is busywork undertaken
purely to satisfy a reviewer.

### What "meaningful desktop integration" has to mean here

The bar is the same one Apple sets in the macOS track, so build it once:

| Integration | Fixes | Also satisfies |
| --- | --- | --- |
| **Native TTS bridge** via `speech-dispatcher` | [03-blockers-and-fixes.md](03-blockers-and-fixes.md) §L5 — silent Listen button | Flathub polish bar; macOS §M2 |
| Full offline: ship `data/` in-bundle | 36 MB of lazy network fetches | macOS §M7 |
| `.desktop` entry + AppStream MetaInfo | §L1 — app is invisible to launchers | Mandatory for Flathub and Snap |
| Menu bar and keyboard accelerators | §L11 — no accelerators at all | Flathub polish bar |
| Desktop notifications for the daily streak | Nothing — new capability | Flathub polish bar |
| System colour-scheme following via portal | Theme ignores desktop preference | Flathub polish bar |
| XDG portal file dialogs for progress export | No export path exists | Sandbox-compatible by construction |

---

## 4. Relationship to the Windows track

The Linux host is a **re-implementation of a host that already exists and
works**, not a new design. The Windows WPF host established the architecture;
Linux changes the shell around it, not the logic inside it.

| Concern | Windows host | Linux host |
| --- | --- | --- |
| Injected bootstrap script | `AddScriptToExecuteOnDocumentCreatedAsync` | Tauri `initialization_script` |
| Layout fix (W7 / L8) | `bootstrap.js` | **Identical file, unchanged** |
| Ad blocking (W4 / L3) | `WebResourceRequested` → HTTP 204 | Tauri request interception |
| Auth popup (W3 / L4) | Owned `PopupWindow` | New Tauri window or system browser |
| First-run notice (W5 / L7) | `bootstrap.js` | **Identical file, unchanged** |
| Navigation policy | `AppConfig.IsAllowedInApp` | Same rules, ported to Rust |
| External links | `Process.Start` after scheme check | `xdg-open` after scheme check |

`bootstrap.js` is the important line in that table. It is plain JavaScript with
no host bindings, so the layout fix, ad stubbing, and first-run notice transfer
without modification. Keep a **single copy** shared by all hosts rather than
forking it per platform — three divergent copies of the same CSS injection is a
predictable and avoidable maintenance failure.

**Sequencing implication:** Linux is the cheapest of the remaining tracks. It
has no store account, no signing identity, no annual fee, and no review queue.
It can be built and released while the macOS track waits on an Apple Developer
Program enrolment.

---

## 5. Chosen toolchain

| Concern | Choice | Note |
| --- | --- | --- |
| Host framework | **Tauri v2** | One Rust codebase shared with macOS. Uses the system WebKitGTK via `webkit2gtk-4.1`. |
| Web engine | WebKitGTK 4.1 | Bundled in the AppImage; system-provided for `.deb` / `.rpm`. |
| Bundler | `tauri build --bundles appimage,deb,rpm` | Tauri emits all three from one build. |
| TTS backend | `speech-dispatcher` | Fronts espeak-ng, Festival, and Pico; the standard Linux abstraction. |
| Icon generation | `tauri icon` from `icon.svg` | Produces the hicolor PNG set; shared with the Windows W1 work. |
| Metadata | `.desktop` + AppStream MetaInfo XML | Validated with `appstreamcli validate`. |
| App ID | `io.github.qlupala9p.udsp` | Code-host form, since no custom domain is registered yet. See §L9. |
| CI | GitHub Actions, `ubuntu-latest` | Build on the **oldest** supported base, not the newest — glibc symbols do not link backwards. |
| Signing | None required | Linux has no Gatekeeper equivalent. Publish `sha256sum` files alongside the artefacts. |
| Distribution | GitHub Releases | Snap and Flathub layered on later. |

**One CI caveat worth stating early.** Build on the oldest glibc you intend to
support. A binary produced on a current Ubuntu will refuse to start on an older
one with a `GLIBC_2.xx not found` error, and the failure appears only on user
machines — never on the build host. Either pin an older runner image or build
inside a container based on the target floor.
