# Linux — Release Runbook

Procedure for building, verifying, and publishing the Linux desktop app.

Prerequisite: the **Blocker** items in
[03-blockers-and-fixes.md](03-blockers-and-fixes.md) are closed, and §L12 has
been decided so you know whether the Flathub branch in §8 is in scope.

---

## 1. Accounts and costs

Linux is the cheapest of the four tracks. There is no store account, no signing
identity, and no annual fee on the recommended path.

| Item | Cost | Required for |
| --- | --- | --- |
| GitHub account and Releases | **Free** | AppImage, `.deb`, `.rpm` distribution |
| GitHub Actions | **Free** for public repos | CI builds |
| Code signing | **Not applicable** | Linux has no Gatekeeper or SmartScreen equivalent |
| Snap Store developer account | **Free** | §7, optional |
| Flathub | **Free** | §8, optional |
| Custom domain | ~$10–15/yr | Optional; improves the app ID (§L9) and is already recommended for the Android track |

**Total to first public release: $0.** Contrast with $25 for Play, a Partner
Center fee for Microsoft, and $99/yr for Apple. There is no financial reason to
sequence Linux late.

---

## 2. Establish app identity

Do this **first**. The application ID propagates into filenames that are
painful to change afterwards — see
[03-blockers-and-fixes.md](03-blockers-and-fixes.md) §L9.

| Value | Recommended | Used in |
| --- | --- | --- |
| Application ID | `io.github.qlupala9p.udsp` | `.desktop` filename, MetaInfo `<id>`, icon filenames, Flatpak manifest |
| Binary name | `topwords` | `Exec=`, package name, `StartupWMClass` |
| Display name | `Top Words` | `.desktop` `Name`, MetaInfo `<name>` |
| Package name | `topwords` | `.deb` / `.rpm` |
| Deep-link scheme | `topwords://` | Optional, needed only for §L4 option 3 |

If a custom domain is being registered for the Android track, use the domain
form instead and skip the later migration.

Record these values once, in the host's configuration, and derive every
filename from them rather than hard-coding strings in multiple places.

---

## 3. Toolchain and prerequisites

Build host packages, on a Debian or Ubuntu base:

```bash
sudo apt install libwebkit2gtk-4.1-dev build-essential curl wget file \
  libxdo-dev libssl-dev libayatana-appindicator3-dev librsvg2-dev \
  desktop-file-utils appstream
```

Then the Rust toolchain and the Tauri CLI:

```bash
curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh
cargo install tauri-cli --version '^2'
```

**Choose the build base deliberately.** Build on the *oldest* distribution you
intend to support, not the newest. Binaries linked against a recent glibc fail
to start on older systems with `GLIBC_2.xx not found`, and that failure never
appears on the build host — only on user machines. Pin an older GitHub Actions
runner image, or build inside a container matching your floor.

Generate the icon set from the existing vector source:

```bash
cargo tauri icon path/to/icon.svg
```

This produces the hicolor PNG rasters required by §L2. The Windows track
already generated an overlapping set — reuse those outputs where the sizes
match rather than maintaining two pipelines.

---

## 4. Build

```bash
cargo tauri build --bundles appimage,deb,rpm
```

Artefacts land under `target/release/bundle/`:

| Artefact | Path | Notes |
| --- | --- | --- |
| AppImage | `appimage/topwords_<ver>_amd64.AppImage` | Self-contained; bundles WebKitGTK |
| Debian package | `deb/topwords_<ver>_amd64.deb` | Depends on the system WebKitGTK |
| RPM package | `rpm/topwords-<ver>-1.x86_64.rpm` | Same |

**Declare the TTS dependencies** in the `.deb` and `.rpm` metadata —
`speech-dispatcher` and at least one voice engine such as `espeak-ng` — or
§L5's failure mode ships to users. The AppImage should bundle them so the
recommended download is audible with no user action.

**`aarch64` is worth building** if CI time allows. ARM Linux desktops are a
small but real audience, and the marginal cost is one extra matrix entry.

---

## 5. Local verification

Automated checks first, because they are fast and catch the most common
packaging mistakes:

```bash
desktop-file-validate io.github.qlupala9p.udsp.desktop
appstreamcli validate io.github.qlupala9p.udsp.metainfo.xml
```

Then manual verification. **Test on a distribution older than the build host**
— that is where glibc and WebKitGTK problems surface. A container or throwaway
VM is sufficient for most of it, but audio and window management need a real
desktop session.

| Check | Why |
| --- | --- |
| AppImage runs on a clean system | Catches the missing-`libfuse2` failure |
| `.deb` installs and removes cleanly | `sudo apt install ./topwords_*.deb` then `apt remove` |
| Launcher entry appears with the correct icon | Validates §L1 and §L2 together |
| Only **one** dock icon while running | Catches a wrong `StartupWMClass` |
| Runs under both Wayland and X11 | Wayland is the default on current GNOME and KDE |
| HiDPI and fractional scaling | Common source of blurry or mis-sized windows |
| **TTS produces audible output** | The §L5 regression check — listen, do not just check for an absence of errors |

---

## 6. Publish on GitHub Releases

Attach every artefact plus checksums. Without signing, checksums are the only
integrity signal users have:

```bash
sha256sum topwords_*.AppImage topwords_*.deb topwords-*.rpm > SHA256SUMS
```

Release notes should state the supported distribution floor, name the AppImage
as the recommended download, and — until §L5 lands — say plainly which packages
need `speech-dispatcher` installed for audio to work.

