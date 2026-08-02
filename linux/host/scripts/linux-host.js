/*
 * Linux-only host script. Injected after shared/bootstrap.js.
 *
 * Everything here needs a native call, which is exactly why it is not in the
 * shared bootstrap. Three jobs, each mapped to a blocker in
 * linux/03-blockers-and-fixes.md:
 *
 *   L5  WebKitGTK ships no speechSynthesis at all — not "no voices", the API is
 *       absent. Bridged to speech-dispatcher through the Rust host.
 *   L4  signInWithPopup cannot work: Tauri does not open the auxiliary window
 *       WebKitGTK would need for window.opener to be wired up. Redirected.
 *   L3  Defence in depth for ad requests issued from script at runtime.
 *
 * The Rust host substitutes "__TW_VOICES__" and "__TW_BLOCKED_HOSTS__" with JSON
 * arrays before injection, so config.rs stays the single source of truth. Both
 * tokens are quoted strings here so the file is valid JavaScript on its own and
 * `node --check` can verify it.
 */
(function () {
  "use strict";

  var VOICES = "__TW_VOICES__";
  var BLOCKED_HOSTS = "__TW_BLOCKED_HOSTS__";

  function invoke(cmd, args) {
    // Tauri's own bootstrap may not have run yet, so resolve late rather than
    // capturing __TAURI__ at load time.
    var api = window.__TAURI__ && window.__TAURI__.core;
    if (!api || typeof api.invoke !== "function") {
      return Promise.reject(new Error("Tauri IPC unavailable"));
    }
    return api.invoke(cmd, args || {});
  }

  /* ================================================================== *
   * L3 — runtime ad requests.
   *
   * The static <script src> tags are stopped at the network layer by the host
   * (see adblock.rs) because a MutationObserver callback is a microtask and
   * therefore always arrives *after* a parser-inserted classic script has
   * already executed. What is caught here is the second wave: the beacons and
   * XHRs an ad script issues once it is running. Cheap, and it keeps the app
   * quiet if the network filter is compiled out.
   * ================================================================== */
  function isBlockedUrl(input) {
    var href;
    try {
      href = typeof input === "string" ? input : (input && input.url) || String(input);
      var host = new URL(href, location.href).hostname.toLowerCase();
      for (var i = 0; i < BLOCKED_HOSTS.length; i++) {
        var pattern = BLOCKED_HOSTS[i].toLowerCase();
        if (pattern.charAt(0) === "*") {
          var suffix = pattern.slice(1);
          if (host.length > suffix.length && host.slice(-suffix.length) === suffix) { return true; }
        } else if (host === pattern) {
          return true;
        }
      }
    } catch (e) { /* not a parsable URL — let it through */ }
    return false;
  }

  var nativeFetch = window.fetch;
  if (typeof nativeFetch === "function") {
    window.fetch = function (input, init) {
      if (isBlockedUrl(input)) {
        return Promise.resolve(new Response(null, { status: 204, statusText: "No Content" }));
      }
      return nativeFetch.apply(this, arguments);
    };
  }

  var nativeOpen = XMLHttpRequest.prototype.open;
  XMLHttpRequest.prototype.open = function (method, url) {
    this.__twBlocked = isBlockedUrl(url);
    return nativeOpen.apply(this, arguments);
  };

  var nativeSend = XMLHttpRequest.prototype.send;
  XMLHttpRequest.prototype.send = function () {
    if (this.__twBlocked) { return; }
    return nativeSend.apply(this, arguments);
  };

  if (navigator.sendBeacon) {
    var nativeBeacon = navigator.sendBeacon.bind(navigator);
    navigator.sendBeacon = function (url, data) {
      // Report success so callers do not retry over a different transport.
      return isBlockedUrl(url) ? true : nativeBeacon(url, data);
    };
  }

  /* ================================================================== *
   * L5 — speechSynthesis over speech-dispatcher.
   * ================================================================== */
  function installSpeech() {
    function Voice(name, lang) {
      this.voiceURI = "speech-dispatcher:" + lang;
      this.name = name;
      this.lang = lang;
      this.localService = true;
      this["default"] = lang === "en-US";
    }

    var voices = VOICES.map(function (pair) {
      return new Voice("Top Words (" + pair[1] + ")", pair[0]);
    });

    function Utterance(text) {
      this.text = text == null ? "" : String(text);
      this.lang = "";
      this.voice = null;
      this.volume = 1;
      this.rate = 1;
      this.pitch = 1;
      this.onstart = null;
      this.onend = null;
      this.onerror = null;
      this.onboundary = null;
      this.onpause = null;
      this.onresume = null;
      this.onmark = null;
    }

    Utterance.prototype.addEventListener = function (type, fn) {
      if (typeof fn !== "function") { return; }
      var key = "on" + type;
      var prev = this[key];
      this[key] = function (ev) {
        if (typeof prev === "function") { prev.call(this, ev); }
        fn.call(this, ev);
      };
    };
    Utterance.prototype.removeEventListener = function () { /* not tracked */ };

    function fire(utterance, type) {
      var handler = utterance["on" + type];
      if (typeof handler !== "function") { return; }
      try {
        handler.call(utterance, { type: type, utterance: utterance, charIndex: 0, elapsedTime: 0 });
      } catch (e) { /* page handler threw — not ours to swallow silently, but not fatal */ }
    }

    /*
     * speech-dispatcher does not report completion back to us, so `end` is
     * fired on an estimate: ~14 characters per second at rate 1, floored at
     * 700ms. The web app only uses `end` to re-enable its play button, so an
     * approximation is acceptable — but do not build anything timing-critical
     * on it without adding a real completion channel from the Rust side.
     */
    function estimateMs(text, rate) {
      var perSecond = 14 * (rate > 0 ? rate : 1);
      return Math.max(700, Math.ceil((text.length / perSecond) * 1000));
    }

    var current = null;
    var timer = null;

    function finish() {
      if (timer) { clearTimeout(timer); timer = null; }
      var utterance = current;
      current = null;
      if (utterance) { fire(utterance, "end"); }
    }

    var synth = {
      pending: false,
      paused: false,
      get speaking() { return current !== null; },

      getVoices: function () { return voices.slice(); },

      speak: function (utterance) {
        if (!utterance || !utterance.text) { return; }
        synth.cancel();

        var lang = (utterance.voice && utterance.voice.lang) || utterance.lang || "en-US";
        current = utterance;

        invoke("tts_speak", {
          text: utterance.text,
          lang: lang,
          rate: utterance.rate,
          pitch: utterance.pitch
        }).then(function () {
          fire(utterance, "start");
          timer = setTimeout(finish, estimateMs(utterance.text, utterance.rate));
        })["catch"](function (err) {
          current = null;
          var handler = utterance.onerror;
          if (typeof handler === "function") {
            handler.call(utterance, { type: "error", error: "synthesis-failed", message: String(err), utterance: utterance });
          }
        });
      },

      cancel: function () {
        if (timer) { clearTimeout(timer); timer = null; }
        current = null;
        invoke("tts_cancel")["catch"](function () { /* nothing playing */ });
      },

      // speech-dispatcher supports pause/resume, but the app never calls them,
      // so they are deliberately no-ops rather than half-implemented.
      pause: function () { },
      resume: function () { },
      addEventListener: function () { },
      removeEventListener: function () { }
    };

    try {
      Object.defineProperty(window, "speechSynthesis", { value: synth, configurable: false });
      Object.defineProperty(window, "SpeechSynthesisUtterance", { value: Utterance, configurable: false });
    } catch (e) {
      window.speechSynthesis = synth;
      window.SpeechSynthesisUtterance = Utterance;
    }
  }

  // Only shim when the engine really has nothing. If a future WebKitGTK gains
  // a working implementation, defer to it.
  if (typeof window.speechSynthesis === "undefined" || typeof window.SpeechSynthesisUtterance === "undefined") {
    installSpeech();
  }

  /* ================================================================== *
   * L4 — signInWithPopup has no handler.
   *
   * Patching a vendor SDK is not something to do lightly. The alternative is
   * changing firebase-client.js in the udsp repo, which is the better fix and
   * is recorded as such in the blocker. This exists so the Linux build is not
   * blocked on that change: it swaps the popup flow for the redirect flow,
   * which lands on accounts.google.com and then *.firebaseapp.com — both
   * already in IN_APP_HOSTS, so the whole exchange stays in the app window.
   *
   * Remove this block once the web app calls signInWithRedirect itself.
   * ================================================================== */
  (function patchAuth() {
    var attempts = 0;
    var handle = setInterval(function () {
      attempts++;
      var Auth = window.firebase && window.firebase.auth && window.firebase.auth.Auth;
      var proto = Auth && Auth.prototype;

      if (proto && typeof proto.signInWithRedirect === "function" && !proto.__twRedirectPatched) {
        proto.__twRedirectPatched = true;
        proto.signInWithPopup = function (provider) {
          // The page navigates away, so this promise intentionally never
          // settles; onAuthStateChanged delivers the result after the return
          // trip. Rejecting here would make the app render a sign-in error
          // during the redirect.
          this.signInWithRedirect(provider);
          return new Promise(function () { });
        };
        clearInterval(handle);
        return;
      }

      if (attempts > 300) { clearInterval(handle); }   // ~15s, then give up
    }, 50);
  })();
})();
