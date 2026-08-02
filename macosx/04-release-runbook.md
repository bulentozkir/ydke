# macOS — Release Runbook

Procedure for building, signing, notarizing, and publishing the macOS app.

Prerequisite: the **Blocker** items in
[03-blockers-and-fixes.md](03-blockers-and-fixes.md) that apply to your chosen
channel are closed. On the direct-download path that is §M3, §M4, and §M5 —
§M1, §M2, §M7, and §M12 do not apply.

**This runbook assumes no Mac hardware is available** and is written against
GitHub Actions `macos-latest` runners. Every command below can also be run
locally on a Mac if one exists.

---

## 1. Accounts and costs

| Item | Cost | Required for |
| --- | --- | --- |
| **Apple Developer Program** | **$99/yr** | Signing, notarization, and the App Store — all of it |
| GitHub Actions `macos-latest` | Free for public repos | CI builds and notarization |
| Homebrew Cask | Free | Secondary distribution |
| App Store Connect | Included in the $99 | Mac App Store only |

macOS is the **only track with a recurring fee**, and it is unavoidable —
without a Developer ID certificate, a downloaded app is blocked by Gatekeeper
with a message users read as "this app is broken."

**Enrolment is not instant.** Identity verification takes days; organisations
additionally need a D-U-N-S number, which can take longer. Start enrolment
before it is on the critical path — it is the classic hidden delay in a macOS
release schedule.

---

## 2. Apple Developer setup

Do this before writing any host code. Certificates and identifiers propagate
into the build configuration.

| Item | Where | Value |
| --- | --- | --- |
| Team ID | Membership details | 10 characters; needed by `notarytool` |
| Bundle identifier | Identifiers → App IDs | `com.topwords.trainer` or `io.github.qlupala9p.udsp` |
| Developer ID Application cert | Certificates | Signs the app for **direct** distribution |
| App-specific password | <https://account.apple.com> → Sign-In and Security | For `notarytool`; the plain Apple ID password does not work |

If the Mac App Store is in scope, a **Mac App Distribution** certificate and a
provisioning profile are also needed — these are different from the Developer
ID certificate, and mixing them up fails late. See
[03-blockers-and-fixes.md](03-blockers-and-fixes.md) §M5.

Use the **same bundle identifier** as the Linux application ID where possible.
Keeping one identity across platforms simplifies Firebase configuration, deep
links, and support.

Store credentials once with `notarytool` rather than passing them on every
command:

```bash
xcrun notarytool store-credentials "AC_NOTARY" \
  --apple-id "you@example.com" --team-id "TEAMID" --password "app-specific-pw"
```

---

## 3. Toolchain and prerequisites

Xcode command line tools, the Rust toolchain, and both architecture targets:

```bash
xcode-select --install
curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh
rustup target add aarch64-apple-darwin x86_64-apple-darwin
cargo install tauri-cli --version '^2'
```

Generate the icon set — this produces the `.icns` required by §M3:

```bash
cargo tauri icon path/to/icon.svg
```

Review the output rather than accepting it. As noted in §M3, macOS expects a
rounded-rect silhouette with padding, not a full-bleed square; a straight
rasterisation of `icon.svg` will look foreign in the Dock.

Set the minimum system version explicitly in the Tauri configuration. Do not
set it lower than you can actually test — **2.4.5(viii)** expects the app to run
on the currently shipping OS, and claiming support for versions you have never
launched on is how obscure rejections happen.

---

## 4. Build

```bash
cargo tauri build --target universal-apple-darwin
```

This produces a **universal binary** containing both `arm64` and `x86_64`
slices. Apple Silicon is the majority of the installed base, but Intel Macs are
still supported and still in use — build both. The size cost is small relative
to the support cost of shipping the wrong architecture.

Artefacts appear under
`target/universal-apple-darwin/release/bundle/`:

| Artefact | Path |
| --- | --- |
| App bundle | `macos/Top Words.app` |
| Disk image | `dmg/Top Words_<ver>_universal.dmg` |

Confirm both slices are actually present before going further — a build that
silently produced one architecture looks identical until a user on the other
one tries to launch it:

```bash
lipo -info "Top Words.app/Contents/MacOS/topwords"
```

---

## 5. Sign, notarize, staple

Tauri performs signing during the build when the environment is configured, and
this is the preferred route because it handles nested code correctly.

