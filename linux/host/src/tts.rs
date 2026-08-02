//! **L5** — WebKitGTK has no Web Speech API, so the trainer's pronunciation
//! button does nothing on Linux. This bridges it to the desktop's own speech
//! stack.
//!
//! `speech-dispatcher` is preferred because it is what every Linux screen reader
//! already drives: it fronts espeak-ng, Festival and Pico, honours the user's
//! configured voice, and is present on Ubuntu, Fedora and Arch by default.
//! `espeak-ng` is the fallback for minimal installs.
//!
//! Both are invoked through `std::process::Command` with one argument per
//! `.arg()` call. No shell is involved anywhere in this module, so page-supplied
//! text cannot become shell syntax.

use std::process::{Child, Command, Stdio};
use std::sync::Mutex;

/// Text longer than this is refused rather than truncated. Nothing the app
/// pronounces is a fraction of it; a larger value would only be useful to
/// someone trying to wedge the speech daemon.
const MAX_TEXT_LEN: usize = 4000;

/// The espeak-ng fallback gives us a process to kill; speech-dispatcher is
/// cancelled with `spd-say -C` and needs no handle.
static CURRENT_CHILD: Mutex<Option<Child>> = Mutex::new(None);

#[derive(Clone, Copy, PartialEq, Eq)]
enum Backend {
    SpeechDispatcher,
    EspeakNg,
    None,
}

impl Backend {
    fn detect() -> Self {
        if in_path("spd-say") {
            Backend::SpeechDispatcher
        } else if in_path("espeak-ng") || in_path("espeak") {
            Backend::EspeakNg
        } else {
            Backend::None
        }
    }

    fn name(self) -> &'static str {
        match self {
            Backend::SpeechDispatcher => "speech-dispatcher",
            Backend::EspeakNg => "espeak-ng",
            Backend::None => "none",
        }
    }
}

/// Resolve a binary against `PATH` without shelling out to `which`.
fn in_path(binary: &str) -> bool {
    std::env::var_os("PATH")
        .map(|paths| {
            std::env::split_paths(&paths).any(|dir| {
                let candidate = dir.join(binary);
                candidate.is_file()
            })
        })
        .unwrap_or(false)
}

/// Reduce a BCP-47 tag to the primary subtag speech-dispatcher expects
/// (`en-US` -> `en`), rejecting anything that is not a plain language tag.
fn primary_subtag(lang: &str) -> Option<String> {
    let tag = lang.split(['-', '_']).next()?;
    let ok = (2..=3).contains(&tag.len()) && tag.chars().all(|c| c.is_ascii_alphabetic());
    ok.then(|| tag.to_ascii_lowercase())
}

/// Web `rate` is 0.1..10 centred on 1. speech-dispatcher is -100..100 centred
/// on 0. The app only ever uses 0.7 and 1.0, so the mapping is kept linear and
/// gentle rather than logarithmic.
fn spd_rate(rate: f32) -> i32 {
    (((rate.clamp(0.1, 10.0) - 1.0) * 50.0).round() as i32).clamp(-100, 100)
}

/// Web `pitch` is 0..2 centred on 1.
fn spd_pitch(pitch: f32) -> i32 {
    (((pitch.clamp(0.0, 2.0) - 1.0) * 100.0).round() as i32).clamp(-100, 100)
}

/// espeak-ng speaks in words per minute; 175 is its default.
fn espeak_wpm(rate: f32) -> i32 {
    ((175.0 * rate.clamp(0.1, 10.0)).round() as i32).clamp(80, 450)
}

/// espeak-ng pitch is 0..99, default 50.
fn espeak_pitch(pitch: f32) -> i32 {
    ((pitch.clamp(0.0, 2.0) * 50.0).round() as i32).clamp(0, 99)
}

fn kill_current() {
    if let Ok(mut guard) = CURRENT_CHILD.lock() {
        if let Some(mut child) = guard.take() {
            let _ = child.kill();
            let _ = child.wait();
        }
    }
}

