using System.Net;
using System.Text.Json;

namespace YDKE_Windows;

internal static class GoogleSignInPage
{
    // Both official CDN modules verified HTTP 200 before pinning this version.
    internal const string FirebaseSdkVersion = "12.2.1";
    internal const string FirebaseAppUrl = "https://www.gstatic.com/firebasejs/" + FirebaseSdkVersion + "/firebase-app.js";
    internal const string FirebaseAuthUrl = "https://www.gstatic.com/firebasejs/" + FirebaseSdkVersion + "/firebase-auth.js";

    internal static string CreateContentSecurityPolicy(string nonce)
    {
        ValidateRandomValue(nonce);
        return "default-src 'none'; base-uri 'none'; object-src 'none'; form-action 'none'; frame-ancestors 'none'; "
            + $"script-src 'nonce-{nonce}' https://www.gstatic.com/firebasejs/{FirebaseSdkVersion}/ https://apis.google.com; "
            + $"style-src 'nonce-{nonce}'; "
            + "connect-src 'self' https://identitytoolkit.googleapis.com https://securetoken.googleapis.com "
            + $"https://www.googleapis.com https://apis.google.com https://{CloudConfig.FirebaseAuthDomain}; "
            + $"frame-src https://{CloudConfig.FirebaseAuthDomain}/__/auth/iframe; "
            + "img-src 'none'; font-src 'none'; worker-src 'none'";
    }

