/*! Top Words (udsp) — Copyright 2026 Bulent Ozkir, Ahmet Arda Ozkir, Halit Eren Ozkir
 * Licensed under the PolyForm Noncommercial License 1.0.0 — NONCOMMERCIAL USE ONLY.
 * <https://polyformproject.org/licenses/noncommercial/1.0.0>
 */

"use strict";
(function () {
  function report(result) {
    window.parent.postMessage(
      { type: "topwords-history-sync", result: result },
      window.location.origin
    );
  }

  if (!window.TWAuth) {
    report("failed");
    return;
  }

  var unsubscribe = function () {};
  unsubscribe = window.TWAuth.onAuthChange(function (user) {
    unsubscribe();
    if (!user) {
      report("signed-out");
      return;
    }
    window.TWAuth.loadProfileToLocal()
      .then(function () {
        report("ok");
      })
      .catch(function () {
        report("failed");
      });
  });
})();