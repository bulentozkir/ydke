/*! Top Words (udsp) — Copyright 2026 Bulent Ozkir, Ahmet Arda Ozkir, Halit Eren Ozkir. All rights reserved. */
"use strict";

// App-CHROME language switcher (menus, toolbars, header, more-sheet). This is
// deliberately separate from the existing "Lang" selector, which picks the
// VOCABULARY content language (en/de/fr/it/es/pt/nl word data) and is
// untouched by anything here. Loaded by every page alongside shared.js or
// moresheet.js (whichever that page already uses) so it works everywhere,
// including pages like games.html that don't load shared.js. Targets stable
// selectors (href/id) instead of requiring data-i18n markup, so adding this
// file needed no other HTML edits beyond the <script> tag itself.
(function () {
  var LANG_KEY = "udsp_ui_lang_v1";
  var LANGS = ["tr", "en", "de", "fr", "es", "pt", "nl"];
  var LANG_META = {
    tr: { flag: "🇹🇷", name: "Türkçe" },
    en: { flag: "🇬🇧", name: "English" },
    de: { flag: "🇩🇪", name: "Deutsch" },
    fr: { flag: "🇫🇷", name: "Français" },
    es: { flag: "🇪🇸", name: "Español" },
    pt: { flag: "🇵🇹", name: "Português" },
    nl: { flag: "🇳🇱", name: "Nederlands" },
  };

  // key -> [emoji-or-null, {lang: text}]. The emoji is only used by
  // setCombinedText() (elements whose visible text is one "emoji Label"
  // text node, e.g. more-sheet links) -- setSpanText()/setAttr() targets
  // ignore it since their emoji already lives in a separate sibling node.
  var STR = {
    nav_cards: ["🃏", { tr: "Kartlar", en: "Cards", de: "Karten", fr: "Cartes", es: "Tarjetas", pt: "Cartões", nl: "Kaarten" }],
    nav_quiz: ["📝", { tr: "Test", en: "Quiz", de: "Quiz", fr: "Quiz", es: "Cuestionario", pt: "Questionário", nl: "Quiz" }],
    nav_games: ["🎮", { tr: "Oyunlar", en: "Games", de: "Spiele", fr: "Jeux", es: "Juegos", pt: "Jogos", nl: "Spellen" }],
    nav_words: ["📋", { tr: "Kelimeler", en: "Words", de: "Wörter", fr: "Mots", es: "Palabras", pt: "Palavras", nl: "Woorden" }],
    nav_morph: ["🔄", { tr: "Morph", en: "Morph", de: "Morph", fr: "Morph", es: "Morph", pt: "Morph", nl: "Morph" }],
    nav_more: ["⋯", { tr: "Diğer", en: "More", de: "Mehr", fr: "Plus", es: "Más", pt: "Mais", nl: "Meer" }],

    hdr_home: ["🏠", { tr: "Ana Sayfa", en: "Home", de: "Startseite", fr: "Accueil", es: "Inicio", pt: "Início", nl: "Start" }],
    hdr_about: ["ℹ️", { tr: "Hakkında", en: "About", de: "Über uns", fr: "À propos", es: "Acerca de", pt: "Sobre", nl: "Over" }],
    hdr_help: ["❓", { tr: "Yardım", en: "Help", de: "Hilfe", fr: "Aide", es: "Ayuda", pt: "Ajuda", nl: "Help" }],
    hdr_stats: ["📊", { tr: "İstatistikler", en: "Stats", de: "Statistiken", fr: "Statistiques", es: "Estadísticas", pt: "Estatísticas", nl: "Statistieken" }],
    hdr_profile: [null, { tr: "Profil", en: "Profile", de: "Profil", fr: "Profil", es: "Perfil", pt: "Perfil", nl: "Profiel" }],
    hdr_about_page: [null, { tr: "Bu sayfa hakkında", en: "About this page", de: "Über diese Seite", fr: "À propos de cette page", es: "Acerca de esta página", pt: "Sobre esta página", nl: "Over deze pagina" }],

    more_home: ["🏠", { tr: "Ana Sayfa", en: "Home", de: "Startseite", fr: "Accueil", es: "Inicio", pt: "Início", nl: "Start" }],
    more_stats: ["📊", { tr: "İstatistikler", en: "Stats", de: "Statistiken", fr: "Statistiques", es: "Estadísticas", pt: "Estatísticas", nl: "Statistieken" }],
    more_listening: ["🎧", { tr: "Dinleme", en: "Listening", de: "Hören", fr: "Écoute", es: "Escucha", pt: "Escuta", nl: "Luisteren" }],
    more_about: ["ℹ️", { tr: "Hakkında", en: "About", de: "Über uns", fr: "À propos", es: "Acerca de", pt: "Sobre", nl: "Over" }],
    more_help: ["❓", { tr: "Yardım", en: "Help", de: "Hilfe", fr: "Aide", es: "Ayuda", pt: "Ajuda", nl: "Help" }],
    more_profile: ["👤", { tr: "Profil", en: "Profile", de: "Profil", fr: "Profil", es: "Perfil", pt: "Perfil", nl: "Profiel" }],
    more_history: ["📜", { tr: "Geçmiş", en: "History", de: "Verlauf", fr: "Historique", es: "Historial", pt: "Histórico", nl: "Geschiedenis" }],
    more_privacy: ["🔒", { tr: "Gizlilik", en: "Privacy", de: "Datenschutz", fr: "Confidentialité", es: "Privacidad", pt: "Privacidade", nl: "Privacy" }],
    more_terms: ["📄", { tr: "Şartlar", en: "Terms", de: "Bedingungen", fr: "Conditions", es: "Términos", pt: "Termos", nl: "Voorwaarden" }],
    more_contact: ["✉️", { tr: "İletişim", en: "Contact", de: "Kontakt", fr: "Contact", es: "Contacto", pt: "Contato", nl: "Contact" }],
    more_close: [null, { tr: "Kapat", en: "Close", de: "Schließen", fr: "Fermer", es: "Cerrar", pt: "Fechar", nl: "Sluiten" }],

    lang_label: [null, { tr: "Dil", en: "Lang", de: "Sprache", fr: "Langue", es: "Idioma", pt: "Idioma", nl: "Taal" }],
    level_label: [null, { tr: "Seviye", en: "Level", de: "Niveau", fr: "Niveau", es: "Nivel", pt: "Nível", nl: "Niveau" }],
  };

  function getLang() {
    try {
      var v = localStorage.getItem(LANG_KEY);
      if (LANGS.indexOf(v) !== -1) return v;
    } catch (e) {}
    return "tr";
  }

  function text(key, lang) {
    var row = STR[key];
    if (!row) return "";
    return row[1][lang] || row[1].tr;
  }

  // .bn-label / .btn-txt: the emoji already lives in a separate sibling
  // node, so only this element's own text changes. Preserves a leading
  // space for .btn-txt (" About"), matching the existing markup's spacing.
  function setSpanText(selector, key, lang, leadingSpace) {
    var el = document.querySelector(selector);
    if (el) el.textContent = (leadingSpace ? " " : "") + text(key, lang);
  }

  // .more-link: "emoji Label" is ONE text node, so the whole thing is
  // replaced together.
  function setCombinedText(selector, key, lang) {
    var el = document.querySelector(selector);
    if (!el) return;
    var row = STR[key];
    el.textContent = (row[0] ? row[0] + " " : "") + text(key, lang);
  }

  function setAttr(selector, attr, key, lang) {
    var el = document.querySelector(selector);
    if (el) el.setAttribute(attr, text(key, lang));
  }

  function apply(lang) {
    // Bottom nav (shared across all 38 chrome-bearing pages).
    setSpanText('a[href="index.html"] .bn-label', "nav_cards", lang);
    setSpanText('a[href="quiz.html"] .bn-label', "nav_quiz", lang);
    setSpanText('a[href="games.html"] .bn-label', "nav_games", lang);
    setSpanText('a[href="wordlist.html"] .bn-label', "nav_words", lang);
    setSpanText('a[href="wordmorph.html"] .bn-label', "nav_morph", lang);
    setSpanText("#more-btn .bn-label", "nav_more", lang);

    // Header icon buttons, scoped to .brand-actions so the (differently
    // shaped) more-sheet links below with the same hrefs aren't matched.
    setSpanText('.brand-actions a[href="home.html"] .btn-txt', "hdr_home", lang, true);
    setSpanText('.brand-actions a[href="about.html"] .btn-txt', "hdr_about", lang, true);
    setSpanText('.brand-actions a[href="help.html"] .btn-txt', "hdr_help", lang, true);
    setSpanText('.brand-actions a[href="stats.html"] .btn-txt', "hdr_stats", lang, true);
    setAttr('.brand-actions a[href="home.html"]', "aria-label", "hdr_home", lang);
    setAttr('.brand-actions a[href="home.html"]', "data-tip", "hdr_home", lang);
    setAttr('.brand-actions a[href="about.html"]', "aria-label", "hdr_about", lang);
    setAttr('.brand-actions a[href="about.html"]', "data-tip", "hdr_about", lang);
    setAttr('.brand-actions a[href="help.html"]', "aria-label", "hdr_help", lang);
    setAttr('.brand-actions a[href="help.html"]', "data-tip", "hdr_help", lang);
    setAttr('.brand-actions a[href="stats.html"]', "aria-label", "hdr_stats", lang);
    setAttr('.brand-actions a[href="stats.html"]', "data-tip", "hdr_stats", lang);
    setAttr("#profile-icon-link", "aria-label", "hdr_profile", lang);
    setAttr("#profile-icon-link", "data-tip", "hdr_profile", lang);
    setAttr(".seo-info-btn", "aria-label", "hdr_about_page", lang);
    setAttr(".seo-info-btn", "data-tip", "hdr_about_page", lang);

    // More-sheet menu.
    setCombinedText('.more-sheet a[href="home.html"]', "more_home", lang);
    setCombinedText('.more-sheet a[href="stats.html"]', "more_stats", lang);
    setCombinedText('.more-sheet a[href="listening.html"]', "more_listening", lang);
    setCombinedText('.more-sheet a[href="about.html"]', "more_about", lang);
    setCombinedText('.more-sheet a[href="help.html"]', "more_help", lang);
    setCombinedText('.more-sheet a[href="profile.html"]', "more_profile", lang);
    setCombinedText('.more-sheet a[href="history.html"]', "more_history", lang);
    setCombinedText('.more-sheet a[href="privacy.html"]', "more_privacy", lang);
    setCombinedText('.more-sheet a[href="terms.html"]', "more_terms", lang);
    setCombinedText('.more-sheet a[href="terms.html#contact"]', "more_contact", lang);
    setSpanText("#more-close", "more_close", lang);

    // Lang/Level combo-box labels (the preceding sibling of each <select>).
    var langsNav = document.getElementById("langs-nav");
    if (langsNav && langsNav.previousElementSibling) langsNav.previousElementSibling.textContent = text("lang_label", lang);
    var levelsNav = document.getElementById("levels-nav");
    if (levelsNav && levelsNav.previousElementSibling) levelsNav.previousElementSibling.textContent = text("level_label", lang);

    document.dispatchEvent(new CustomEvent("udsp:ui-lang-changed", { detail: { lang: lang } }));
  }

  function setLang(code) {
    if (LANGS.indexOf(code) === -1) return;
    try {
      localStorage.setItem(LANG_KEY, code);
    } catch (e) {}
    apply(code);
  }

  apply(getLang());

  window.UdspI18n = { get: getLang, set: setLang, text: text, LANGS: LANGS, LANG_META: LANG_META };
})();
