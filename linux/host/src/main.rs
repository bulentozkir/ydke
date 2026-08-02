//! Top Words — Linux host.
//!
//! A thin Tauri v2 shell around <https://udsp.vercel.app>. Deliberately thin:
//! the web app is the product, and the strategy record (`linux/02-packaging-strategy.md`
//! §5) chose a shared Rust host over a GTK-native rewrite precisely so this file
//! stays small enough to reason about.
//!
//! Three responsibilities, and no more:
//!   1. Inject the shared bootstrap and the Linux-only host script.
//!   2. Decide which top-level navigations stay in the window (everything else
//!      goes to the user's real browser through xdg-open).
//!   3. Expose the speech-dispatcher bridge the web app needs (L5).

mod adblock;
mod config;
mod tts;

use std::process::{Command, Stdio};

use tauri::WebviewWindowBuilder;

fn main() {
    tauri::Builder::default()
        .invoke_handler(tauri::generate_handler![
            open_external,
            tts::tts_backend,
            tts::tts_speak,
            tts::tts_cancel
        ])
        .setup(|app| {
            // The window geometry lives in tauri.conf.json (which declares
            // "create": false) so the values stay next to the bundle metadata,
            // but it has to be built here to attach the scripts and the
            // navigation policy.
            let window_config = app
                .config()
                .app
                .windows
                .first()
                .cloned()
                .ok_or_else(|| "tauri.conf.json declares no window".to_string())?;

            let window = WebviewWindowBuilder::from_config(app.handle(), &window_config)?
                .initialization_script(&config::bootstrap_script())
                .initialization_script(&config::host_script())
                .on_navigation(|url| {
                    if config::is_blocked(url) {
                        return false;
                    }
                    if config::is_allowed_in_app(url) {
                        return true;
                    }

                    // Unlike the Windows host, which shows the page in a
                    // stripped-down viewer window, Linux hands it to the
                    // desktop's default browser. That is both the platform
                    // convention and a requirement for any store listing: an
                    // app that renders arbitrary third-party pages in its own
                    // chrome is a browser, and gets reviewed as one.
                    let target = url.to_string();
                    std::thread::spawn(move || {
                        if let Err(err) = open_in_browser(&target) {
                            eprintln!("topwords: could not open external link: {err}");
                        }
                    });
                    false
                })
                .build()?;

            // Non-fatal: without it the app still runs, it just leaks ad
            // requests that the injected guard cannot see. See adblock.rs.
            if let Err(err) = adblock::install(&window) {
                eprintln!("topwords: network ad filter unavailable: {err}");
            }

            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("failed to start the Top Words host");
}

/// Open a link in the user's browser.
///
/// Exposed to the page as well as used by the navigation policy, so the scheme
/// check is not optional: without it, page content could invoke any registered
/// URL handler on the machine. Only http and https ever reach xdg-open, and the
/// URL is passed as a single argument to a directly-executed binary — there is
/// no shell in this path to inject into.
#[tauri::command]
fn open_external(url: String) -> Result<(), String> {
    open_in_browser(&url)
}

fn open_in_browser(raw: &str) -> Result<(), String> {
    let parsed = tauri::Url::parse(raw).map_err(|err| err.to_string())?;

    if !matches!(parsed.scheme(), "http" | "https") {
        return Err(format!("refusing to open scheme: {}", parsed.scheme()));
    }

    let child = Command::new("xdg-open")
        .arg(parsed.as_str())
        .stdin(Stdio::null())
        .stdout(Stdio::null())
        .stderr(Stdio::null())
        .spawn()
        .map_err(|err| format!("xdg-open failed: {err}"))?;

    std::thread::spawn(move || {
        let mut child = child;
        let _ = child.wait();
    });

    Ok(())
}
