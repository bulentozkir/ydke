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

/* Top Words — Firebase Auth + Firestore profile client.
 * Uses the Firebase COMPAT SDK (classic <script> globals, no bundler/no
 * `type="module"`) so it fits this app's existing plain-global-script
 * architecture (see shared.js/home.js precedent). Loaded by Profile on demand
 * and inside the disposable History sync frame.
 *
 * Exposes a small public API on window.TWAuth for profile.js to call:
 *   signIn(providerId), signOut(), onAuthChange(cb),
 *   saveProfileFromLocal(), loadProfileToLocal(),
 *   deleteProfileDoc(), deleteAccountAndProfile(), currentUser()
 *
 * Data model: one Firestore document per user at users/{uid} (uid = the
 * Firebase Auth UID) — see firestore.rules for the security rules this
 * depends on (create/update/delete restricted to request.auth.uid === the
 * document's own id). Profile documents mirror the SAME localStorage keys
 * shared.js/home.js already use (kept in sync by hand, same precedent as
 * home.js's own header comment), so signing in on a new device can pull
 * existing progress down, and this device's progress can be pushed up.
 */
"use strict";
(function () {
  var KNOWN_KEY = "udsp_known_v1";
  var FAV_KEY = "udsp_fav_v1";
  var STREAK_KEY = "udsp_streak_v1";
  var GOAL_KEY = "udsp_daily_v1";
  var RESUME_KEY = "udsp_resume_v2";
  var START_PAGE_KEY = "udsp_start_page_v1"; // which page "Continue" on home.html opens
  var AUTOSAVE_KEY = "udsp_autosave_v1"; // whether the every-60s autosave tick is enabled
  var HISTORY_KEY = "udsp_history_v1";
  var HISTORY_MAX = 300;

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
    } catch (e) {}
  }

  if (!window.firebase || !window.FIREBASE_CONFIG) {
    console.error("TWAuth: Firebase SDK or FIREBASE_CONFIG missing — check script load order in profile.html.");
    return;
  }
  if (!window.FIREBASE_CONFIG.apiKey || /REPLACE_ME/.test(window.FIREBASE_CONFIG.apiKey)) {
    // firebase-config.js still has placeholders — profile.js checks this
    // itself before rendering sign-in UI; just avoid a noisy init crash here.
    console.warn("TWAuth: firebase-config.js still has placeholder values — not initializing.");
    return;
  }

  firebase.initializeApp(window.FIREBASE_CONFIG);
  var auth = firebase.auth();
  var db = firebase.firestore();

  // Only Google is offered as a sign-in provider (Meta/Microsoft/LinkedIn
  // and, most recently, Apple were all removed per explicit request --
  // simplest possible support surface).
  var PROVIDERS = {
    google: function () {
      return new firebase.auth.GoogleAuthProvider();
    },
  };

  function profileRef(uid) {
    return db.collection("users").doc(uid);
  }

  // History is an APPEND-ONLY log that can grow independently on different
  // devices (studied on phone AND laptop) -- unlike known/fav/streak (plain
  // maps/objects safe to overwrite from whichever side synced last), a
  // naive overwrite of the `history` array field would silently DROP
  // whichever side wasn't just written. Union + dedupe + sort + cap instead,
  // used on every path that reads OR writes history (see saveProfileFromLocal
  // and applyToLocal below).
  function mergeHistory(a, b) {
    var combined = (a || []).concat(b || []);
    var seen = {};
    var deduped = [];
    combined.forEach(function (e) {
      var key = (e.t || 0) + "|" + (e.type || "") + "|" + (e.word || e.title || "") + "|" + (e.score || "");
      if (seen[key]) return;
      seen[key] = true;
      deduped.push(e);
    });
    deduped.sort(function (x, y) {
      return (y.t || 0) - (x.t || 0);
    });
    return deduped.slice(0, HISTORY_MAX);
  }

  function localSnapshot() {
    return {
      known: lsGet(KNOWN_KEY, {}),
      fav: lsGet(FAV_KEY, {}),
      streak: lsGet(STREAK_KEY, { current: 0, longest: 0, last: "" }),
      daily: lsGet(GOAL_KEY, { date: "", count: 0, goal: 20 }),
      resume: lsGet(RESUME_KEY, null),
      startPage: lsGet(START_PAGE_KEY, "index.html"),
      autosave: lsGet(AUTOSAVE_KEY, true),
      history: lsGet(HISTORY_KEY, []),
    };
  }

  function applyToLocal(data) {
    if (!data) return;
    if (data.known) lsSet(KNOWN_KEY, data.known);
    if (data.fav) lsSet(FAV_KEY, data.fav);
    if (data.streak) lsSet(STREAK_KEY, data.streak);
    if (data.daily) lsSet(GOAL_KEY, data.daily);
    if (data.resume) lsSet(RESUME_KEY, data.resume);
    if (data.startPage) lsSet(START_PAGE_KEY, data.startPage);
    if (data.autosave !== undefined) lsSet(AUTOSAVE_KEY, data.autosave);
    if (data.history) lsSet(HISTORY_KEY, mergeHistory(lsGet(HISTORY_KEY, []), data.history));
  }

  // Lightweight, single-field preference save -- always saves locally first
  // (works even signed-out), and additionally pushes just this one field to
  // the cloud profile when signed in, without touching known/fav/streak/etc.
  function saveStartPage(value) {
    lsSet(START_PAGE_KEY, value);
    var user = auth.currentUser;
    if (!user) return Promise.resolve();
    return profileRef(user.uid).set({ startPage: value, updatedAt: Date.now() }, { merge: true });
  }

  // Same lightweight pattern as saveStartPage(), for the autosave checkbox.
  function saveAutosavePref(value) {
    lsSet(AUTOSAVE_KEY, !!value);
    var user = auth.currentUser;
    if (!user) return Promise.resolve();
    return profileRef(user.uid).set({ autosave: !!value, updatedAt: Date.now() }, { merge: true });
  }

  function signIn(providerId) {
    var build = PROVIDERS[providerId];
    if (!build) return Promise.reject(new Error("Unknown provider: " + providerId));
    return auth.signInWithPopup(build()).then(function (result) {
      var user = result.user;
      var ref = profileRef(user.uid);
      return ref.get().then(function (snap) {
        if (snap.exists) {
          // Returning profile already in the cloud -> this device adopts it.
          applyToLocal(snap.data());
        } else {
          // First-ever sign-in on any device -> seed the cloud profile from
          // whatever this device already has in localStorage.
          var seed = Object.assign(
            {
              uid: user.uid,
              displayName: user.displayName || "",
              email: user.email || "",
              photoURL: user.photoURL || "",
              provider: providerId,
              createdAt: Date.now(),
              updatedAt: Date.now(),
            },
            localSnapshot()
          );
          return ref.set(seed);
        }
      }).then(function () {
        return user;
      });
    });
  }

  function signOutUser() {
    return auth.signOut();
  }

  function onAuthChange(cb) {
    return auth.onAuthStateChanged(cb);
  }

  function saveProfileFromLocal() {
    var user = auth.currentUser;
    if (!user) return Promise.reject(new Error("Not signed in."));
    var ref = profileRef(user.uid);
    // History needs a read-before-write merge (see mergeHistory's comment) --
    // every other field is safe to just overwrite via {merge:true} below.
    return ref.get().then(function (snap) {
      var cloudHistory = snap.exists && snap.data().history ? snap.data().history : [];
      var merged = mergeHistory(lsGet(HISTORY_KEY, []), cloudHistory);
      lsSet(HISTORY_KEY, merged);
      var data = Object.assign({ uid: user.uid, updatedAt: Date.now() }, localSnapshot());
      data.history = merged;
      return ref.set(data, { merge: true });
    });
  }

  function loadProfileToLocal() {
    var user = auth.currentUser;
    if (!user) return Promise.reject(new Error("Not signed in."));
    return profileRef(user.uid).get().then(function (snap) {
      if (snap.exists) applyToLocal(snap.data());
      return snap.exists ? snap.data() : null;
    });
  }

  function deleteProfileDoc() {
    var user = auth.currentUser;
    if (!user) return Promise.reject(new Error("Not signed in."));
    return profileRef(user.uid).delete();
  }

  function deleteAccountAndProfile() {
    var user = auth.currentUser;
    if (!user) return Promise.reject(new Error("Not signed in."));
    return deleteProfileDoc().then(function () {
      // May reject with auth/requires-recent-login if the session is old;
      // profile.js surfaces that error message as-is rather than building
      // a full re-authentication flow here.
      return user.delete();
    });
  }

  window.TWAuth = {
    signIn: signIn,
    signOut: signOutUser,
    onAuthChange: onAuthChange,
    saveProfileFromLocal: saveProfileFromLocal,
    loadProfileToLocal: loadProfileToLocal,
    deleteProfileDoc: deleteProfileDoc,
    deleteAccountAndProfile: deleteAccountAndProfile,
    saveStartPage: saveStartPage,
    saveAutosavePref: saveAutosavePref,
    currentUser: function () {
      return auth.currentUser;
    },
  };
})();