| Variable | Purpose |
| --- | --- |
| `APPLE_CERTIFICATE` | Base64-encoded `.p12` |
| `APPLE_CERTIFICATE_PASSWORD` | Its password |
| `APPLE_SIGNING_IDENTITY` | `Developer ID Application: NAME (TEAMID)` |
| `APPLE_ID`, `APPLE_PASSWORD`, `APPLE_TEAM_ID` | Notarization credentials |

In CI these belong in encrypted repository secrets. **Never commit a `.p12` or
an app-specific password**, and be careful that build logs do not echo them.

If signing manually, sign **inside-out** — nested frameworks and helpers first,
then the outer bundle:

```bash
codesign --force --options runtime --timestamp \
  --sign "Developer ID Application: NAME (TEAMID)" \
  --entitlements entitlements.plist "Top Words.app"
```

> Avoid `--deep`. Apple explicitly discourages it: it applies the same
> entitlements to nested code that should have its own, and it is a documented
> source of subtle signing bugs that only surface at notarization.

Then notarize the `.dmg` and staple the ticket:

```bash
xcrun notarytool submit "Top Words_1.0.0_universal.dmg" \
  --keychain-profile "AC_NOTARY" --wait
xcrun stapler staple "Top Words_1.0.0_universal.dmg"
```

`--wait` blocks until Apple returns a verdict, usually within minutes.
**Stapling is not optional** — without it, a user who is offline on first launch
gets a Gatekeeper failure, because the notarization ticket cannot be fetched.

On rejection, fetch the reason rather than guessing:

```bash
xcrun notarytool log <submission-id> --keychain-profile "AC_NOTARY"
```

The overwhelmingly common causes are a missing Hardened Runtime flag, a missing
secure timestamp, and unsigned nested binaries.

---

## 6. Local verification

Automated checks first:

```bash
codesign --verify --deep --strict --verbose=2 "Top Words.app"
spctl -a -vvv -t install "Top Words.app"
xcrun stapler validate "Top Words_1.0.0_universal.dmg"
```

`spctl` should report `source=Notarized Developer ID`. Anything else means the
app will be blocked on a user's machine.

**Then test the real Gatekeeper path.** This is the step most often done wrong:

> Download the `.dmg` **through a web browser** so it receives the
> `com.apple.quarantine` attribute, then open it. Copying the file over a
> network share, `scp`, or AirDrop does not reproduce the quarantine flag, and
> a build that appears to launch cleanly can still be blocked for real users.

Verify on both architectures. A universal binary that works on Apple Silicon
can still fail on Intel, and CI runners are increasingly arm64-only.

---

## 7. Publish

**Direct download.** Host the stapled `.dmg` on GitHub Releases or the project
site, with a `sha256sum` alongside. Release notes should state the minimum
macOS version and note that the app is notarized.

**Homebrew Cask.** A small Ruby file in `homebrew/homebrew-cask` pointing at the
same `.dmg`, giving `brew install --cask topwords`. It costs one pull request,
needs no infrastructure, and reaches a technically-inclined audience. Homebrew
requires the app to be notarized — which §5 has already done — and the cask must
be updated with a new version and checksum on each release, which is worth
automating in CI from the start.

**Auto-update.** Tauri's updater plugin can check a JSON endpoint and prompt.
Available on the direct channel only — **2.4.5(vii)** prohibits it on the App
Store, where updates must go through the store.

---

## 8. Mac App Store submission (optional — gated)

**Do not start this section until the `GATE` decision in
[03-blockers-and-fixes.md](03-blockers-and-fixes.md) has been made
deliberately.** It requires §M1 (Sign in with Apple), §M2 (native additions),
§M7 (bundled content), §M11 (sandbox entitlements), and §M12 (privacy
disclosures) — the four most expensive items in this repository.

Differences from the direct path:

| Step | Direct | Mac App Store |
| --- | --- | --- |
| Certificate | Developer ID Application | **Mac App Distribution** |
| Provisioning profile | Not needed | **Required**, embedded in the bundle |
| Sandbox | Optional | **Mandatory** — `com.apple.security.app-sandbox` |
| Content | May load the live site | **Bundled** — §M7 |
| Auth providers | Google alone is fine | **Google + Apple** — §M1 |
| Notarization | Required | Not applicable — the store handles it |
| Upload | Attach to a release | Transporter, or `xcrun altool --upload-package` |
| Review | None | Human review against 4.2 |