    internal static string CreateHtml(string state, string nonce, string language)
    {
        ValidateRandomValue(state);
        ValidateRandomValue(nonce);
        language = NormalizeLanguage(language);
        var copy = GetCopy(language);
        // Default JSON escaping protects script delimiters, quotes and Unicode; never use relaxed escaping.
        var options = JsonSerializer.Serialize(new
        {
            firebase = new
            {
                apiKey = CloudConfig.FirebaseApiKey,
                authDomain = CloudConfig.FirebaseAuthDomain,
                projectId = CloudConfig.FirebaseProjectId,
            },
            state,
            language,
            copy,
            errors = GoogleBrowserSignIn.BrowserErrorCodes,
        });

        return $$"""
            <!doctype html>
            <html lang="{{WebUtility.HtmlEncode(language)}}">
            <head>
              <meta charset="UTF-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <meta name="referrer" content="no-referrer">
              <title>{{WebUtility.HtmlEncode(copy["pageTitle"])}}</title>
              <script nonce="{{nonce}}">
                (() => {
                  const param = new URLSearchParams(window.location.search).get("scoutTheme");
                  const theme =
                    param || (window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light");
                  document.documentElement.setAttribute("data-theme", theme);
                })();
              </script>
              <style nonce="{{nonce}}">
                :root {
                  color-scheme: light;
                  --cp-bg: #f7f4ef;
                  --cp-bg-elevated: #fcfbf8;
                  --cp-surface: #ffffff;
                  --cp-surface-soft: #f5f5f5;
                  --cp-border: #dedede;
                  --cp-border-strong: #919191;
                  --cp-text: #242424;
                  --cp-text-muted: #5c5c5c;
                  --cp-text-soft: #6f6f6f;
                  --cp-accent: #b11f4b;
                  --cp-accent-hover: #9a1a41;
                  --cp-accent-soft: rgba(177, 31, 75, 0.08);
                  --cp-accent-fg: #ffffff;
                  --cp-success: #16a34a;
                  --cp-danger: #dc2626;
                  --cp-warning: #f59e0b;
                  --cp-link: #0078d4;
                  --cp-shadow: 0 18px 48px rgba(0, 0, 0, 0.12);
                  --cp-overlay: rgba(255, 255, 255, 0.8);
                  --cp-panel: rgba(255, 255, 255, 0.86);
                  --cp-panel-strong: rgba(255, 255, 255, 0.96);
                  --cp-sheen: rgba(255, 255, 255, 0.55);
                  --cp-highlight: rgba(177, 31, 75, 0.12);
                }
                html[data-theme="dark"] {
                  color-scheme: dark;
                  --cp-bg: #3d3b3a;
                  --cp-bg-elevated: #343231;
                  --cp-surface: #292929;
                  --cp-surface-soft: #2e2e2e;
                  --cp-border: #474747;
                  --cp-border-strong: #5f5f5f;
                  --cp-text: #dedede;
                  --cp-text-muted: #919191;
                  --cp-text-soft: #b0b0b0;
                  --cp-accent: #fd8ea1;
                  --cp-accent-hover: #fb7b91;
                  --cp-accent-soft: rgba(253, 142, 161, 0.14);
                  --cp-accent-fg: #1a1a1a;
                  --cp-success: #4ade80;
                  --cp-danger: #f87171;
                  --cp-warning: #fbbf24;
                  --cp-link: #4da6ff;
                  --cp-shadow: 0 18px 48px rgba(0, 0, 0, 0.32);
                  --cp-overlay: rgba(41, 41, 41, 0.88);
                  --cp-panel: rgba(41, 41, 41, 0.72);
                  --cp-panel-strong: rgba(41, 41, 41, 0.96);
                  --cp-sheen: rgba(255, 255, 255, 0.04);
                  --cp-highlight: rgba(253, 142, 161, 0.12);
                }
                * { box-sizing: border-box; }
                body {
                  margin: 0; min-height: 100vh; display: grid; place-items: center; padding: 24px;
                  background: var(--cp-bg); color: var(--cp-text);
                  font-family: "Segoe UI", Aptos, Calibri, -apple-system, BlinkMacSystemFont, sans-serif;
                  font-size: 18px; line-height: 1.6;
                }
                main {
                  width: 100%; max-width: 480px; padding: 32px; border-radius: 16px;
                  background: var(--cp-surface); border: 1px solid var(--cp-border);
                  box-shadow: 0 1px 2px var(--cp-border);
                }
                .brand { margin: 0 0 16px; color: var(--cp-accent); font-weight: 700; letter-spacing: .12em; }
                h1 { margin: 0 0 12px; font-size: 28px; line-height: 1.25; font-weight: 600; }
                p { margin: 0 0 20px; }
                .description, .footnote { color: var(--cp-text-muted); }
                #status {
                  padding: 16px; margin: 24px 0 16px; border-radius: .625rem;
                  background: var(--cp-surface-soft); border: 1px solid var(--cp-border);
                }
                button {
                  width: 100%; min-height: 48px; padding: 12px 20px; border-radius: .625rem;
                  border: 1px solid var(--cp-accent); background: var(--cp-accent); color: var(--cp-accent-fg);
                  font: inherit; font-weight: 600; cursor: pointer;
                }
                button:hover { background: var(--cp-accent-hover); border-color: var(--cp-accent-hover); }
                button:focus-visible { outline: 3px solid var(--cp-link); outline-offset: 4px; }
                button:disabled { cursor: wait; opacity: .65; }
                [hidden] { display: none !important; }
                .footnote { margin: 24px 0 0; }
              </style>
            </head>
            <body>
              <main aria-labelledby="title">
                <p class="brand">YDKE</p>
                <h1 id="title">{{WebUtility.HtmlEncode(copy["heading"])}}</h1>
                <p class="description">{{WebUtility.HtmlEncode(copy["description"])}}</p>
                <p id="status" role="status" aria-live="polite" aria-atomic="true">{{WebUtility.HtmlEncode(copy["statusPreparing"])}}</p>
                <button id="continue" type="button" hidden>{{WebUtility.HtmlEncode(copy["Continue"])}}</button>
                <p class="footnote">{{WebUtility.HtmlEncode(copy["footnote"])}}</p>
                <noscript>{{WebUtility.HtmlEncode(copy["noscript"])}}</noscript>
              </main>
              <script type="module" nonce="{{nonce}}">
                const options = {{options}};
                const status = document.getElementById("status");
                const button = document.getElementById("continue");
                const allowedErrors = new Set(options.errors);
                const resultPath = window.location.pathname + "result";
                let delivered = false;
                let ended = false;
                let busy = false;
                let startPopup = null;
                let clearSession = async () => {};

                function showButton(label) {
                  button.textContent = label;
                  button.hidden = false;
                  button.disabled = false;
                }

                async function postResult(payload, keepalive = false) {
                  const response = await fetch(resultPath, {
                    method: "POST",
                    mode: "same-origin",
                    credentials: "omit",
                    cache: "no-store",
                    redirect: "error",
                    referrerPolicy: "no-referrer",
                    headers: { "Content-Type": "application/json", "X-YDKE-State": options.state },
                    body: JSON.stringify(payload),
                    signal: AbortSignal.timeout(10000),
                    keepalive
                  });
                  if (!response.ok || (await response.json()).received !== true) throw new Error("callback-unavailable");
                  delivered = true;
                }

                async function failed(error) {
                  busy = false;
                  if (error?.code === "auth/popup-blocked") {
                    status.textContent = options.copy.statusBlocked;
                    showButton(options.copy.Continue);
                    return;
                  }
                  const code = allowedErrors.has(error?.code) ? error.code : "auth/sign-in-failed";
                  try { await postResult({ error: code }); } catch { /* The native attempt may already be closed. */ }
                  await clearSession();
                  ended = true;
                  status.textContent = options.copy.notfinished;
                  showButton(options.copy.returnapp);
                }

                function attemptSignIn() {
                  if (ended) {
                    // This one-shot endpoint has closed; retry in YDKE for fresh callback state.
                    status.textContent = options.copy.retryhint;
                    return;
                  }
                  if (!startPopup || busy) return;
                  busy = true;
                  button.disabled = true;
                  status.textContent = options.copy.signingIn;
                  startPopup();
                }

                // The click starts signInWithPopup synchronously, without awaiting any other work first.
                button.addEventListener("click", attemptSignIn);
                window.addEventListener("pagehide", () => {
                  if (!delivered) void postResult({ error: "auth/page-closed" }, true).catch(() => {});
                });

                async function ready() {
                  try {
                    const [{ initializeApp }, {
                      initializeAuth, getAuth, setPersistence, inMemoryPersistence,
                      browserPopupRedirectResolver, GoogleAuthProvider, signInWithPopup, signOut
                    }] = await Promise.all([
                      import({{JsonSerializer.Serialize(FirebaseAppUrl)}}),
                      import({{JsonSerializer.Serialize(FirebaseAuthUrl)}})
                    ]);

                    const app = initializeApp(options.firebase);
                    // Initialize in memory before getAuth can probe browser persistence.
                    initializeAuth(app, {
                      persistence: inMemoryPersistence,
                      popupRedirectResolver: browserPopupRedirectResolver
                    });
                    const auth = getAuth(app);
                    await setPersistence(auth, inMemoryPersistence);
                    if (options.language) auth.languageCode = options.language;
                    else auth.useDeviceLanguage();
                    const provider = new GoogleAuthProvider();
                    clearSession = async () => {
                      // Firebase SDK memory only. Never Google logout or cookie deletion.
                      try { await signOut(auth); } catch { }
                    };

                    startPopup = () => {
                      let pending;
                      try { pending = signInWithPopup(auth, provider); }
                      catch (error) { void failed(error); return; }
                      void pending.then(async result => {
                        try {
                          const idToken = GoogleAuthProvider.credentialFromResult(result)?.idToken;
                          if (typeof idToken !== "string" || !idToken) {
                            await failed({ code: "auth/missing-id-token" });
                            return;
                          }
                          // This is Google's OAuth ID token, NOT a Firebase user ID token.
                          await postResult({ idToken });
                          ended = true;
                          button.hidden = true;
                          status.textContent = options.copy.responseSent;
                        } catch {
                          ended = true;
                          status.textContent = options.copy.callbackfailed;
                          showButton(options.copy.returnapp);
                        } finally {
                          await clearSession();
                          busy = false;
                        }
                      }).catch(failed);
                    };
                    attemptSignIn(); // Automatic first try; popup blockers require a real click.
                  } catch {
                    await failed({ code: "auth/sdk-load-failed" });
                  }
                }
                void ready();
              </script>
            </body>
            </html>
            """;
    }

