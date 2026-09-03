/*! Top Words (udsp) — Copyright 2026 Bulent Ozkir, Ahmet Arda Ozkir, Halit Eren Ozkir
 * Licensed under the PolyForm Noncommercial License 1.0.0 — NONCOMMERCIAL USE ONLY.
 * <https://polyformproject.org/licenses/noncommercial/1.0.0>
 *
 * Any commercial use requires prior written permission from the copyright
 * holders. Written permission from any ONE of bulentozkir@hotmail.com,
 * bulentozkir@gmail.com, ahmetardaozkir@gmail.com or haliterenozkir@gmail.com
 * is sufficient and binding on all of them.
 *
 * Required Notice: Copyright 2026 Bulent Ozkir, Ahmet Arda Ozkir, Halit Eren
 * Ozkir (https://udsp.vercel.app)
 * Full terms: see LICENSE and NOTICE in this repository.
 */

/* Top Words — Profile page rendering (profile.html only).
 * Talks only to window.TWAuth (firebase-client.js); never touches the
 * Firebase SDK directly. Renders three states: not-configured (placeholder
 * firebase-config.js values), signed-out (Google sign-in), signed-in
 * (profile summary + cloud-sync buttons + delete-profile danger zone).
 */
"use strict";
(function () {
  function $(id) {
    return document.getElementById(id);
  }
  function esc(s) {
    return String(s).replace(/[&<>"]/g, function (c) {
      return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c];
    });
  }
  function lsGet(k, d) {
    try {
      var v = JSON.parse(localStorage.getItem(k));
      return v === null || v === undefined ? d : v;
    } catch (e) {
      return d;
    }
  }
  function lsSet(k, v) {
    try {
      localStorage.setItem(k, JSON.stringify(v));
    } catch (e) {
      /* ignore storage errors (private mode) */
    }
  }

  // Default start page: which page the Home dashboard's "Continue" button
  // opens (see home.js's startPageInfo()) -- kept here too since profile.js
  // never loads home.js (each page is its own standalone script, same
  // precedent as the KNOWN_KEY/FAV_KEY/etc. constants duplicated across
  // shared.js/home.js/firebase-client.js).
  var START_PAGE_KEY = "udsp_start_page_v1";
  var START_PAGES = [
    { value: "index.html", tr: "Kelime Kartları", en: "Flashcards" },
    { value: "quiz.html", tr: "Quiz", en: "Quiz" },
    { value: "games.html", tr: "Oyunlar", en: "Games" },
    { value: "wordlist.html", tr: "Kelime Listesi", en: "Word List" },
    { value: "stats.html", tr: "İstatistikler", en: "Stats" },
    { value: "wordmorph.html", tr: "Word Morph", en: "Word Morph" },
  ];

  // Appearance: dark (default, matches every existing user's current look --
  // this key simply doesn't exist yet on their device, so lsGet's fallback of
  // "dark" keeps them looking exactly as before) or light. Applied via a
  // `data-theme="light"` attribute on <html>; a tiny inline script in every
  // page's <head> (see the bulk edit across all 24 HTML files) reads this
  // SAME key and sets the attribute BEFORE first paint so there's no flash
  // of the wrong theme on pages other than this one. Deliberately a plain
  // localStorage-only device preference, NOT synced to the Firebase profile
  // (same category as an OS-level display setting -- unlike start-page/
  // autosave, which mirror actual study progress across devices).
  var THEME_KEY = "udsp_theme_v1";

  // Daily streak + known/favourite counts -- moved here from the header
  // (was a small "🔥 N" chip on all 17 study pages, replaced by the header
  // profile icon, see styles.css/.profile-icon-link). Pure local read, shown
  // in all 3 states since it doesn't need Firebase to be meaningful.
  var STREAK_KEY = "udsp_streak_v1";
  var KNOWN_KEY = "udsp_known_v1";
  var FAV_KEY = "udsp_fav_v1";
  var PROFILE_LINKED_KEY = "udsp_profile_linked_v1";

  // Autosave: every 60s, while this page is open and the user is signed in,
  // automatically push local progress to the cloud (same payload as the
  // "Save to cloud" button) -- so progress is never lost even if the user
  // forgets to sync manually. Enabled by default (a truly first-time,
  // never-toggled device reads `true` from the missing-key fallback below).
  var AUTOSAVE_KEY = "udsp_autosave_v1";
  var AUTOSAVE_LAST_KEY = "udsp_autosave_last_v1"; // last successful autosave time (HH:MM:SS), display-only
  var AUTOSAVE_INTERVAL_MS = 60000;
  var autosaveTimer = null;
  var authLoadPromise = null;
  var authObserverBound = false;

  var AUTH_SCRIPTS = [
    "https://www.gstatic.com/firebasejs/10.13.0/firebase-app-compat.js",
    "https://www.gstatic.com/firebasejs/10.13.0/firebase-auth-compat.js",
    "https://www.gstatic.com/firebasejs/10.13.0/firebase-firestore-compat.js",
    "firebase-client.js",
  ];

  function loadScript(src) {
    return new Promise(function (resolve, reject) {
      var script = document.createElement("script");
      script.src = src;
      script.onload = resolve;
      script.onerror = function () {
        reject(new Error("Could not load " + src));
      };
      document.head.appendChild(script);
    });
  }

  function ensureAuthLoaded() {
    if (window.TWAuth) return Promise.resolve();
    if (authLoadPromise) return authLoadPromise;
    authLoadPromise = AUTH_SCRIPTS.reduce(function (chain, src) {
      return chain.then(function () {
        return loadScript(src);
      });
    }, Promise.resolve()).then(function () {
      if (!window.TWAuth) throw new Error("Firebase authentication could not start.");
    });
    return authLoadPromise;
  }

  // Progress reset ("RESET · SIFIRLA"): mirrors stats.js's existing "Reset
  // all progress" button (same key list) but ALSO sweeps every per-game
  // best-score key by PREFIX instead of enumerating lang/level combinations
  // by hand -- profile.js is standalone and has no access to shared.js's
  // LANG_ORDER/LANGS config, so this is both simpler and more robust (auto-
  // covers any future language/level without needing this list touched).
  var PROGRESS_KEYS = [
    "udsp_known_v1",
    "udsp_fav_v1",
    "udsp_srs_v1",
    "udsp_stats_v1",
    "udsp_streak_v1",
    "udsp_daily_v1",
    "udsp_resume_v2",
    "udsp_history_v1",
  ];
  var PROGRESS_PREFIXES = [
    "udsp_best_scores_", // Quiz
    "udsp_matrix_best_", // Word Matrix
    "udsp_memory_best_", // Matching Pairs
    "udsp_speedround_best_", // Speed Round
    "udsp_survival_best_", // Survival Streak
    "udsp_truefalse_best_", // True/False Blitz
    "udsp_wordrace_best_", // Word Race
    "udsp_bingo_best_", // Word Bingo
    "udsp_oddoneout_best_", // Odd One Out
    "udsp_wordclass_best_", // Word Class
    "udsp_wordguess_best_", // Word Guess
    "udsp_categorysprint_best_", // Category Sprint
    "udsp_cluedetective_best_", // Clue Detective
    "udsp_listeningchoice_best_", // Listening Choice
    "udsp_bossrush_best_", // Boss Rush
    "udsp_dailychallenge_streak_", // Daily Challenge streak
    "udsp_dailychallenge_state_", // Daily Challenge puzzle state
    "udsp_scrabble_best_", // Scrabble
    "udsp_crossword_best_", // Crossword
    "udsp_codycross_best_", // CodyCross
  ];
  function resetAllProgress() {
    PROGRESS_KEYS.forEach(function (k) {
      try {
        localStorage.removeItem(k);
      } catch (e) {
        /* ignore */
      }
    });
    try {
      Object.keys(localStorage).forEach(function (k) {
        var isProgressKey = PROGRESS_PREFIXES.some(function (p) {
          return k.indexOf(p) === 0;
        });
        if (isProgressKey) localStorage.removeItem(k);
      });
    } catch (e) {
      /* ignore storage errors (private mode) */
    }
  }

  function renderStreakSection() {
    var streak = lsGet(STREAK_KEY, { current: 0, longest: 0, last: "" });
    var known = lsGet(KNOWN_KEY, {});
    var fav = lsGet(FAV_KEY, {});
    var html = '<section class="home-hero">';
    html += '<div class="profile-row-head">';
    html += '<h2 class="home-section-title">🔥 Günlük seri · Daily streak</h2>';
    html +=
      '<a class="profile-history-link" href="history.html" data-tip="' +
      esc("Geçmiş sayfasında çalıştığın kelimeleri, quizleri ve daha fazlasını gör. · See studied words, completed quizzes and more on the History page.") +
      '">📜 Geçmiş · History</a>';
    html += "</div>";
    html += '<div class="home-stat-row">';
    html +=
      '<div class="home-stat"><span class="home-stat-num">🔥 ' + (streak.current || 0) +
      '</span><span class="home-stat-label">günlük seri · day streak</span></div>';
    html +=
      '<div class="home-stat"><span class="home-stat-num">✓ ' + Object.keys(known).length +
      '</span><span class="home-stat-label">bilinen · known</span></div>';
    html +=
      '<div class="home-stat"><span class="home-stat-num">★ ' + Object.keys(fav).length +
      '</span><span class="home-stat-label">favori · saved</span></div>';
    html += "</div>";
    html += "</section>";
    return html;
  }

  function renderSettingsSection() {
    var current = lsGet(START_PAGE_KEY, "index.html");
    var autosaveEnabled = lsGet(AUTOSAVE_KEY, true);
    var lastSync = lsGet(AUTOSAVE_LAST_KEY, "");
    var html = '<section class="home-hero">';
    html += '<h2 class="home-section-title">⚙️ Ayarlar · Settings</h2>';

    html += '<div class="profile-setting-row">';
    html += '<label class="profile-checkbox-row profile-setting-label">';
    html += '<input type="checkbox" id="profile-autosave-toggle"' + (autosaveEnabled ? " checked" : "") + " />";
    html += "<span>Otomatik kaydet · Autosave</span>";
    html +=
      '<span class="info-tip" tabindex="0" data-tip="' +
      esc(
        "İlerlemeni (bilinen/favori kelimeler, seri) bu profil sayfası açıkken her dakika otomatik olarak buluta kaydeder. · Automatically saves your progress (known/favorite words, streak) to the cloud every minute while this profile page stays open."
      ) +
      '">ⓘ</span>';
    html += "</label>";
    html += "</div>";
    html +=
      '<p id="profile-autosave-status" class="home-goal' + (lastSync ? " ok" : "") + '">' +
      (lastSync
        ? "✓ Son kayıt · Last saved: " + esc(lastSync)
        : "⏳ Henüz otomatik kaydedilmedi · Not autosaved yet") +
      "</p>";

    html += '<div class="profile-setting-row">';
    html += '<span class="profile-setting-label">Başlangıç sayfası · Start page';
    html +=
      '<span class="info-tip" tabindex="0" data-tip="' +
      esc(
        "Ana sayfadaki “Devam et” butonu hangi sayfayı açsın? · Which page should the “Continue” button on Home open?"
      ) +
      '">ⓘ</span></span>';
    html += '<select id="profile-start-page" class="profile-select" aria-label="Default start page">';
    START_PAGES.forEach(function (p) {
      html +=
        '<option value="' + p.value + '"' + (p.value === current ? " selected" : "") + ">" +
        esc(p.tr) + " · " + esc(p.en) + "</option>";
    });
    html += "</select>";
    html += "</div>";
    html += '<p id="profile-start-page-msg" class="home-goal ok" hidden>✓ Kaydedildi · Saved</p>';

    html += '<div class="profile-setting-row">';
    html += '<span class="profile-setting-label">Görünüm · Appearance';
    html +=
      '<span class="info-tip" tabindex="0" data-tip="' +
      esc(
        "Siteyi karanlık veya aydınlık temada görüntüle -- tarayıcının üzerinde bu cihaz için hatırlanır. · Show the site in dark or light theme -- remembered on this device only."
      ) +
      '">ⓘ</span></span>';
    html += '<select id="profile-theme-select" class="profile-select" aria-label="Appearance theme">';
    html += '<option value="dark"' + (lsGet(THEME_KEY, "dark") === "dark" ? " selected" : "") + ">🌙 Karanlık · Dark</option>";
    html += '<option value="light"' + (lsGet(THEME_KEY, "dark") === "light" ? " selected" : "") + ">☀️ Aydınlık · Light</option>";
    html += "</select>";
    html += "</div>";

    html += '<div class="profile-setting-row">';
    html += '<span class="profile-setting-label">Uygulama dili · App language';
    html +=
      '<span class="info-tip" tabindex="0" data-tip="' +
      esc(
        "Menüleri ve araç çubuklarını seçtiğin dilde gösterir. Kelime içeriği (Cards/Quiz/Words) ayrı bir ayardır ve bundan etkilenmez. · Shows menus and toolbars in your chosen language. Vocabulary content (Cards/Quiz/Words) is a separate setting and is not affected."
      ) +
      '">ⓘ</span></span>';
    html += '<select id="profile-ui-lang" class="profile-select" aria-label="App language">';
    var uiLangCurrent = window.UdspI18n ? UdspI18n.get() : "tr";
    (window.UdspI18n ? UdspI18n.LANGS : ["tr", "en", "de", "fr", "es", "pt", "nl"]).forEach(function (code) {
      var meta = (window.UdspI18n && UdspI18n.LANG_META[code]) || { flag: "", name: code };
      html +=
        '<option value="' + code + '"' + (code === uiLangCurrent ? " selected" : "") + ">" +
        esc(meta.flag + " " + meta.name) + "</option>";
    });
    html += "</select>";
    html += "</div>";

    html += "</section>";
    return html;
  }

  function wireStartPageSection() {
    var sel = $("profile-start-page");
    if (!sel) return;
    sel.addEventListener("change", function () {
      if (window.TWAuth && window.TWAuth.saveStartPage) {
        window.TWAuth.saveStartPage(sel.value);
      } else {
        lsSet(START_PAGE_KEY, sel.value);
      }
      var msg = $("profile-start-page-msg");
      if (msg) {
        msg.hidden = false;
        clearTimeout(msg._hideTimer);
        msg._hideTimer = setTimeout(function () {
          msg.hidden = true;
        }, 2000);
      }
    });
  }

  // Applies the theme IMMEDIATELY on the current page (no reload needed) by
  // toggling the same data-theme attribute + theme-color meta tag that each
  // page's early inline <head> script sets on load -- so switching here is an
  // instant live preview, not just a "takes effect next visit" setting.
  function applyTheme(theme) {
    if (theme === "light") {
      document.documentElement.setAttribute("data-theme", "light");
    } else {
      document.documentElement.removeAttribute("data-theme");
    }
    var m = document.querySelector('meta[name="theme-color"]');
    if (m) m.setAttribute("content", theme === "light" ? "#f8fafc" : "#0f172a");
  }

  function wireThemeSection() {
    var sel = $("profile-theme-select");
    if (!sel) return;
    sel.addEventListener("change", function () {
      lsSet(THEME_KEY, sel.value);
      applyTheme(sel.value);
    });
  }

  // App-CHROME language (menus/toolbars), NOT the vocabulary-content "Lang"
  // selector elsewhere in the app -- delegates entirely to i18n.js so this
  // page doesn't duplicate its dictionary or persistence.
  function wireLanguageSection() {
    var sel = $("profile-ui-lang");
    if (!sel || !window.UdspI18n) return;
    sel.value = UdspI18n.get();
    sel.addEventListener("change", function () {
      UdspI18n.set(sel.value);
    });
  }

  function wireAutosaveSection() {
    var cb = $("profile-autosave-toggle");
    if (!cb) return;
    cb.addEventListener("change", function () {
      if (window.TWAuth && window.TWAuth.saveAutosavePref) {
        window.TWAuth.saveAutosavePref(cb.checked);
      } else {
        lsSet(AUTOSAVE_KEY, cb.checked);
      }
      scheduleAutosaveTick();
    });
  }

  function formatClock(d) {
    function pad(n) {
      return n < 10 ? "0" + n : "" + n;
    }
    return pad(d.getHours()) + ":" + pad(d.getMinutes()) + ":" + pad(d.getSeconds());
  }

  function runAutosaveTick() {
    if (!lsGet(AUTOSAVE_KEY, true)) return Promise.resolve();
    if (!window.TWAuth || !window.TWAuth.currentUser || !window.TWAuth.currentUser()) {
      return Promise.resolve();
    }
    return window.TWAuth.saveProfileFromLocal()
      .then(function () {
        var t = formatClock(new Date());
        lsSet(AUTOSAVE_LAST_KEY, t);
        var status = $("profile-autosave-status");
        if (status) {
          status.textContent = "✓ Son kayıt · Last saved: " + t;
          status.classList.add("ok");
        }
      })
      .catch(function () {
        /* silent -- transient network errors shouldn't nag every minute; the
           next tick retries automatically. */
      });
  }

  function scheduleAutosaveTick() {
    if (autosaveTimer) clearTimeout(autosaveTimer);
    autosaveTimer = null;
    if (document.visibilityState === "hidden" || !lsGet(AUTOSAVE_KEY, true)) return;
    if (!window.TWAuth || !window.TWAuth.currentUser || !window.TWAuth.currentUser()) return;
    autosaveTimer = setTimeout(function () {
      autosaveTimer = null;
      runAutosaveTick().then(scheduleAutosaveTick, scheduleAutosaveTick);
    }, AUTOSAVE_INTERVAL_MS);
  }

  document.addEventListener("visibilitychange", scheduleAutosaveTick);

  function renderDangerZoneSection(isSignedIn) {
    var html = '<section class="home-hero">';
    html += '<h2 class="home-section-title">⚠️ Tehlikeli bölge · Danger zone</h2>';

    html += '<div class="profile-danger-row">';
    html += '<span class="profile-setting-label">İlerlemeyi sıfırla · Reset progress';
    html +=
      '<span class="info-tip" tabindex="0" data-tip="' +
      esc(
        "Bilinen/favori kelimeleri, seriyi, istatistikleri ve en iyi skorları bu cihazdan kalıcı olarak siler. · Permanently deletes known/favorite words, streak, stats, and best scores from this device."
      ) +
      '">ⓘ</span></span>';
    html += '<button type="button" class="home-lang-btn is-danger profile-danger-btn" id="profile-reset-btn">🔄 SIFIRLA · RESET</button>';
    html += "</div>";

    if (isSignedIn) {
      html += '<div class="profile-danger-row">';
      html += '<span class="profile-setting-label">Profilimi sil · Delete my profile';
      html +=
        '<span class="info-tip" tabindex="0" data-tip="' +
        esc(
          "Profilini silmek yalnızca buluttaki kaydını kaldırır; bu cihazdaki yerel ilerlemen etkilenmez, hesabın kalır. · Deleting your profile only removes the cloud record; local progress on this device is unaffected and your account stays."
        ) +
        '">ⓘ</span></span>';
      html += '<button type="button" class="home-lang-btn is-danger profile-danger-btn" id="profile-delete">🗑️ Sil · Delete</button>';
      html += "</div>";

      html += '<div class="profile-danger-row">';
      html += '<span class="profile-setting-label">Hesabımı tamamen sil · Delete my account';
      html +=
        '<span class="info-tip" tabindex="0" data-tip="' +
        esc(
          "Bu işlem bulut profilini VE giriş hesabını kalıcı olarak siler — geri alınamaz. Yakın zamanda giriş yapmadıysan önce tekrar giriş yapman istenebilir. · This permanently deletes both your cloud profile AND your login account — this cannot be undone. If you haven’t signed in recently, you may be asked to sign in again first."
        ) +
        '">ⓘ</span></span>';
      html += '<button type="button" class="home-lang-btn is-danger profile-danger-btn" id="profile-delete-account">⛔ Sil · Delete</button>';
      html += "</div>";
    }

    html += "</section>";
    return html;
  }

  function wireResetSection() {
    var btn = $("profile-reset-btn");
    if (!btn) return;
    btn.addEventListener("click", function () {
      var ok1 = confirm(
        "İlerlemeni sıfırlamak istediğine emin misin? Bu; bilinen/favori kelimeleri, seriyi, istatistikleri ve en iyi skorları siler. · Are you sure you want to reset your progress? This clears known/favorite words, streak, stats, and best scores."
      );
      if (!ok1) return;
      var ok2 = confirm(
        "Son kez soruyoruz — emin misin? Bu işlem GERİ ALINAMAZ. · One last time — are you sure? This action CANNOT BE UNDONE."
      );
      if (!ok2) return;
      resetAllProgress();
      alert("✓ İlerleme sıfırlandı. · Progress has been reset.");
      location.reload();
    });
  }

  var PROVIDER_ORDER = ["google"];
  // Official Google "G" logo (Google's own multi-color brand mark for
  // "Sign in with Google" buttons) inlined as SVG -- replaces the old "🔴"
  // emoji placeholder. Not escaped via esc() since this is a fixed
  // internal constant, not user input, same as the emoji it replaces.
  var GOOGLE_ICON =
    '<svg class="provider-icon" width="18" height="18" viewBox="0 0 18 18" aria-hidden="true" focusable="false">' +
    '<path fill="#4285F4" d="M17.64 9.2c0-.637-.057-1.251-.164-1.84H9v3.481h4.844c-.209 1.125-.843 2.078-1.796 2.717v2.258h2.908c1.702-1.567 2.684-3.874 2.684-6.615z"/>' +
    '<path fill="#34A853" d="M9 18c2.43 0 4.467-.806 5.956-2.18l-2.908-2.259c-.806.54-1.837.86-3.048.86-2.344 0-4.328-1.584-5.036-3.711H.957v2.332C2.438 15.983 5.482 18 9 18z"/>' +
    '<path fill="#FBBC05" d="M3.964 10.71c-.18-.54-.282-1.117-.282-1.71s.102-1.17.282-1.71V4.958H.957C.347 6.173 0 7.548 0 9s.348 2.827.957 4.042l3.007-2.332z"/>' +
    '<path fill="#EA4335" d="M9 3.58c1.321 0 2.508.454 3.44 1.345l2.582-2.58C13.463.891 11.426 0 9 0 5.482 0 2.438 2.017.957 4.958L3.964 7.29C4.672 5.163 6.656 3.58 9 3.58z"/>' +
    "</svg>";
  var PROVIDER_LABEL = {
    google: GOOGLE_ICON + " Google",
  };

  function configIsPlaceholder() {
    var c = window.FIREBASE_CONFIG || {};
    return !c.apiKey || /REPLACE_ME/.test(c.apiKey);
  }

  function renderNotConfigured(root) {
    root.innerHTML =
      '<div class="profile-grid">' +
      '<section class="home-hero">' +
      '<h1 class="home-title">👤 Profil · Profile</h1>' +
      '<p class="home-sub">Firebase henüz yapılandırılmadı — <code>firebase-config.js</code> içindeki REPLACE_ME değerlerini Firebase Console’dan alıp doldurun. · ' +
      "Firebase isn’t configured yet — fill in the REPLACE_ME values in firebase-config.js from the Firebase Console.</p>" +
      "</section>" +
      renderStreakSection() +
      renderSettingsSection() +
      renderDangerZoneSection(false) +
      "</div>";
    wireStartPageSection();
    wireAutosaveSection();
    wireThemeSection();
    wireLanguageSection();
    wireResetSection();
  }

  function renderSignedOut(root) {
    var html = '<div class="profile-grid">';
    html += '<section class="home-hero">';
    html += '<h1 class="home-title">👤 Profil · Profile ' +
      '<span class="info-tip" tabindex="0" data-tip="' +
      esc("İlerlemeni bulutta saklamak için giriş yap. · Sign in to store your progress in the cloud.") +
      '">ⓘ</span></h1>';
    html += '<div class="home-langs">';
    PROVIDER_ORDER.forEach(function (id) {
      html += '<button type="button" class="home-lang-btn" data-provider="' + id + '">' + PROVIDER_LABEL[id] + "</button>";
    });
    html += "</div>";
    html += '<p id="profile-msg" class="home-goal" hidden></p>';
    html += "</section>";
    html += renderStreakSection();
    html += renderSettingsSection();
    html += renderDangerZoneSection(false);
    html += "</div>";
    root.innerHTML = html;

    Array.prototype.forEach.call(root.querySelectorAll("[data-provider]"), function (b) {
      b.addEventListener("click", function () {
        var msg = $("profile-msg");
        msg.hidden = true;
        msg.classList.remove("err");
        b.disabled = true;
        ensureAuthLoaded()
          .then(function () {
            bindAuthObserver(root);
            return window.TWAuth.signIn(b.getAttribute("data-provider"));
          })
          .catch(function (err) {
            var currentMsg = $("profile-msg");
            if (!currentMsg) return;
            currentMsg.textContent = "Giriş başarısız · Sign-in failed: " + (err && err.message ? err.message : err);
            currentMsg.classList.add("err");
            currentMsg.hidden = false;
          })
          .then(function () {
            if (b.isConnected) b.disabled = false;
          });
      });
    });
    wireStartPageSection();
    wireAutosaveSection();
    wireThemeSection();
    wireLanguageSection();
    wireResetSection();
  }

  function renderSignedIn(root, user) {
    var html = '<div class="profile-grid">';
    html += '<section class="home-hero">';
    html += '<h1 class="home-title">👋 ' + esc(user.displayName || user.email || "Kullanıcı") + "</h1>";
    if (user.email) html += '<p class="home-sub">' + esc(user.email) + "</p>";
    html += '<div class="home-langs">';
    html += '<button type="button" class="home-lang-btn" id="profile-sync-down">⬇️ Buluttan al · Load from cloud</button>';
    html += '<button type="button" class="home-lang-btn" id="profile-sync-up">⬆️ Buluta kaydet · Save to cloud</button>';
    html += '<button type="button" class="home-lang-btn" id="profile-signout">🚪 Çıkış · Sign out</button>';
    html += "</div>";
    html += '<p id="profile-msg" class="home-goal" hidden></p>';
    html += "</section>";
    html += renderStreakSection();
    html += renderSettingsSection();
    html += renderDangerZoneSection(true);
    html += "</div>";
    root.innerHTML = html;

    function showMsg(text, isErr) {
      var el = $("profile-msg");
      el.textContent = text;
      el.classList.toggle("err", !!isErr);
      el.classList.toggle("ok", !isErr);
      el.hidden = false;
    }

    $("profile-sync-down").addEventListener("click", function () {
      window.TWAuth.loadProfileToLocal()
        .then(function () {
          showMsg("✓ Bulut verisi bu cihaza yüklendi. · Cloud data loaded onto this device.", false);
        })
        .catch(function (e) {
          showMsg("Hata · Error: " + e.message, true);
        });
    });
    $("profile-sync-up").addEventListener("click", function () {
      window.TWAuth.saveProfileFromLocal()
        .then(function () {
          showMsg("✓ Bu cihazın verisi buluta kaydedildi. · This device's data was saved to the cloud.", false);
        })
        .catch(function (e) {
          showMsg("Hata · Error: " + e.message, true);
        });
    });
    $("profile-signout").addEventListener("click", function () {
      window.TWAuth.signOut().then(function () {
        lsSet(PROFILE_LINKED_KEY, 0);
        location.reload();
      });
    });
    $("profile-delete").addEventListener("click", function () {
      if (!confirm("Profilini kalıcı olarak silmek istediğine emin misin? · Permanently delete your cloud profile?")) return;
      window.TWAuth.deleteProfileDoc()
        .then(function () {
          showMsg("✓ Profil silindi. · Profile deleted.", false);
        })
        .catch(function (e) {
          showMsg("Hata · Error: " + e.message, true);
        });
    });
    $("profile-delete-account").addEventListener("click", function () {
      if (!confirm("Bu, hesabını ve profilini KALICI olarak siler. Emin misin? · This will PERMANENTLY delete your account and profile. Are you sure?")) return;
      window.TWAuth.deleteAccountAndProfile()
        .then(function () {
          showMsg("✓ Hesap ve profil silindi. · Account and profile deleted.", false);
        })
        .catch(function (e) {
          if (e && e.code === "auth/requires-recent-login") {
            showMsg(
              "Güvenlik için tekrar giriş yapman gerekiyor — çıkış yapıp yeniden giriş yaptıktan sonra tekrar dene. · For security, please sign out and sign back in, then try again.",
              true
            );
          } else {
            showMsg("Hata · Error: " + (e && e.message ? e.message : e), true);
          }
        });
    });
    wireStartPageSection();
    wireAutosaveSection();
    wireThemeSection();
    wireLanguageSection();
    wireResetSection();
  }

  function bindAuthObserver(root) {
    if (authObserverBound) return;
    authObserverBound = true;
    window.TWAuth.onAuthChange(function (user) {
      if (user) {
        // Definitive proof this device has an active cloud profile -- drives
        // the header's profile-icon "is-linked" dot on the other 21 pages
        // (shared.js/moresheet.js/home.js) and silences the "create a
        // profile" reminder popover, since one already exists.
        lsSet(PROFILE_LINKED_KEY, 1);
        renderSignedIn(root, user);
      } else {
        // Clear the flag too -- Firebase just told us, authoritatively, that
        // this device is NOT currently signed in (explicit sign-out, expired
        // session, or never signed in). Without this, PROFILE_LINKED_KEY
        // stayed stuck at 1 forever after the FIRST sign-in, even across a
        // later sign-out -- so the header icon on other pages and the
        // Home-page sign-in nudge kept acting as if the device were still
        // linked. This was the actual bug behind that, not stale caching.
        lsSet(PROFILE_LINKED_KEY, 0);
        renderSignedOut(root);
      }
      scheduleAutosaveTick();
    });
  }

  function boot() {
    var root = $("profile-root");
    if (!root) return;
    if (configIsPlaceholder()) {
      renderNotConfigured(root);
      return;
    }
    if (!lsGet(PROFILE_LINKED_KEY, 0)) {
      renderSignedOut(root);
      return;
    }
    root.innerHTML = '<p class="home-sub" style="text-align:center">Yükleniyor… · Loading…</p>';
    ensureAuthLoaded()
      .then(function () {
        bindAuthObserver(root);
      })
      .catch(function () {
        lsSet(PROFILE_LINKED_KEY, 0);
        renderSignedOut(root);
      });
  }

  document.addEventListener("DOMContentLoaded", boot);
})();
