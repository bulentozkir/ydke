/* Top Words — Home dashboard.
 * Reads localStorage directly (streak, resume, known, favourites) and renders
 * an orientation/dashboard screen. Deliberately does NOT load shared.js or the
 * word-data files: it only needs a handful of localStorage keys, so it stays
 * instant and avoids shared.js's "no words loaded" bootstrap guard. Keys here
 * are kept in sync with shared.js by hand (they are stable/versioned). */
"use strict";
(function () {
  var WELCOME_KEY = "udsp_welcomed_v1";
  var RESUME_KEY = "udsp_resume_v2";
  var KNOWN_KEY = "udsp_known_v1";
  var FAV_KEY = "udsp_fav_v1";
  var STREAK_KEY = "udsp_streak_v1";
  var INTRO_KEY = "udsp_intro_seen_v1";
  var GOAL_KEY = "udsp_daily_v1";
  var START_PAGE_KEY = "udsp_start_page_v1"; // set from profile.html
  var PROFILE_LINKED_KEY = "udsp_profile_linked_v1"; // set by profile.js on sign-in

  function get(k, d) {
    try {
      var v = JSON.parse(localStorage.getItem(k));
      return v === null || v === undefined ? d : v;
    } catch (e) {
      return d;
    }
  }
  function set(k, v) {
    try {
      localStorage.setItem(k, JSON.stringify(v));
    } catch (e) {}
  }
  function esc(s) {
    return String(s).replace(/[&<>"]/g, function (c) {
      return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c];
    });
  }
  function todayStr() {
    var d = new Date();
    return d.getFullYear() + "-" + (d.getMonth() + 1) + "-" + d.getDate();
  }

  var LANG_NAME = { en: "English", de: "German", fr: "French", it: "Italian", es: "Spanish", pt: "Portuguese" };
  var LANG_TR = { en: "İngilizce", de: "Almanca", fr: "Fransızca", it: "İtalyanca", es: "İspanyolca", pt: "Portekizce" };
  var LANG_FLAG = { en: "🇬🇧", de: "🇩🇪", fr: "🇫🇷", it: "🇮🇹", es: "🇪🇸", pt: "🇵🇹" };
  var DEFAULT_LEVEL = { en: "B2", de: "GA1", fr: "A1", it: "A1", es: "A1", pt: "A1" };
  var GOAL_OPTIONS = [
    { value: 5, tr: "Rahat", en: "Relaxed", mins: 5 },
    { value: 15, tr: "Düzenli", en: "Regular", mins: 15 },
    { value: 30, tr: "Yoğun", en: "Intense", mins: 30 },
  ];
  var MODE_OPTIONS = [
    { value: "learn", tr: "Ezber", en: "Memorize", desc: "Kelime kartları · Flashcards" },
    { value: "practice", tr: "Pratik", en: "Practice", desc: "Test ve oyunlar · Quizzes & games" },
  ];
  var selectedLang = null;
  var selectedGoal = 15;
  var selectedMode = "learn";
  function levelLabel(l) {
    if (!l) return "";
    if (l === "MIX") return "Mix";
    if (l === "PV") return "Phrasal Verbs";
    if (l === "PART") return "Partikelverb";
    var g = { GA1: "A1", GA2: "A2", GB1: "B1", GB2: "B2", GC1: "C1", GC2: "C2" };
    return g[l] || l;
  }

  // Default start page (set on profile.html, "udsp_start_page_v1") -- which
  // page the Home dashboard's "Continue"/"Start" button opens. Defaults to
  // Flashcards (index.html), matching this button's original hardcoded
  // behavior exactly for anyone who never touches the profile setting.
  var START_PAGES = [
    { value: "index.html", tr: "Kelime Kartları", en: "Flashcards" },
    { value: "quiz.html", tr: "Quiz", en: "Quiz" },
    { value: "games.html", tr: "Oyunlar", en: "Games" },
    { value: "wordlist.html", tr: "Kelime Listesi", en: "Word List" },
    { value: "stats.html", tr: "İstatistikler", en: "Stats" },
    { value: "wordmorph.html", tr: "Word Morph", en: "Word Morph" },
  ];
  function startPageInfo() {
    var v = get(START_PAGE_KEY, "index.html");
    var found = null;
    START_PAGES.forEach(function (p) {
      if (p.value === v) found = p;
    });
    return found || START_PAGES[0];
  }

  var resume = get(RESUME_KEY, null);
  var known = get(KNOWN_KEY, {});
  var fav = get(FAV_KEY, {});
  var streak = get(STREAK_KEY, { current: 0, longest: 0, last: "" });
  var knownCount = Object.keys(known).length;
  var favCount = Object.keys(fav).length;
  var streakN = streak.current || 0;
  var returning = !!resume || knownCount > 0 || streakN > 0;
  var studiedToday = streak.last === todayStr();

  // Mark that the home/welcome has been shown, so index.html's first-visit
  // redirect (shared.js) does not send this browser back here again.
  set(WELCOME_KEY, 1);

  function startLang(lang) {
    set(RESUME_KEY, { lang: lang, level: DEFAULT_LEVEL[lang] || "A1", category: "Mix" });
    location.href = "index.html";
  }

  // New-visitor setup configurator: persists the chosen language, daily-goal
  // size and study mode, then jumps into the app. Nudges (shake + scroll) the
  // language picker instead of navigating if no language has been chosen yet.
  function startJourney() {
    if (!selectedLang) {
      var picker = document.getElementById("home-lang-picker");
      if (picker) {
        picker.classList.remove("home-nudge");
        void picker.offsetWidth;
        picker.classList.add("home-nudge");
        picker.scrollIntoView({ behavior: "smooth", block: "center" });
      }
      return;
    }
    set(RESUME_KEY, { lang: selectedLang, level: DEFAULT_LEVEL[selectedLang] || "A1" });
    set(GOAL_KEY, { date: "", count: 0, goal: selectedGoal });
    location.href = selectedMode === "practice" ? "quiz.html" : "index.html";
  }

  /* ---- Intro tour: an auto-playing, "stories"-style walkthrough that
   * summarizes the app (mirrors help.html). Auto-plays once for brand-new
   * visitors; anyone can replay it via the "How it works" button. ---- */
  var INTRO_DURATION = 4200; // ms each slide is shown before auto-advancing
  var INTRO_SLIDES = [
    { icon: "\uD83D\uDC4B", title: "Top Words", text: "\u0130ngilizce, Almanca, Frans\u0131zca, \u0130talyanca ve \u0130spanyolca kelimeleri \u00fccretsiz \u00f6\u011fren \u2014 kay\u0131t yok. \u00b7 Learn English, German, French, Italian &amp; Spanish vocabulary \u2014 free, no sign-up." },
    { icon: "\uD83C\uDCCF", title: "Flashcards", text: "Karta dokun, \u00e7evir ve anlam\u0131 g\u00f6r. \uD83D\uDD0A Dinle \u00b7 \u2605 Favori \u00b7 \uD83D\uDD00 Kar\u0131\u015ft\u0131r. \u00b7 Tap a card to flip it and see the meaning." },
    { icon: "\uD83D\uDCDD", title: "Quiz &amp; Word Morph", text: "20 soruluk testler ve e\u015f/z\u0131t anlam turlar\u0131yla kendini s\u0131na. \u00b7 Test yourself with quizzes and synonym / antonym rounds." },
    { icon: "\uD83C\uDFAE", title: "16 Oyun \u00b7 16 Games", text: "Hangman, H\u0131z Turu, E\u015fle\u015ftirme, Kelime Tombalas\u0131, Okudu\u011funu Anlama ve daha fazlas\u0131. \u00b7 Hangman, Speed Round, Matching, Word Bingo, Reading and more." },
    { icon: "\uD83D\uDD25", title: "\u0130lerleme \u00b7 Progress", text: "G\u00fcnl\u00fck seri, bilinen kelimeler ve istatistikler \u2014 taray\u0131c\u0131nda saklan\u0131r, hesap yok. \u00b7 Daily streak, known words and stats \u2014 saved in your browser, no account." },
    { icon: "\uD83D\uDE80", title: "Haz\u0131r m\u0131s\u0131n? \u00b7 Ready?", text: "Ba\u015flamak i\u00e7in bir dil se\u00e7. \u00b7 Choose a language to start.", langs: true }
  ];

  var introEl = null, introTimer = null, introIdx = 0;

  function introBuild() {
    var el = document.createElement("div");
    el.className = "intro-tour";
    el.setAttribute("role", "dialog");
    el.setAttribute("aria-modal", "true");
    el.setAttribute("aria-label", "Top Words intro");
    el.style.setProperty("--intro-dur", INTRO_DURATION / 1000 + "s");
    var bars = "";
    for (var i = 0; i < INTRO_SLIDES.length; i++) bars += '<div class="intro-bar"><span></span></div>';
    var langBtns = "";
    ["en", "de", "fr", "it", "es", "pt"].forEach(function (l) {
      langBtns += '<button type="button" class="home-lang-btn" data-lang="' + l + '">' + LANG_FLAG[l] + " " + LANG_NAME[l] + "</button>";
    });
    el.innerHTML =
      '<div class="intro-card">' +
        '<div class="intro-bars">' + bars + "</div>" +
        '<button class="intro-skip" id="intro-skip" type="button">Atla \u00b7 Skip \u2715</button>' +
        '<div class="intro-slide" id="intro-slide">' +
          '<div class="intro-icon" id="intro-icon"></div>' +
          '<h2 class="intro-title" id="intro-title"></h2>' +
          '<p class="intro-text" id="intro-text"></p>' +
          '<div class="intro-langs" id="intro-langs" hidden>' + langBtns + "</div>" +
        "</div>" +
        '<div class="intro-controls">' +
          '<button class="intro-nav" id="intro-prev" type="button" aria-label="Previous">\u2190</button>' +
          '<span class="intro-count" id="intro-count"></span>' +
          '<button class="intro-nav intro-next" id="intro-next" type="button">Sonraki \u00b7 Next \u2192</button>' +
        "</div>" +
      "</div>";
    document.body.appendChild(el);
    el.querySelector("#intro-skip").addEventListener("click", introClose);
    el.querySelector("#intro-prev").addEventListener("click", function () { introShow(Math.max(0, introIdx - 1)); });
    el.querySelector("#intro-next").addEventListener("click", function () { introShow(introIdx + 1); });
    el.addEventListener("click", function (e) {
      if (e.target === el) { introClose(); return; } // backdrop tap closes
      if (e.target.closest && !e.target.closest("button") && !e.target.closest("a") && e.target.closest(".intro-slide")) {
        if (introIdx < INTRO_SLIDES.length - 1) introShow(introIdx + 1); // tap card body advances
      }
    });
    Array.prototype.forEach.call(el.querySelectorAll(".intro-langs .home-lang-btn"), function (b) {
      b.addEventListener("click", function () { set(INTRO_KEY, 1); startLang(b.getAttribute("data-lang")); });
    });
    document.addEventListener("keydown", introKey);
    return el;
  }

  function introKey(e) {
    if (!introEl) return;
    if (e.key === "Escape") introClose();
    else if (e.key === "ArrowRight") introShow(introIdx + 1);
    else if (e.key === "ArrowLeft") introShow(Math.max(0, introIdx - 1));
  }

  function introShow(i) {
    clearTimeout(introTimer);
    if (i >= INTRO_SLIDES.length) { introClose(); return; }
    introIdx = i;
    var s = INTRO_SLIDES[i];
    var last = i === INTRO_SLIDES.length - 1;
    document.getElementById("intro-icon").textContent = s.icon;
    document.getElementById("intro-title").innerHTML = s.title;
    document.getElementById("intro-text").innerHTML = s.text;
    document.getElementById("intro-count").textContent = i + 1 + " / " + INTRO_SLIDES.length;
    document.getElementById("intro-langs").hidden = !s.langs;
    document.getElementById("intro-next").hidden = last;
    document.getElementById("intro-prev").disabled = i === 0;
    var slide = document.getElementById("intro-slide");
    slide.style.animation = "none"; void slide.offsetWidth; slide.style.animation = "";
    var barEls = introEl.querySelectorAll(".intro-bar");
    Array.prototype.forEach.call(barEls, function (b, idx) {
      b.classList.remove("is-active", "is-done");
      if (idx < i) b.classList.add("is-done");
    });
    if (!last) {
      var active = barEls[i];
      void active.offsetWidth; // reflow so the fill animation restarts cleanly
      active.classList.add("is-active");
      introTimer = setTimeout(function () { introShow(i + 1); }, INTRO_DURATION);
    } else {
      barEls[i].classList.add("is-done");
    }
  }

  function startIntro() {
    if (!introEl) introEl = introBuild();
    document.body.classList.add("intro-open");
    introShow(0);
  }

  function introClose() {
    clearTimeout(introTimer);
    set(INTRO_KEY, 1);
    document.body.classList.remove("intro-open");
    document.removeEventListener("keydown", introKey);
    if (introEl) {
      var el = introEl;
      el.classList.add("is-closing");
      setTimeout(function () { if (el && el.parentNode) el.parentNode.removeChild(el); }, 200);
      introEl = null;
    }
  }

  var html = "";
  var sp = startPageInfo();

  // Sign-in nudge: shown at the very TOP of the page (above the hero, not
  // buried below it) whenever this device has no linked cloud profile yet.
  // By explicit request this has NO dismiss button and NO "once per day"
  // gate (unlike shared.js's equivalent banner previously had) -- it must
  // keep showing on every load until the device actually signs in, so it
  // can't be silenced and then forgotten about. Wording is deliberately
  // DIFFERENT from shared.js's banner (mentions "continue where you left
  // off") since that phrasing only makes sense next to Home's own
  // "Devam et · Continue" button -- this replaces the old inline "Bugün
  // çalıştın" message that used to live inside the hero card below. The
  // full explanation is a hover/focus tooltip on the button (data-tip)
  // rather than always-visible static text, which was too long/wordy.
  if (!get(PROFILE_LINKED_KEY, 0)) {
    html += '<div class="profile-nudge-banner" id="home-profile-nudge">';
    html += '<span class="profile-nudge-text">🔑 Giriş yapmadın · Not signed in</span>';
    html += '<span class="profile-nudge-actions">';
    html +=
      '<a class="profile-nudge-link" href="profile.html" data-tip="' +
      "Profiline giriş yap! Böylece geçmiş çalışmalarının izi saklanır ve son kaldığın yerden devam edebilirsin! · " +
      "Sign in to your profile! This way your past study history is kept and you can continue right where you left off!" +
      '">👤 Profil · Profile</a>';
    html += "</span>";
    html += "</div>";
  }

  if (returning) {
    html += '<section class="home-hero">';
    html += '<h1 class="home-title">👋 Tekrar hoş geldin · Welcome back</h1>';
    html += '<div class="home-stat-row">';
    html +=
      '<div class="home-stat"><span class="home-stat-num">🔥 ' + streakN +
      '</span><span class="home-stat-label">günlük seri · day streak</span></div>';
    html +=
      '<div class="home-stat"><span class="home-stat-num">✓ ' + knownCount +
      '</span><span class="home-stat-label">bilinen · known</span></div>';
    html +=
      '<div class="home-stat"><span class="home-stat-num">★ ' + favCount +
      '</span><span class="home-stat-label">favori · saved</span></div>';
    html += "</div>";
    if (studiedToday) {
      html +=
        '<p class="home-goal ok">✅ Bugün çalıştın · Studied today</p>';
    } else {
      html +=
        '<p class="home-goal">🎯 Serini sürdür · Keep your streak</p>';
    }
    if (resume) {
      html +=
        '<a class="home-continue" href="' + sp.value + '">▶ Devam et · Continue — <strong>' +
        esc(LANG_NAME[resume.lang] || resume.lang) + " · " + esc(levelLabel(resume.level)) +
        "</strong>" + (sp.value !== "index.html" ? " · " + esc(sp.tr) : "") + "</a>";
    } else {
      html +=
        '<a class="home-continue" href="' + sp.value + '">▶ ' + esc(sp.tr) + " · Start " + esc(sp.en) + "</a>";
    }
    html += "</section>";
  } else {
    html += '<section class="home-hero home-hero-new">';
    html += '<div class="home-hero-grid">';
    html += '<div class="home-hero-copy">';
    html += '<span class="home-badge">✨ Geleceğin öğrenme deneyimi · The future of learning</span>';
    html += '<h1 class="home-title">Yeni bir dil macerasına hazır mısın?</h1>';
    html +=
      '<p class="home-sub">Binlerce kelimeyi oyunlarla öğren. · Learn thousands of words through games.</p>';
    html += "</div>"; // .home-hero-copy
    html += '<div class="home-hero-card" aria-hidden="true">';
    html += '<span class="home-hero-card-badge">👋 Hoş Geldin · Welcome</span>';
    html += '<strong class="home-hero-card-title">Top Words</strong>';
    html += '<div class="home-hero-globe">🌐<span>🇬🇧</span><span>🇩🇪</span><span>🇫🇷</span><span>🇮🇹</span><span>🇪🇸</span><span>🇵🇹</span></div>';
    html += '<span class="home-hero-tag">6 dil · 6 languages</span>';
    html += "</div>"; // .home-hero-card
    html += "</div>"; // .home-hero-grid
    html += "</section>";

    html += '<section class="home-picker" id="home-lang-picker">';
    html += '<h2 class="home-picker-title">Hangi dili öğrenmek istersin? · Which language?</h2>';
    html += '<div class="home-langs-cards">';
    ["en", "de", "fr", "it", "es", "pt"].forEach(function (l) {
      html +=
        '<button type="button" class="home-lang-card" data-lang="' + l + '">' +
        '<span class="home-lang-flag">' + LANG_FLAG[l] + "</span>" +
        '<span class="home-lang-name">' + LANG_TR[l] + "</span>" +
        '<span class="home-lang-native">' + LANG_NAME[l] + "</span>" +
        "</button>";
    });
    html += "</div>";
    html += "</section>";

    html += '<section class="home-setup-grid">';
    html += '<div class="home-setup-card">';
    html += '<h3>◎ Günlük hedefini seç · Daily goal</h3>';
    html += '<div class="home-goal-options" id="home-goal-options">';
    GOAL_OPTIONS.forEach(function (g) {
      html +=
        '<button type="button" class="home-opt-btn' + (g.value === selectedGoal ? " is-selected" : "") +
        '" data-goal="' + g.value + '">' +
        '<span class="home-opt-text"><strong>' + g.tr + " · " + g.en + "</strong>" +
        '<span class="home-opt-sub">' + g.value + " Kelime · words</span>" +
        "<small>Günde " + g.mins + " dk · " + g.mins + " min/day</small></span>" +
        '<span class="home-opt-check">✓</span>' +
        "</button>";
    });
    html += "</div>";
    html += "</div>"; // .home-setup-card (goal)

    html += '<div class="home-setup-card">';
    html += '<h3>🎓 Nasıl çalışalım? · How to study</h3>';
    html += '<div class="home-mode-options" id="home-mode-options">';
    MODE_OPTIONS.forEach(function (m) {
      html +=
        '<button type="button" class="home-opt-btn' + (m.value === selectedMode ? " is-selected" : "") +
        '" data-mode="' + m.value + '">' +
        '<span class="home-opt-text"><strong>' + m.tr + " · " + m.en + "</strong>" +
        '<span class="home-opt-sub">' + m.desc + "</span></span>" +
        '<span class="home-opt-check">✓</span>' +
        "</button>";
    });
    html += "</div>";
    html += "</div>"; // .home-setup-card (mode)
    html += "</section>"; // .home-setup-grid

    html += '<div class="home-cta-row">';
    html += '<button type="button" class="home-cta-btn" id="home-cta">Hadi Başlayalım · Let\u2019s get started →</button>';
    html += '<p class="home-cta-note">Ücretsiz · Kayıt yok · Free, no sign-up</p>';
    html += "</div>";
  }

  html += '<div class="home-howto-row"><button type="button" class="home-howto" id="home-howto">\u25b6 Nas\u0131l \u00e7al\u0131\u015f\u0131r? \u00b7 How it works</button></div>';

  var links = [
    ["index.html", "🃏", "Flashcards", "Kelime kartları"],
    ["quiz.html", "📝", "Quiz", "20 soruluk test"],
    ["games.html", "🎮", "Games", "16 oyun"],
    ["wordlist.html", "📋", "Word List", "Tüm kelimeler"],
    ["stats.html", "📊", "Stats", "İlerlemen"],
  ];
  html += '<h2 class="home-section-title">Hızlı erişim · Jump in</h2>';
  html += '<div class="game-tiles">';
  links.forEach(function (a) {
    html +=
      '<a class="game-tile" href="' + a[0] + '"><span class="game-tile-icon">' +
      a[1] + '</span><span class="game-tile-name">' + a[2] +
      '</span><span class="game-tile-desc">' + a[3] + "</span></a>";
  });
  html += "</div>";

  var root = document.getElementById("home-root");
  if (root) {
    root.innerHTML = html;
    // Only wire elements that are actual language picks (data-lang) -- this
    // same .home-lang-btn class is reused as a generic pill-button style by
    // other elements too (e.g. the "Create Profile" link below), which must
    // NOT trigger startLang().
    var btns = root.querySelectorAll(".home-lang-btn[data-lang]");
    Array.prototype.forEach.call(btns, function (b) {
      b.addEventListener("click", function () {
        startLang(b.getAttribute("data-lang"));
      });
    });
    var howto = document.getElementById("home-howto");
    if (howto) howto.addEventListener("click", startIntro);

    // New-visitor configurator wiring (no-ops harmlessly on the returning-
    // user branch, since these elements simply don't exist there).
    var langCards = root.querySelectorAll(".home-lang-card");
    Array.prototype.forEach.call(langCards, function (b) {
      b.addEventListener("click", function () {
        Array.prototype.forEach.call(langCards, function (x) { x.classList.remove("is-selected"); });
        b.classList.add("is-selected");
        selectedLang = b.getAttribute("data-lang");
      });
    });
    var goalBtns = root.querySelectorAll("#home-goal-options .home-opt-btn");
    Array.prototype.forEach.call(goalBtns, function (b) {
      b.addEventListener("click", function () {
        Array.prototype.forEach.call(goalBtns, function (x) { x.classList.remove("is-selected"); });
        b.classList.add("is-selected");
        selectedGoal = parseInt(b.getAttribute("data-goal"), 10) || 15;
      });
    });
    var modeBtns = root.querySelectorAll("#home-mode-options .home-opt-btn");
    Array.prototype.forEach.call(modeBtns, function (b) {
      b.addEventListener("click", function () {
        Array.prototype.forEach.call(modeBtns, function (x) { x.classList.remove("is-selected"); });
        b.classList.add("is-selected");
        selectedMode = b.getAttribute("data-mode");
      });
    });
    var cta = document.getElementById("home-cta");
    if (cta) cta.addEventListener("click", startJourney);
  }

  var profileIconLink = document.getElementById("profile-icon-link");
  if (profileIconLink && get(PROFILE_LINKED_KEY, 0)) {
    profileIconLink.classList.add("is-linked");
  }

  // Auto-play the intro once for brand-new visitors (fully skippable).
  // Returning users can replay it anytime via the "How it works" button.
  if (!get(INTRO_KEY, 0) && !returning) {
    startIntro();
  }
})();