    private static Dictionary<string, string> GetCopy(string language) => language switch
    {
        "tr" => new()
        {
            ["pageTitle"] = "Google ile giriş · YDKE",
            ["heading"] = "Google ile giriş yap",
            ["description"] = "Google, kendi sayfasında hesabını sorar. Sonra YDKE'ye geri dön.",
            ["statusPreparing"] = "Google hazırlanıyor…",
            ["statusBlocked"] = "Google penceresi açılmadı. Google ile devam et düğmesine bas.",
            ["Continue"] = "Google ile devam et",
            ["notfinished"] = "Giriş tamamlanmadı. Yeniden denemek için YDKE'ye dön.",
            ["returnapp"] = "YDKE'ye dön",
            ["retryhint"] = "YDKE'de Giriş yap düğmesine bas. Yeni açılan sekmeyi kullan.",
            ["signingIn"] = "Google'ın sayfasında giriş yap. YDKE'den iptal edebilirsin.",
            ["responseSent"] = "Google'ın yanıtı YDKE'ye gönderildi. YDKE'nin yanıtı kontrol etmesi için geri dön. Bu sekmeyi kapatabilirsin.",
            ["callbackfailed"] = "Google'ın yanıtı gönderilirken bir sorun oldu. Yeniden denemek için YDKE'ye dön.",
            ["footnote"] = "Yardım mı gerek? Bir büyüğünden yardım iste. YDKE'den iptal edebilirsin.",
            ["noscript"] = "Bu sayfa için JavaScript gerekli. Bir büyüğünden yardım iste veya YDKE'den iptal et.",
        },
        "de" => new()
        {
            ["pageTitle"] = "Mit Google anmelden · YDKE",
            ["heading"] = "Mit Google anmelden",
            ["description"] = "Google fragt auf seiner eigenen Seite nach deinem Konto. Komm danach zu YDKE zurück.",
            ["statusPreparing"] = "Google wird vorbereitet…",
            ["statusBlocked"] = "Das Google-Fenster ging nicht auf. Wähle Weiter mit Google.",
            ["Continue"] = "Weiter mit Google",
            ["notfinished"] = "Die Anmeldung ist noch nicht fertig. Geh zu YDKE zurück und versuche es noch einmal.",
            ["returnapp"] = "Zurück zu YDKE",
            ["retryhint"] = "Wähle in YDKE Anmelden. Nutze den neuen Browser-Tab.",
            ["signingIn"] = "Melde dich auf der Google-Seite an. Du kannst in YDKE abbrechen.",
            ["responseSent"] = "Googles Antwort wurde an YDKE gesendet. Geh zurück, damit YDKE sie prüfen kann. Du kannst diesen Tab schließen.",
            ["callbackfailed"] = "Beim Senden von Googles Antwort gab es ein Problem. Geh zu YDKE zurück und versuche es noch einmal.",
            ["footnote"] = "Brauchst du Hilfe? Frag einen Erwachsenen. Du kannst in YDKE abbrechen.",
            ["noscript"] = "Diese Seite braucht JavaScript. Frag einen Erwachsenen oder brich in YDKE ab.",
        },
        "fr" => new()
        {
            ["pageTitle"] = "Connexion Google · YDKE",
            ["heading"] = "Se connecter avec Google",
            ["description"] = "Google te demande ton compte sur sa propre page. Reviens ensuite dans YDKE.",
            ["statusPreparing"] = "Google se prépare…",
            ["statusBlocked"] = "La fenêtre Google ne s'est pas ouverte. Choisis Continuer avec Google.",
            ["Continue"] = "Continuer avec Google",
            ["notfinished"] = "La connexion n'est pas terminée. Reviens dans YDKE pour réessayer.",
            ["returnapp"] = "Revenir dans YDKE",
            ["retryhint"] = "Dans YDKE, choisis Se connecter. Utilise le nouvel onglet du navigateur.",
            ["signingIn"] = "Connecte-toi sur la page de Google. Tu peux annuler dans YDKE.",
            ["responseSent"] = "La réponse de Google a été envoyée à YDKE. Reviens pour que YDKE la vérifie. Tu peux fermer cet onglet.",
            ["callbackfailed"] = "Un problème est survenu en envoyant la réponse de Google. Reviens dans YDKE pour réessayer.",
            ["footnote"] = "Besoin d'aide ? Demande à un adulte. Tu peux annuler dans YDKE.",
            ["noscript"] = "Cette page a besoin de JavaScript. Demande à un adulte ou annule dans YDKE.",
        },
        "es" => new()
        {
            ["pageTitle"] = "Entrar con Google · YDKE",
            ["heading"] = "Entrar con Google",
            ["description"] = "Google te pide tu cuenta en su propia página. Después, vuelve a YDKE.",
            ["statusPreparing"] = "Preparando Google…",
            ["statusBlocked"] = "La ventana de Google no se abrió. Elige Continuar con Google.",
            ["Continue"] = "Continuar con Google",
            ["notfinished"] = "No se pudo iniciar sesión. Vuelve a YDKE para intentarlo otra vez.",
            ["returnapp"] = "Volver a YDKE",
            ["retryhint"] = "En YDKE, elige Iniciar sesión. Usa la nueva pestaña del navegador.",
            ["signingIn"] = "Inicia sesión en la página de Google. Puedes cancelar en YDKE.",
            ["responseSent"] = "La respuesta de Google se envió a YDKE. Vuelve para que YDKE la revise. Puedes cerrar esta pestaña.",
            ["callbackfailed"] = "Hubo un problema al enviar la respuesta de Google. Vuelve a YDKE para intentarlo otra vez.",
            ["footnote"] = "¿Necesitas ayuda? Pídesela a un adulto. Puedes cancelar en YDKE.",
            ["noscript"] = "Esta página necesita JavaScript. Pide ayuda a un adulto o cancela en YDKE.",
        },
        "pt" => new()
        {
            ["pageTitle"] = "Entrar com o Google · YDKE",
            ["heading"] = "Entrar com o Google",
            ["description"] = "O Google pede a sua conta na própria página. Depois, volte ao YDKE.",
            ["statusPreparing"] = "Preparando o Google…",
            ["statusBlocked"] = "A janela do Google não abriu. Escolha Continuar com o Google.",
            ["Continue"] = "Continuar com o Google",
            ["notfinished"] = "Não foi possível entrar. Volte ao YDKE para tentar de novo.",
            ["returnapp"] = "Voltar ao YDKE",
            ["retryhint"] = "No YDKE, escolha Entrar. Use a nova aba do navegador.",
            ["signingIn"] = "Entre com a sua conta na página do Google. Pode cancelar no YDKE.",
            ["responseSent"] = "A resposta do Google foi enviada ao YDKE. Volte para o YDKE conferir a resposta. Pode fechar esta aba.",
            ["callbackfailed"] = "Houve um problema ao enviar a resposta do Google. Volte ao YDKE para tentar de novo.",
            ["footnote"] = "Precisa de ajuda? Peça a um adulto. Pode cancelar no YDKE.",
            ["noscript"] = "Esta página precisa de JavaScript. Peça ajuda a um adulto ou cancele no YDKE.",
        },
        "nl" => new()
        {
            ["pageTitle"] = "Inloggen met Google · YDKE",
            ["heading"] = "Inloggen met Google",
            ["description"] = "Google vraagt op zijn eigen pagina om je account. Ga daarna terug naar YDKE.",
            ["statusPreparing"] = "Google wordt klaargezet…",
            ["statusBlocked"] = "Het Google-venster ging niet open. Kies Doorgaan met Google.",
            ["Continue"] = "Doorgaan met Google",
            ["notfinished"] = "Het inloggen is niet gelukt. Ga terug naar YDKE om het nog eens te proberen.",
            ["returnapp"] = "Terug naar YDKE",
            ["retryhint"] = "Kies Inloggen in YDKE. Gebruik het nieuwe tabblad.",
            ["signingIn"] = "Log in op de pagina van Google. Je kunt annuleren in YDKE.",
            ["responseSent"] = "Het antwoord van Google is naar YDKE gestuurd. Ga terug zodat YDKE het kan controleren. Je kunt dit tabblad sluiten.",
            ["callbackfailed"] = "Er ging iets mis bij het sturen van Googles antwoord. Ga terug naar YDKE om het nog eens te proberen.",
            ["footnote"] = "Hulp nodig? Vraag het aan een volwassene. Je kunt annuleren in YDKE.",
            ["noscript"] = "Deze pagina heeft JavaScript nodig. Vraag een volwassene om hulp of annuleer in YDKE.",
        },
        _ => new()
        {
            ["pageTitle"] = "Google sign-in · YDKE",
            ["heading"] = "Sign in with Google",
            ["description"] = "Google asks for your account on its own page. Then come back to YDKE.",
            ["statusPreparing"] = "Getting Google ready…",
            ["statusBlocked"] = "The Google window did not open. Choose Continue with Google.",
            ["Continue"] = "Continue with Google",
            ["notfinished"] = "Sign-in did not finish. Go back to YDKE to try again.",
            ["returnapp"] = "Return to YDKE",
            ["retryhint"] = "In YDKE, choose Sign in. Use the new browser tab.",
            ["signingIn"] = "Sign in on Google's page. You can cancel in YDKE.",
            ["responseSent"] = "Google's reply was sent to YDKE. Go back so YDKE can check it. You can close this tab.",
            ["callbackfailed"] = "There was a problem sending Google's reply. Go back to YDKE to try again.",
            ["footnote"] = "Need help? Ask a grown-up. You can cancel in YDKE.",
            ["noscript"] = "This page needs JavaScript. Ask a grown-up for help, or cancel in YDKE.",
        },
    };

    private static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language) || language.Length > 35) return "en";
        var parts = language.Split('-');
        if (parts.Any(part => part.Length == 0 || !part.All(char.IsAsciiLetterOrDigit))) return "en";
        var baseLanguage = parts[0].ToLowerInvariant();
        return baseLanguage is "en" or "tr" or "de" or "fr" or "es" or "pt" or "nl" ? baseLanguage : "en";
    }

    private static void ValidateRandomValue(string value)
    {
        if (value is not { Length: 43 } || value.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_')))
            throw new ArgumentException("An unpredictable base64url value is required.");
    }
}