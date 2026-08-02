#!/usr/bin/env bash
#
# Build the Linux packages for Top Words.
#
#   ./build.sh            generate icons, build appimage + deb + rpm, validate, checksum
#   ./build.sh validate   validate the metadata only (no toolchain needed beyond appstreamcli)
#
# Build on the OLDEST glibc you intend to support. glibc is forward-compatible
# and not backward-compatible, so a binary linked on Ubuntu 24.04 will not start
# on 22.04, while the reverse works. See linux/02-packaging-strategy.md §5.
#
# Ubuntu/Debian build prerequisites:
#   sudo apt install build-essential curl wget file libssl-dev libayatana-appindicator3-dev \
#                    librsvg2-dev libwebkit2gtk-4.1-dev libxdo-dev \
#                    desktop-file-utils appstream rpm
#
# Fedora:
#   sudo dnf install @development-tools openssl-devel libappindicator-gtk3-devel \
#                    librsvg2-devel webkit2gtk4.1-devel libxdo-devel \
#                    desktop-file-utils appstream rpm-build

set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
host="$here/../host"
metainfo="$here/io.github.qlupala9p.udsp.metainfo.xml"
mode="${1:-build}"

log()  { printf '\n\033[1m==> %s\033[0m\n' "$*"; }
warn() { printf '\033[33mwarning: %s\033[0m\n' "$*" >&2; }
die()  { printf '\033[31merror: %s\033[0m\n' "$*" >&2; exit 1; }

# ---------------------------------------------------------------- metadata ---
validate_metadata() {
    log "Validating AppStream metadata"
    if command -v appstreamcli >/dev/null 2>&1; then
        # Screenshot URLs are fetched over the network. If they 404 the build is
        # not broken, but the software-centre listing will be, so this is a hard
        # failure rather than a warning.
        appstreamcli validate --explain "$metainfo"
    else
        warn "appstreamcli not installed; skipping AppStream validation"
        warn "  Debian/Ubuntu: apt install appstream    Fedora: dnf install appstream"
    fi
}

validate_desktop_entry() {
    log "Validating .desktop entry"
    local rendered
    rendered="$(find "$host/target" -name 'topwords.desktop' -path '*/bundle/*' -print -quit 2>/dev/null || true)"

    if [[ -z "$rendered" ]]; then
        warn "no rendered .desktop found under target/; skipping"
        return
    fi

    if command -v desktop-file-validate >/dev/null 2>&1; then
        desktop-file-validate "$rendered"
        echo "ok: $rendered"
    else
        warn "desktop-file-validate not installed; skipping"
    fi
}

if [[ "$mode" == "validate" ]]; then
    validate_metadata
    exit 0
fi

# ----------------------------------------------------------------- version ---
# The release entry in the metainfo has to match what is actually being shipped,
# or software centres show the wrong changelog. Drive it from the tag.
version="$(git -C "$here" describe --tags --abbrev=0 2>/dev/null | sed 's/^v//' || true)"
if [[ -n "$version" ]]; then
    log "Stamping metainfo with version $version"
    today="$(date -u +%Y-%m-%d)"
    sed -i -E "s|<release version=\"[^\"]+\" date=\"[^\"]+\">|<release version=\"$version\" date=\"$today\">|" "$metainfo"
else
    warn "no git tag found; leaving the metainfo release entry as-is"
fi

# ------------------------------------------------------------------- tools ---
command -v cargo >/dev/null 2>&1 || die "cargo not found — install Rust from https://rustup.rs"

if command -v cargo-tauri >/dev/null 2>&1; then
    tauri() { cargo tauri "$@"; }
elif command -v npx >/dev/null 2>&1; then
    tauri() { npx --yes "@tauri-apps/cli@^2" "$@"; }
else
    die "neither cargo-tauri nor npx found — run: cargo install tauri-cli --version '^2'"
fi

# ------------------------------------------------------------------- icons ---
# Not committed: they are a pure function of icon.svg, and 20-odd generated PNGs
# in review diffs are noise. tauri.conf.json references them, so this has to run
# before any cargo command, including `cargo check`.
log "Generating icons from icons/icon.svg"
( cd "$host" && tauri icon icons/icon.svg )

# ------------------------------------------------------------------- tests ---
log "Running host tests"
( cd "$host" && cargo test --release )

# ------------------------------------------------------------------- build ---
# webkit-filter is deliberately not enabled here; see host/src/adblock.rs.
log "Building bundles"
( cd "$host" && tauri build --bundles appimage,deb,rpm )

# ---------------------------------------------------------------- validate ---
validate_metadata
validate_desktop_entry

# --------------------------------------------------------------- checksums ---
# There is no code signing on Linux (no equivalent of an MSIX signature that
# users' machines check), so a published checksum is the only integrity story
# the download has. Publish this file alongside the artifacts.
log "Collecting artifacts"
bundle="$host/target/release/bundle"
out="$here/../dist"
mkdir -p "$out"

find "$bundle" -type f \( -name '*.AppImage' -o -name '*.deb' -o -name '*.rpm' \) -exec cp -f {} "$out"/ \;

( cd "$out" && sha256sum ./*.AppImage ./*.deb ./*.rpm > SHA256SUMS 2>/dev/null ) || warn "nothing to checksum"

log "Done"
ls -lh "$out"
