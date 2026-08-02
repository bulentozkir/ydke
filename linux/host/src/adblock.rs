//! **L3 / W4** — stop ad requests before they reach the network.
//!
//! The Windows host answers every blocked pattern with an empty HTTP 204 via
//! `WebResourceRequested`. WebKitGTK's equivalent is the `send-request` signal
//! on each `WebResource`; returning `true` from it stops the load.
//!
//! Why this is behind a Cargo feature, off by default
//! --------------------------------------------------
//! Reaching the underlying `webkit2gtk::WebView` through `with_webview` requires
//! this crate to depend on the *same* `webkit2gtk` version wry does. If the two
//! resolve differently, `PlatformWebview::inner()` returns a type that is
//! nominally identical and yet will not unify, and the failure is a wall of
//! trait-resolution errors rather than anything that points at the cause.
//!
//! That pin can only be established on a machine that can actually build the
//! crate. Until someone does that on a Linux box and records the exact version
//! in Cargo.toml, the default build ships without it and relies on:
//!
//!   * the udsp repo not emitting the AdSense tag when `window.TWApp.isPackaged()`
//!     — the primary fix, and the one recorded against L3; and
//!   * the fetch / XHR / sendBeacon guards in `scripts/linux-host.js`, which
//!     catch runtime beacons but cannot catch a parser-inserted `<script src>`.
//!
//! Build with `--features webkit-filter` once the version is pinned.

#[cfg(feature = "webkit-filter")]
pub fn install(window: &tauri::WebviewWindow) -> Result<(), String> {
    use webkit2gtk::{URIRequestExt, WebResourceExt, WebViewExt};

    window
        .with_webview(|webview| {
            let webview = webview.inner();

            webview.connect_resource_load_started(|_webview, resource, _request| {
                resource.connect_send_request(|_resource, request, _redirect| {
                    let uri = match request.uri() {
                        Some(uri) => uri.to_string(),
                        None => return false,
                    };

                    match tauri::Url::parse(&uri) {
                        // true stops the load. The renderer surfaces that as a
                        // failed request, exactly as it does for the 204 on
                        // Windows: nothing is delivered either way.
                        Ok(parsed) => crate::config::is_blocked(&parsed),
                        Err(_) => false,
                    }
                });
            });
        })
        .map_err(|err| err.to_string())
}

#[cfg(not(feature = "webkit-filter"))]
pub fn install(_window: &tauri::WebviewWindow) -> Result<(), String> {
    Err("built without the webkit-filter feature".into())
}
