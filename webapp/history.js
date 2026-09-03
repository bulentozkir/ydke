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

/* Top Words — History page (history.html only).
 * Reads the local activity log (HISTORY_KEY, appended to by shared.js's
 * logHistory() on Flashcards/Quiz/Word Morph/Reading Comprehension) and
 * renders it newest-first. Cloud sync runs in a disposable same-origin iframe
 * so Firebase has no recurring work on the idle History page.
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

  var HISTORY_KEY = "udsp_history_v1";
  var PROFILE_LINKED_KEY = "udsp_profile_linked_v1";
  var syncFrame = null;
  var syncTimeout = null;

  var LANG_NAME = { en: "English", de: "German", fr: "French", it: "Italian" };
  var TYPE_META = {
    flashcard: { icon: "🃏" },
    quiz: { icon: "📝" },
    wordmorph: { icon: "🔄" },
    readingcomp: { icon: "📖" },
  };

  function pad(n) {
    return n < 10 ? "0" + n : "" + n;
  }
  function formatWhen(t) {
    var d = new Date(t);
    var now = new Date();
    var time = pad(d.getHours()) + ":" + pad(d.getMinutes());
    if (d.toDateString() === now.toDateString()) return "Bugün · Today, " + time;
    var y = new Date(now);
    y.setDate(y.getDate() - 1);
    if (d.toDateString() === y.toDateString()) return "Dün · Yesterday, " + time;
    var months = ["Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara"];
    return d.getDate() + " " + months[d.getMonth()] + " " + d.getFullYear() + ", " + time;
  }

  function describeEntry(e) {
    var lang = LANG_NAME[e.lang] || e.lang || "";
    var langLevel = lang + (e.level ? " · " + e.level : "");
    if (e.type === "flashcard") {
      return (
        "<strong>" + esc(e.word || "") + "</strong> kelimesini çalıştın · studied <strong>" +
        esc(e.word || "") + "</strong>" + (langLevel ? " — " + esc(langLevel) : "")
      );
    }
    if (e.type === "quiz") {
      var pct = e.pct != null ? e.pct : e.total ? Math.round((e.score / e.total) * 100) : 0;
      return (
        "Quiz tamamlandı: <strong>" + e.score + " / " + e.total + "</strong> (%" + pct +
        ") · Quiz completed: " + e.score + " / " + e.total + " (" + pct + "%)" +
        (langLevel ? " — " + esc(langLevel) : "")
      );
    }
    if (e.type === "wordmorph") {
      return (
        "Word Morph turu tamamlandı: <strong>" + e.score + " / " + e.total + "</strong>" +
        (e.win ? " (Kazandı · Won)" : " (Kaybetti · Lost)") + (lang ? " — " + esc(lang) : "")
      );
    }
    if (e.type === "readingcomp") {
      return (
        "\u201c" + esc(e.title || "") + "\u201d tamamlandı: <strong>" + e.score + " / " + e.total +
        "</strong> · \u201c" + esc(e.title || "") + "\u201d completed: " + e.score + " / " + e.total
      );
    }
    return "Aktivite · Activity";
  }

  function render() {
    var all = lsGet(HISTORY_KEY, [])
      .slice()
      .sort(function (a, b) {
        return (b.t || 0) - (a.t || 0);
      });
    var filterSel = $("history-filter");
    var filter = filterSel ? filterSel.value : "all";
    var list = filter === "all" ? all : all.filter(function (e) { return e.type === filter; });

    var html = "";
    if (!list.length) {
      html +=
        '<p class="home-sub" style="text-align:center">Henüz aktivite yok. Çalışmaya başla! · No activity yet. Start studying!</p>';
    } else {
      html += '<ul class="history-list">';
      list.forEach(function (e) {
        var meta = TYPE_META[e.type] || { icon: "📌" };
        html +=
          '<li class="history-item history-item-' + esc(e.type || "") + '">' +
          '<span class="history-icon">' + meta.icon + "</span>" +
          '<div class="history-body">' +
          "<span>" + describeEntry(e) + "</span>" +
          '<span class="history-meta">' + formatWhen(e.t) + "</span>" +
          "</div></li>";
      });
      html += "</ul>";
    }
    var itemsEl = $("history-items");
    if (itemsEl) itemsEl.innerHTML = html;
    var countEl = $("history-count");
    if (countEl) countEl.textContent = list.length + " / " + all.length;
  }

  function wireSyncButton() {
    var btn = $("history-sync-btn");
    if (!btn) return;
    btn.hidden = !lsGet(PROFILE_LINKED_KEY, 0);

    function finishSync(result, original) {
      if (syncTimeout) clearTimeout(syncTimeout);
      syncTimeout = null;
      if (syncFrame) syncFrame.remove();
      syncFrame = null;
      btn.disabled = false;
      if (result === "ok") {
        btn.textContent = original;
        render();
      } else if (result === "signed-out") {
        localStorage.setItem(PROFILE_LINKED_KEY, "0");
        btn.textContent = "Profilde tekrar giriş yap · Sign in again";
      } else {
        btn.textContent = "Senkronizasyon başarısız · Sync failed";
      }
    }

    window.addEventListener("message", function (event) {
      if (!syncFrame || event.source !== syncFrame.contentWindow || event.origin !== location.origin) return;
      if (!event.data || event.data.type !== "topwords-history-sync") return;
      finishSync(event.data.result, syncFrame.getAttribute("data-original"));
    });

    btn.addEventListener("click", function () {
      if (syncFrame) return;
      btn.disabled = true;
      var original = btn.textContent;
      btn.textContent = "⏳ Senkronize ediliyor… · Syncing…";
      syncFrame = document.createElement("iframe");
      syncFrame.hidden = true;
      syncFrame.src = "history-sync.html";
      syncFrame.setAttribute("data-original", original);
      syncFrame.setAttribute("title", "Cloud history sync");
      document.body.appendChild(syncFrame);
      syncTimeout = setTimeout(function () {
        finishSync("failed", original);
      }, 30000);
    });
  }

  document.addEventListener("DOMContentLoaded", function () {
    render();
    var filterSel = $("history-filter");
    if (filterSel) filterSel.addEventListener("change", render);
    wireSyncButton();
  });
})();