**Auto-update.** Neither AppImage nor a manually-installed `.deb` updates
itself. Tauri's updater plugin can check a JSON endpoint published alongside
the release and prompt the user. Worth adding before the audience grows;
without it, users stay on whatever version they first downloaded, indefinitely.

---

## 7. Snap Store (optional second channel)

The recommended second channel: real store distribution and genuine discovery
on Ubuntu, with **automated review** rather than Flathub's editorial bar and no
authorship policy to resolve.

```bash
snapcraft
snapcraft upload --release=stable topwords_<ver>_amd64.snap
```

Points that need care:

- Use strict confinement. Classic confinement triggers manual review and is not
  needed here.
- Required plugs: `network`, `desktop`, `desktop-legacy`, `wayland`, `x11`,
  `audio-playback`, `browser-support`.
- **Verify TTS inside the confined snap**, not just outside it. Audio playback
  and `speech-dispatcher` socket access are separate permissions, and this is
  the most likely thing to break in the sandbox.
- The AppStream MetaInfo from §L1 supplies the store listing metadata — the
  work is already done.

---

## 8. Flathub submission (optional — gated on §L12)

**Do not start this section until
[03-blockers-and-fixes.md](03-blockers-and-fixes.md) §L12 is resolved.** The
Generative AI policy determines whether a submission is viable at all, and it
costs nothing to settle first.

Also required before submitting: §L6 (the anti-wrapper objection, answered by
real native integration) and §L10 (English localisation, at minimum declared).

Preconditions that are easy to miss:

- The manifest must be named exactly `<app-id>.yml` or `<app-id>.json`.
- **No network access during the build.** Every dependency — including the Rust
  crate registry — must be declared as a manifest source. For a Rust project
  this means generating a vendored `cargo-sources.json`.
- The build must run from source. Prebuilt binaries are not accepted.
- The MetaInfo file must be shipped **upstream**, not injected by the manifest.
- Permissions should be minimal, preferring XDG portals over broad filesystem
  or socket access.

Test locally with exactly what Flathub's CI runs, before opening anything:

```bash
flatpak run org.flatpak.Builder --force-clean --sandbox --user \
  --install builddir io.github.qlupala9p.udsp.yml
flatpak run --command=flatpak-builder-lint org.flatpak.Builder \
  manifest io.github.qlupala9p.udsp.yml
```

Submission is a pull request against the `new-pr` branch of the `flathub/flathub`
repository, titled `Add io.github.qlupala9p.udsp`. Expect review to take time
and to include questions about the wrapper objection — have the §L6 answer
ready in the PR description rather than waiting to be asked.

---

## 9. Pre-release verification

- [ ] `desktop-file-validate` passes (L1)
- [ ] `appstreamcli validate` passes (L1)
- [ ] Launcher entry appears with the correct icon at multiple sizes (L1, L2)
- [ ] Exactly one dock icon while the app is running (L1)
- [ ] Google sign-in completes; `users/{uid}` created (L4)
- [ ] Signed-in state survives a full quit and relaunch
- [ ] "Load from cloud" restores progress onto a fresh install (L7)
- [ ] **Listen button produces audible speech** for en/de/fr/it/es/pt (L5)
- [ ] `listening.html` and `dictation.html` are usable, or clearly disabled (L5)
- [ ] Zero requests to `pagead2.googlesyndication.com` on any page (L3)
- [ ] Offline launch reaches cached content with the network disabled
- [ ] No page scrolls vertically at 1000×800; header and nav stay pinned (L8)
- [ ] SEO copy is hidden on study pages, present on About and Help (L8)
- [ ] Runs under Wayland and X11, at 100 % and fractional scaling
- [ ] Runs on a distribution older than the build host
- [ ] AppImage runs on a system without `libfuse2` installed, or fails clearly
- [ ] `.deb` and `.rpm` install and uninstall cleanly
- [ ] Keyboard accelerators work; full keyboard traversal is possible (L11)
- [ ] Both light and dark themes render with a visible focus indicator

---

## 10. Update workflow after launch

Because the host loads the live site, **web deploys reach users immediately
with no rebuild.** A new Linux release is only needed when:

- the host itself changes — navigation policy, TTS bridge, menus
- `bootstrap.js` changes
- icons, `.desktop`, or MetaInfo change
- a dependency or the bundled WebKitGTK needs updating

That last item is the one to watch. The AppImage bundles its own WebKitGTK, so
**you own its security updates for that artefact** — the same obligation that
argued against Electron in the Windows track, in a smaller form. The `.deb` and
`.rpm` inherit distro updates and do not carry it. Track WebKitGTK advisories
and rebuild the AppImage when they land.

Each release must add a `<release>` entry to the MetaInfo file, or Flathub and
software-centre validation will fail. Generate it in CI from the git tag rather
than editing it by hand.

---

## 11. Sequencing note

Linux is the **cheapest and fastest remaining track**: no account, no fee, no
signing identity, no review queue. It can ship the same week the host is
written.

It is also the natural place to prove the Tauri host before the macOS track
needs it. The two share one Rust codebase and one WebKit engine family, so
almost everything verified here — layout, storage, auth flow, `bootstrap.js`
behaviour — carries directly to macOS. Debugging that shared code is far easier
on a platform with no notarization step and no review.

**Recommended order:** Linux host first, macOS second, reusing the proven
codebase.