**Verify the current upload tooling before relying on it.** Apple has moved
this pipeline more than once — `altool` has been progressively deprecated in
favour of `notarytool` for notarization and Transporter for store uploads.
Check the current documentation rather than trusting a command from a runbook.

Store listing assets: screenshots at the app's native resolution, a description
under 4,000 characters, keywords, a support URL, and a privacy policy URL —
`privacy.html` already exists. Publish **`tr-TR` as the primary listing
language** with `en-US` secondary, matching the app's actual audience, and reuse
the SEO copy already tuned for YDS / YÖKDİL / TOEFL intent in `index.html`.

---

## 9. Pre-release verification

Direct-download channel:

- [ ] `lipo -info` confirms both `arm64` and `x86_64` slices (§4)
- [ ] `codesign --verify --deep --strict` passes
- [ ] `spctl -a -vvv -t install` reports `source=Notarized Developer ID`
- [ ] `xcrun stapler validate` passes on the `.dmg`
- [ ] **Downloaded via a browser**, the app opens with no Gatekeeper block (§6)
- [ ] Launches on both Apple Silicon and Intel
- [ ] Launches on the oldest declared macOS version
- [ ] Google sign-in completes; `users/{uid}` created (M6)
- [ ] Signed-in state survives a full quit and relaunch
- [ ] `udsp_streak_v1` survives a quit and relaunch — persistent store (M8)
- [ ] "Load from cloud" restores progress onto a fresh install (M8)
- [ ] Listen button produces audible speech for en/de/fr/it/es/pt
- [ ] Zero requests to `pagead2.googlesyndication.com` on any page (M4)
- [ ] No page scrolls vertically at 1000×800; header and nav stay pinned (M9)
- [ ] SEO copy hidden on study pages, present on About and Help (M9)
- [ ] Menu bar present; ⌘C, ⌘W, ⌘Q, ⌘, all behave correctly (M10)
- [ ] Window size, position, and view are restored after relaunch (M10)
- [ ] Light and dark appearance both correct, and follow **live** system changes
- [ ] Dock icon renders correctly at every size (M3)
- [ ] Offline launch reaches cached content with the network disabled

Additionally for the Mac App Store:

- [ ] Sign in with Apple completes, **including the private-relay path** (M1)
- [ ] A user who signed in with Google can link an Apple identity (M1)
- [ ] App runs fully offline from a clean install — content bundled (M7)
- [ ] Sandbox enabled; app functions with only the declared entitlements (M11)
- [ ] `PrivacyInfo.xcprivacy` present; privacy labels match Play (M12)
- [ ] Account deletion reachable in-app — 5.1.1(v), already implemented
- [ ] Any post-launch download discloses its size and prompts — 4.2.3(ii) (M7)

---

## 10. Update workflow after launch

**Direct channel.** The host loads the live site, so web deploys reach users
immediately with no rebuild. A new `.dmg` is needed only when the host itself
changes — `bootstrap.js`, navigation policy, menus, icons — or when a
dependency needs updating. Each release repeats §4 through §7, and the Homebrew
Cask needs its version and checksum bumped.

**Mac App Store.** Fundamentally different. Under **2.4.5(vii)** every update
goes through the store, and because the content is bundled per §M7, **every
content change is an app update** requiring a new submission and review. Word
data corrections, new exercises, and copy fixes all queue behind review.

This asymmetry is worth restating plainly: the App Store build costs more to
operate forever, not just more to ship once. It is the strongest ongoing
argument for the sequencing in
[02-packaging-strategy.md](02-packaging-strategy.md).

---

## 11. Sequencing note

macOS should be **last** of the four tracks.

It has the longest lead time — Developer Program enrolment involves identity
verification measured in days. It has the only recurring fee. It has the only
human reviewer applying an editorial standard. And it shares a codebase with
the Linux track, so almost all of its logic can be proven somewhere cheaper
first.

**Recommended order:** close the shared `udsp` fixes → ship Windows → build the
Tauri host on Linux → port to macOS as a direct `.dmg` → decide on the Mac App
Store with real evidence in hand.

Start the Apple Developer enrolment early anyway. It costs $99 and some
paperwork, and it is the one item on this track that cannot be accelerated once
it becomes urgent.