/// Run a command detached, reaping it on a helper thread so the process table
/// does not fill with zombies over a long study session.
fn spawn_detached(mut command: Command) -> Result<(), String> {
    command.stdin(Stdio::null()).stdout(Stdio::null()).stderr(Stdio::null());
    let child = command.spawn().map_err(|e| e.to_string())?;

    std::thread::spawn(move || {
        let mut child = child;
        let _ = child.wait();
    });

    Ok(())
}

/// Which backend the host will use. The web layer calls this once so it can
/// leave the pronunciation button disabled rather than failing on click.
#[tauri::command]
pub fn tts_backend() -> &'static str {
    Backend::detect().name()
}

#[tauri::command]
pub fn tts_speak(text: String, lang: String, rate: f32, pitch: f32) -> Result<(), String> {
    if text.trim().is_empty() {
        return Err("empty text".into());
    }
    if text.len() > MAX_TEXT_LEN {
        return Err(format!("text exceeds {MAX_TEXT_LEN} bytes"));
    }

    let language = primary_subtag(&lang).ok_or_else(|| format!("unsupported language tag: {lang}"))?;

    match Backend::detect() {
        Backend::SpeechDispatcher => {
            let mut command = Command::new("spd-say");
            command
                .arg("-l")
                .arg(&language)
                .arg("-r")
                .arg(spd_rate(rate).to_string())
                .arg("-p")
                .arg(spd_pitch(pitch).to_string())
                // Everything after -- is the message, so text starting with a
                // dash cannot be read as an option.
                .arg("--")
                .arg(&text);
            spawn_detached(command)
        }
        Backend::EspeakNg => {
            kill_current();

            let binary = if in_path("espeak-ng") { "espeak-ng" } else { "espeak" };
            let mut command = Command::new(binary);
            command
                .arg("-v")
                .arg(&language)
                .arg("-s")
                .arg(espeak_wpm(rate).to_string())
                .arg("-p")
                .arg(espeak_pitch(pitch).to_string())
                .arg("--")
                .arg(&text)
                .stdin(Stdio::null())
                .stdout(Stdio::null())
                .stderr(Stdio::null());

            let child = command.spawn().map_err(|e| e.to_string())?;
            if let Ok(mut guard) = CURRENT_CHILD.lock() {
                *guard = Some(child);
            }
            Ok(())
        }
        Backend::None => Err(
            "no speech backend found — install speech-dispatcher (recommended) or espeak-ng".into(),
        ),
    }
}

#[tauri::command]
pub fn tts_cancel() -> Result<(), String> {
    match Backend::detect() {
        Backend::SpeechDispatcher => {
            let mut command = Command::new("spd-say");
            command.arg("-C");
            spawn_detached(command)
        }
        Backend::EspeakNg => {
            kill_current();
            Ok(())
        }
        Backend::None => Ok(()),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn language_tags_are_reduced_and_validated() {
        assert_eq!(primary_subtag("en-US").as_deref(), Some("en"));
        assert_eq!(primary_subtag("pt_BR").as_deref(), Some("pt"));
        assert_eq!(primary_subtag("de").as_deref(), Some("de"));

        // Anything that could smuggle an option or a path is rejected.
        assert_eq!(primary_subtag("-l"), None);
        assert_eq!(primary_subtag("../../etc/passwd"), None);
        assert_eq!(primary_subtag(""), None);
        assert_eq!(primary_subtag("english"), None);
    }

    #[test]
    fn rate_and_pitch_stay_inside_backend_ranges() {
        assert_eq!(spd_rate(1.0), 0);
        assert_eq!(spd_rate(0.7), -15);
        assert_eq!(spd_rate(1000.0), 100);
        assert_eq!(spd_pitch(1.0), 0);
        assert_eq!(espeak_wpm(1.0), 175);
        assert!((80..=450).contains(&espeak_wpm(0.01)));
        assert!((0..=99).contains(&espeak_pitch(99.0)));
    }
}
