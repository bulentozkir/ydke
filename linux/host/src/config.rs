//! Every value that differs between the web app and the packaged Linux build.
//!
//! This is a port of `windows/src/TopWords.Windows/AppConfig.cs`. The rules are
//! deliberately identical — see `linux/02-packaging-strategy.md` §4. If you change
//! a host list here, change it there too.

/// The origin the app runs against is declared once, in `tauri.conf.json`
/// (`build.frontendDist`), and is repeated below only as a navigation rule.
///
/// **L7 / W5:** if it is ever changed to a custom domain, every user's local
/// progress resets, because `localStorage` is partitioned per origin. Do it
/// before the first public release or not at all.
///
/// There is no user-agent suffix on Linux. Appending one would mean replacing
/// WebKitGTK's entire UA string — Tauri cannot append to it — and Google's
/// sign-in pages reject user agents they do not recognise. The packaged build
/// is detected through `window.TWApp` instead, which is the contract the
/// blocker documents anyway.
///
/// Top-level navigations allowed to stay inside the app window. Anything else is
/// handed to the desktop's default browser. Subresources and iframes are unaffected.
const IN_APP_HOSTS: &[&str] = &[
    "udsp.vercel.app",
    "accounts.google.com",
    "apis.google.com",
    "*.firebaseapp.com",
    "ssl.gstatic.com",
    "www.gstatic.com",
];

/// **L3 / W4:** ad networks. AdSense inside a packaged app is an account-level
/// policy risk on every store, so the request must never leave the machine.
///
/// Deliberately excludes `accounts.google.com`, `apis.google.com`, `*.gstatic.com`
/// and `*.googleapis.com`, which sign-in and Firestore need.
pub const BLOCKED_HOSTS: &[&str] = &[
    "pagead2.googlesyndication.com",
    "*.googlesyndication.com",
    "googleads.g.doubleclick.net",
    "*.doubleclick.net",
    "adservice.google.com",
    "*.adtrafficquality.google",
];

/// Languages the trainer speaks, mapped to speech-dispatcher language codes.
/// Used to synthesise a `speechSynthesis.getVoices()` list, which WebKitGTK
/// does not provide (L5).
pub const TTS_LANGUAGES: &[(&str, &str)] = &[
    ("en-US", "en"),
    ("de-DE", "de"),
    ("fr-FR", "fr"),
    ("it-IT", "it"),
    ("es-ES", "es"),
    ("pt-PT", "pt"),
];

/// Whether a top-level navigation may stay in the app window.
pub fn is_allowed_in_app(url: &tauri::Url) -> bool {
    // Non-web schemes (about:blank, data:) are produced by the host itself.
    if url.scheme() != "http" && url.scheme() != "https" {
        return true;
    }

    match url.host_str() {
        Some(host) => IN_APP_HOSTS.iter().any(|pattern| host_matches(host, pattern)),
        None => false,
    }
}

/// Whether a request should be denied outright (L3).
pub fn is_blocked(url: &tauri::Url) -> bool {
    match url.host_str() {
        Some(host) => BLOCKED_HOSTS.iter().any(|pattern| host_matches(host, pattern)),
        None => false,
    }
}

/// Matches `AppConfig.HostMatches` in the Windows host, byte for byte:
/// a bare pattern is an exact match, `*.example.com` matches any subdomain but
/// not the apex. The leading dot in the suffix test is what makes
/// `evil-firebaseapp.com` fail against `*.firebaseapp.com`.
fn host_matches(host: &str, pattern: &str) -> bool {
    if let Some(suffix) = pattern.strip_prefix('*') {
        // "*.firebaseapp.com" -> host must end with ".firebaseapp.com".
        return host.len() > suffix.len() && host.to_ascii_lowercase().ends_with(&suffix.to_ascii_lowercase());
    }

    host.eq_ignore_ascii_case(pattern)
}

/// The bootstrap shared verbatim with the Windows host, with its platform tokens
/// resolved. See `shared/bootstrap.js`.
pub fn bootstrap_script() -> String {
    include_str!("../../../shared/bootstrap.js")
        .replace("__TW_PLATFORM__", "linux")
        .replace("__TW_IS_MSIX__", "false")
}

/// The Linux-only half: the TTS bridge and the sign-in flow fix, neither of which
/// has a Windows equivalent.
pub fn host_script() -> String {
    let voices = serde_json::to_string(TTS_LANGUAGES).unwrap_or_else(|_| "[]".into());
    let blocked = serde_json::to_string(BLOCKED_HOSTS).unwrap_or_else(|_| "[]".into());

    include_str!("../scripts/linux-host.js")
        .replace("\"__TW_VOICES__\"", &voices)
        .replace("\"__TW_BLOCKED_HOSTS__\"", &blocked)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn url(s: &str) -> tauri::Url {
        s.parse().expect("test URL should parse")
    }

    #[test]
    fn app_origin_and_auth_hosts_stay_in_app() {
        assert!(is_allowed_in_app(&url("https://udsp.vercel.app/home.html")));
        assert!(is_allowed_in_app(&url("https://accounts.google.com/o/oauth2/auth")));
        assert!(is_allowed_in_app(&url("https://udsp-9fedc.firebaseapp.com/__/auth/handler")));
    }

    #[test]
    fn everything_else_leaves_the_app() {
        // The listening page links to all of these.
        assert!(!is_allowed_in_app(&url("https://learnenglish.britishcouncil.org/")));
        assert!(!is_allowed_in_app(&url("https://www.goethe.de/en/spr/ueb/ele.html")));
        assert!(!is_allowed_in_app(&url("https://www.apache.org/licenses/LICENSE-2.0")));
    }

    #[test]
    fn ad_hosts_are_blocked_but_sign_in_is_not() {
        assert!(is_blocked(&url("https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js")));
        assert!(is_blocked(&url("https://googleads.g.doubleclick.net/pagead/id")));
        assert!(!is_blocked(&url("https://accounts.google.com/o/oauth2/auth")));
        assert!(!is_blocked(&url("https://firestore.googleapis.com/v1/projects")));
    }

    #[test]
    fn suffix_match_requires_a_dot_boundary() {
        assert!(!is_blocked(&url("https://notdoubleclick.net/")));
        assert!(!is_allowed_in_app(&url("https://evil-udsp.vercel.app.attacker.test/")));
    }
}
