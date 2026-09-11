using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using YDKE_Windows;

internal static partial class Program
{
    private const string BrowserTestToken = "synthetic-google-id-token.not-authenticated";
    private static readonly TimeSpan BrowserTestDeadline = TimeSpan.FromSeconds(5);

    private static void AddGoogleBrowserSignInTests(List<(string Name, Func<Task> Run)> tests)
    {
        tests.AddRange([
            ("Browser bridge: parent contract and five-minute default remain UI-free", BrowserBridgeContract),
            ("Browser bridge: Default uses exactly one Windows URL target", BrowserBridgeDefaultLaunch),
            ("Browser bridge: Edge receives exactly one local URL argument", () => BrowserBridgeExplicitLaunch(GoogleSignInBrowser.Edge)),
            ("Browser bridge: Chrome receives exactly one local URL argument", () => BrowserBridgeExplicitLaunch(GoogleSignInBrowser.Chrome)),
            ("Browser bridge: unsafe and normalized local URLs are rejected before discovery", BrowserBridgeRejectsUnsafeLaunchUrls),
            ("Browser bridge: missing and unknown browsers fail without weakening legacy HTTPS validation", BrowserBridgeLaunchFailures),
            ("Browser bridge: peer and Host guards exclude non-loopback and rebinding addresses", BrowserBridgeNetworkGuards),
            ("Browser bridge: listener serves requests inside the synchronous launch callback", BrowserBridgeListensBeforeLaunch),
            ("Browser bridge: real page has official SDK, hosted auth domain, no inputs and nonce CSP", BrowserBridgePageAndHeaders),
            ("Browser bridge: template escapes untrusted language and validates nonce/state syntax", BrowserBridgeTemplateEncoding),
            ("Browser bridge: seven languages cover encoded page copy and every sign-in state", BrowserBridgeLocalizedCopy),
            ("Browser bridge: valid synthetic token is returned without authenticating or persisting", BrowserBridgeReturnsUntrustedToken),
            ("Browser bridge: favicon, bad paths, query strings, methods and OPTIONS do not abort", BrowserBridgeRejectsRoutesAndMethods),
            ("Browser bridge: missing or mismatched Origin/state stay rejected until a valid callback", BrowserBridgeRejectsCsrf),
            ("Browser bridge: incorrect literal Host is rejected and cannot finish an attempt", BrowserBridgeRejectsHost),
            ("Browser bridge: empty, malformed, duplicate and mixed JSON payloads cannot finish", BrowserBridgeRejectsPayloads),
            ("Browser bridge: non-JSON media types, encodings and invalid UTF-8 cannot finish", BrowserBridgeRejectsContentTypes),
            ("Browser bridge: oversized fixed-length body is rejected before a valid callback", () => BrowserBridgeRejectsOversize(false)),
            ("Browser bridge: oversized chunked body is bounded before a valid callback", () => BrowserBridgeRejectsOversize(true)),
            ("Browser bridge: exact 64 KiB JSON body is accepted as untrusted transport data", BrowserBridgeExactBodyLimit),
            ("Browser bridge: allowlisted browser failures return generic codes, never cancellation", BrowserBridgeBrowserErrors),
            ("Browser bridge: concurrent attempts have independent paths, states and nonces", BrowserBridgeAttemptIsolation),
            ("Browser bridge: a completed callback cannot be accepted or replayed twice", BrowserBridgeRejectsReplay),
            ("Browser bridge: pre-cancellation never launches a browser", BrowserBridgeCancelledBeforeLaunch),
            ("Browser bridge: cancellation during launch releases the listener", BrowserBridgeCancelledDuringLaunch),
            ("Browser bridge: cancellation during GetContext wait releases the listener", BrowserBridgeCancelledDuringWait),
            ("Browser bridge: cancellation during partial body read releases all listener IO", () => BrowserBridgeInterruptedBody(false)),
            ("Browser bridge: timeout during partial body read returns an error and releases IO", () => BrowserBridgeInterruptedBody(true)),
            ("Browser bridge: timeout while waiting differs from native cancellation", BrowserBridgeWaitTimeout),
            ("Browser bridge: disconnected partial body does not abort a valid attempt", BrowserBridgeDisconnectedBody),
            ("Browser bridge: launch failure is sanitized and releases the registered prefix", BrowserBridgeLaunchException),
            ("Browser bridge: invalid timeouts fail before launch", BrowserBridgeInvalidTimeout),
        ]);
    }

    private static Task BrowserBridgeContract()
    {
        Func<CancellationToken, GoogleSignInBrowser, string, Task<GoogleBrowserSignInResult>> contract = GoogleBrowserSignIn.RequestIdTokenAsync;
        var parameters = contract.Method.GetParameters();
        Check(parameters.Length == 3, "parent method parameters changed");
        Check(Equals(GoogleSignInBrowser.Default, parameters[1].DefaultValue), "browser default changed");
        Check(Equals("en", parameters[2].DefaultValue), "language default changed");
        Equal(TimeSpan.FromMinutes(5), GoogleBrowserSignIn.DefaultTimeout);
        var properties = typeof(GoogleBrowserSignInResult).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        Check(properties.Select(p => p.Name).Order().SequenceEqual(new[] { "ErrorMessage", "IdToken" }), "result must not claim authentication state");
        Check(!new GoogleBrowserSignInResult(BrowserTestToken, null).ToString().Contains(BrowserTestToken, StringComparison.Ordinal), "record formatting must redact tokens");
        return Task.CompletedTask;
    }

    private static Uri BrowserTestPage() => new("http://localhost:49152/auth/" + new string('A', 43) + "/");

    private static Task BrowserBridgeDefaultLaunch()
    {
        var page = BrowserTestPage();
        var command = GoogleBrowserLauncher.CreateLocalSignInStartInfo(page, GoogleSignInBrowser.Default,
            _ => throw new InvalidOperationException("Default must use Windows URL handling, not executable discovery."));
        Check(command.FileName == page.AbsoluteUri, "shell target must be the exact local page");
        Check(command.UseShellExecute, "default must use Windows URL association");
        Equal(0, command.ArgumentList.Count);
        Equal(string.Empty, command.Arguments);
        return Task.CompletedTask;
    }

    private static Task BrowserBridgeExplicitLaunch(GoogleSignInBrowser browser)
    {
        var page = BrowserTestPage();
        var executable = TestExecutablePath(browser);
        var calls = 0;
        var command = GoogleBrowserLauncher.CreateLocalSignInStartInfo(page, browser, requested =>
        {
            Equal(browser, requested);
            calls++;
            return executable;
        });
        Equal(1, calls);
        Equal(executable, command.FileName);
        Check(!command.UseShellExecute, "explicit browser must bypass shell command parsing");
        Equal(1, command.ArgumentList.Count);
        Check(command.ArgumentList[0] == page.AbsoluteUri, "one unchanged URL argument is required");
        Equal(string.Empty, command.Arguments);
        return Task.CompletedTask;
    }

    private static async Task BrowserBridgeRejectsUnsafeLaunchUrls()
    {
        var path = "/auth/" + new string('A', 43) + "/";
        string[] invalid =
        [
            "relative", "javascript:alert(1)", "file:///C:/test.html", "https://localhost:49152" + path,
            "http://127.0.0.1:49152" + path, "http://[::1]:49152" + path, "http://0.0.0.0:49152" + path,
            "http://localhost.example.invalid:49152" + path, "http://localhost.:49152" + path,
            "http://example.invalid:49152" + path, "http://name@localhost:49152" + path,
            "http://localhost:0" + path, "http://localhost" + path, "http://localhost:80" + path,
            "http://LOCALHOST:49152" + path, "HTTP://localhost:49152" + path, "http://localhost:049152" + path,
            "http://localhost:49152/", "http://localhost:49152/auth/short/", "http://localhost:49152" + path + "result",
            "http://localhost:49152" + path.TrimEnd('/'), "http://localhost:49152" + path + "?",
            "http://localhost:49152" + path + "?extra=1", "http://localhost:49152" + path + "#",
            "http://localhost:49152" + path + "#fragment", "http://localhost:49152" + path + " --flag",
            "http://localhost:49152/other/.." + path, "http://localhost:49152//" + path.TrimStart('/'),
            "http://localhost:49152" + path.Replace("/auth/", "/auth%2F", StringComparison.Ordinal),
            "http://localhost:49152" + path.Replace("AAAA", "%41AAA", StringComparison.Ordinal),
            "http://localhost:49152/auth/" + new string('A', 44) + "/",
            "http://localhost:49152/auth/" + new string('A', 42) + "+/",
            @"http://localhost:49152\auth\" + new string('A', 43) + @"\",
        ];
        var discoveries = 0;
        foreach (var browser in Enum.GetValues<GoogleSignInBrowser>())
        {
            await Throws<ArgumentNullException>(() =>
            {
                GoogleBrowserLauncher.CreateLocalSignInStartInfo(null!, browser, _ => { discoveries++; return null; });
                return Task.CompletedTask;
            });
            foreach (var value in invalid)
            {
                // Some port/backslash forms are rejected by System.Uri itself.
                // There is no Uri to pass to the launcher in that case.
                if (!Uri.TryCreate(value, UriKind.RelativeOrAbsolute, out var uri)) continue;
                await Throws<ArgumentException>(() =>
                {
                    GoogleBrowserLauncher.CreateLocalSignInStartInfo(uri, browser, _ => { discoveries++; return null; });
                    return Task.CompletedTask;
                });
            }
        }
        Equal(0, discoveries);
    }

    private static async Task BrowserBridgeLaunchFailures()
    {
        var discoveries = 0;
        await Throws<ArgumentOutOfRangeException>(() =>
        {
            GoogleBrowserLauncher.CreateLocalSignInStartInfo(BrowserTestPage(), (GoogleSignInBrowser)999,
                _ => { discoveries++; return null; });
            return Task.CompletedTask;
        });
        Equal(0, discoveries);
        foreach (var browser in new[] { GoogleSignInBrowser.Edge, GoogleSignInBrowser.Chrome })
        {
            await Throws<InvalidOperationException>(() =>
            {
                GoogleBrowserLauncher.CreateLocalSignInStartInfo(BrowserTestPage(), browser, _ => null);
                return Task.CompletedTask;
            });
        }
        await Throws<ArgumentException>(() =>
        {
            GoogleBrowserLauncher.CreateStartInfo(BrowserTestPage().AbsoluteUri, GoogleSignInBrowser.Default, _ => null);
            return Task.CompletedTask;
        });
        var result = await GoogleBrowserSignIn.RequestIdTokenAsync(CancellationToken.None, (GoogleSignInBrowser)999);
        Check(result.IdToken is null && result.ErrorMessage is not null, "invalid browser must fail without opening anything");
    }

    private static Task BrowserBridgeNetworkGuards()
    {
        foreach (var address in new[] { "127.0.0.1", "127.0.0.2", "::1", "::ffff:127.0.0.1" })
            Check(GoogleBrowserSignIn.IsLoopbackPeer(IPAddress.Parse(address)), "loopback peer should be recognized");
        foreach (var address in new[] { "0.0.0.0", "::", "192.0.2.1", "10.0.0.1", "2001:db8::1", "::ffff:192.0.2.1" })
            Check(!GoogleBrowserSignIn.IsLoopbackPeer(IPAddress.Parse(address)), "non-loopback peer must be refused");
        Check(!GoogleBrowserSignIn.IsLoopbackPeer(null), "missing peer must fail closed");
        Check(GoogleBrowserSignIn.IsExpectedHost("localhost:49152", 49152), "exact host should match");
        foreach (var host in new string?[] { null, "", "localhost", "LOCALHOST:49152", "localhost.:49152", "localhost:049152",
            "localhost:49153", "127.0.0.1:49152", "[::1]:49152", "localhost.example.invalid:49152", "localhost:49152,localhost:49152" })
            Check(!GoogleBrowserSignIn.IsExpectedHost(host, 49152), "literal Host mismatch must fail closed");
        return Task.CompletedTask;
    }

    private static async Task BrowserBridgeListensBeforeLaunch()
    {
        using var http = BrowserHttpClient();
        Uri? capturedPage = null;
        var calls = 0;
        var result = await GoogleBrowserSignIn.RunAsync(CancellationToken.None, page =>
        {
            capturedPage = page;
            calls++;
            var html = http.GetStringAsync(page).GetAwaiter().GetResult();
            var state = BrowserPageState(html);
            using var request = BrowserPost(page, state, BrowserTokenPayload());
            using var response = http.SendAsync(request).GetAwaiter().GetResult();
            Check(response.StatusCode == HttpStatusCode.OK, "callback must work even during launch");
        }, TimeSpan.FromSeconds(10));
        Equal(1, calls);
        BrowserCheckToken(result);
        BrowserCheckPrefixReleased(capturedPage!);
    }

    private static async Task BrowserBridgePageAndHeaders()
    {
        await using var attempt = await BrowserAttempt.OpenAsync(language: "tr");
        using var response = await attempt.Http.GetAsync(attempt.Page);
        Equal(HttpStatusCode.OK, response.StatusCode);
        BrowserCheckHeaders(response);
        var html = await response.Content.ReadAsStringAsync();
        var nonce = Regex.Match(html, "<script nonce=\"([A-Za-z0-9_-]{43})\">").Groups[1].Value;
        var policy = response.Headers.GetValues("Content-Security-Policy").Single();
        Check(nonce.Length == 43 && policy.Contains("'nonce-" + nonce + "'", StringComparison.Ordinal), "nonce must match CSP");
        Equal(3, Regex.Matches(html, "nonce=\"" + Regex.Escape(nonce) + "\"").Count);
        Check(!policy.Contains("unsafe-inline", StringComparison.Ordinal) && !policy.Contains("unsafe-eval", StringComparison.Ordinal), "CSP must not allow unsafe inline/eval");
        Check(!policy.Contains('*'), "CSP must not allow wildcard origins");
        Check(policy.Contains("frame-ancestors 'none'", StringComparison.Ordinal), "iframes must be blocked");
        Check(policy.Contains("frame-src https://" + CloudConfig.FirebaseAuthDomain + "/__/auth/iframe", StringComparison.Ordinal), "only the hosted Firebase transport iframe is needed");
        Check(!Regex.IsMatch(html, @"<(?:input|form|textarea|select)\b", RegexOptions.IgnoreCase), "page must have no credential fields/forms");
        Check(html.Contains(GoogleSignInPage.FirebaseAppUrl, StringComparison.Ordinal)
            && html.Contains(GoogleSignInPage.FirebaseAuthUrl, StringComparison.Ordinal), "official pinned SDK modules required");
        Check(!html.Contains("createAuthUri", StringComparison.Ordinal) && !html.Contains("client_secret", StringComparison.Ordinal)
            && !html.Contains("client_id", StringComparison.Ordinal), "no direct OAuth flow or native client inputs");
        Check(html.Contains("GoogleAuthProvider.credentialFromResult(result)?.idToken", StringComparison.Ordinal), "must return Google, not Firebase, ID token");
        Check(html.Contains("initializeAuth(app", StringComparison.Ordinal) && html.IndexOf("initializeAuth(app", StringComparison.Ordinal)
            < html.IndexOf("const auth = getAuth(app)", StringComparison.Ordinal), "memory-only initialization must precede default getAuth");
        Check(html.Contains("persistence: inMemoryPersistence", StringComparison.Ordinal)
            && html.Contains("await setPersistence(auth, inMemoryPersistence)", StringComparison.Ordinal), "Firebase persistence must be memory-only");
        Check(html.Contains("await signOut(auth)", StringComparison.Ordinal), "local Firebase session must be cleared after delivery");
        Check(!Regex.IsMatch(html, @"(?:localStorage|sessionStorage|indexedDB|document\.cookie)\s*[.(=]"), "page must not read/write browser storage");
        Check(html.Contains("pending = signInWithPopup(auth, provider)", StringComparison.Ordinal)
            && html.Contains("button.addEventListener(\"click\", attemptSignIn)", StringComparison.Ordinal), "real click must synchronously start popup");
        Check(html.Contains("if (error?.code === \"auth/popup-blocked\")", StringComparison.Ordinal), "popup blockers must allow a gesture fallback");
        Check(!html.Contains("error.message", StringComparison.Ordinal) && !html.Contains("innerHTML", StringComparison.Ordinal), "SDK errors must never become raw UI/HTML");
        Check(html.Contains("status.textContent = options.copy.responseSent;", StringComparison.Ordinal), "transport delivery must use the localized pending-verification message");
        Check(!Regex.IsMatch(html, @"<[^>]+\son[a-z]+\s*=", RegexOptions.IgnoreCase), "page must not use inline event handlers");
        foreach (var variable in new[] { "bg", "bg-elevated", "surface", "surface-soft", "border", "border-strong", "text", "text-muted", "text-soft",
            "accent", "accent-hover", "accent-soft", "accent-fg", "success", "danger", "warning", "link", "shadow", "overlay", "panel", "panel-strong", "sheen", "highlight" })
            Equal(2, Regex.Matches(html, "--cp-" + variable + ":").Count);
        Check(html.Contains("\"Segoe UI\", Aptos, Calibri", StringComparison.Ordinal), "required system typography missing");
        Check(Regex.IsMatch(html, @"body\s*\{[^}]*font-size:\s*18px;"), "body text must be readable at 18px");
        Check(Regex.IsMatch(html, @"button\s*\{[^}]*min-height:\s*48px;"), "button must retain its 48px minimum height");
        Check(Regex.IsMatch(html, @"\.footnote\s*\{\s*margin: 24px 0 0;\s*\}"), "help text must inherit the readable body size");
        using var options = BrowserPageOptions(html);
        Equal(CloudConfig.FirebaseAuthDomain, options.RootElement.GetProperty("firebase").GetProperty("authDomain").GetString());
        Equal(CloudConfig.FirebaseProjectId, options.RootElement.GetProperty("firebase").GetProperty("projectId").GetString());
        Check(options.RootElement.GetProperty("firebase").GetProperty("apiKey").GetString() == CloudConfig.FirebaseApiKey, "must use the existing public API key");
        Equal("tr", options.RootElement.GetProperty("language").GetString());
        Check(attempt.State.Length == 43 && IsUrlSafeNoPadding(attempt.State), "state must be a random 32-byte base64url value");
        Check(attempt.State != nonce && !attempt.Page.AbsolutePath.Contains(attempt.State, StringComparison.Ordinal), "path, state and CSP nonce must be independent");
        Check(!attempt.Result.IsCompleted, "GET must not finish sign-in");
        await attempt.AcceptAsync();
    }

    private static async Task BrowserBridgeTemplateEncoding()
    {
        var state = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var nonce = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        const string hostileLanguage = "en\"</script><input name='injected'>\u2028";
        var html = GoogleSignInPage.CreateHtml(state, nonce, hostileLanguage);
        Check(!html.Contains(hostileLanguage, StringComparison.Ordinal) && !html.Contains("<input", StringComparison.Ordinal), "language cannot escape its serialized value");
        using var options = BrowserPageOptions(html);
        Equal("en", options.RootElement.GetProperty("language").GetString());
        using var browserLanguage = BrowserPageOptions(GoogleSignInPage.CreateHtml(state, nonce, ""));
        Equal("en", browserLanguage.RootElement.GetProperty("language").GetString());
        var languageCases = new (string? Language, string Expected)[]
        {
            (null, "en"), (" ", "en"), ("en-US", "en"), ("tr-TR", "tr"), ("TR", "tr"),
            ("DE-de", "de"), ("fr-CA", "fr"), ("es-MX", "es"), ("pt-BR", "pt"), ("nl-BE", "nl"),
            ("xx", "en"), ("xx-TR", "en"), ("tr_TR", "en"), ("-tr", "en"), ("tr-", "en"),
            ("tr--TR", "en"), ("tr\u2028", "en"), ("tr\"><input>", "en"), ("tr-" + new string('a', 33), "en"),
        };
        foreach (var item in languageCases)
        {
            var normalizedHtml = GoogleSignInPage.CreateHtml(state, nonce, item.Language!);
            using var normalizedOptions = BrowserPageOptions(normalizedHtml);
            Equal(item.Expected, normalizedOptions.RootElement.GetProperty("language").GetString());
            Check(normalizedHtml.Contains("<html lang=\"" + item.Expected + "\">", StringComparison.Ordinal), "HTML and Firebase must use the same normalized language");
            Check(!normalizedHtml.Contains("<input", StringComparison.Ordinal), "malformed language must not become markup");
        }
        await Throws<ArgumentException>(() =>
        {
            GoogleSignInPage.CreateHtml(state, "bad\"nonce", "en");
            return Task.CompletedTask;
        });
        await Throws<ArgumentException>(() =>
        {
            GoogleSignInPage.CreateContentSecurityPolicy("bad\r\nnonce");
            return Task.CompletedTask;
        });
    }

    private static Task BrowserBridgeLocalizedCopy()
    {
        var state = new string('A', 43);
        var nonce = new string('B', 43);
        string[] keys = ["pageTitle", "heading", "description", "statusPreparing", "statusBlocked", "Continue", "notfinished",
            "returnapp", "retryhint", "signingIn", "responseSent", "callbackfailed", "footnote", "noscript"];
        string[] statusKeys = ["statusBlocked", "notfinished", "retryhint", "signingIn", "responseSent", "callbackfailed"];
        var bindings = new (string Key, string Pattern)[]
        {
            ("pageTitle", @"<title>([^<]*)</title>"),
            ("heading", "<h1 id=\"title\">([^<]*)</h1>"),
            ("description", "<p class=\"description\">([^<]*)</p>"),
            ("statusPreparing", "<p id=\"status\"[^>]*>([^<]*)</p>"),
            ("Continue", "<button id=\"continue\"[^>]*>([^<]*)</button>"),
            ("footnote", "<p class=\"footnote\">([^<]*)</p>"),
            ("noscript", @"<noscript>([^<]*)</noscript>"),
        };
        var cases = new (string Language, string Heading, string Description, string ResponseSent)[]
        {
            ("en", "Sign in with Google",
                "Google asks for your account on its own page. Then come back to YDKE.",
                "Google's reply was sent to YDKE. Go back so YDKE can check it. You can close this tab."),
            ("tr", "Google ile giriş yap",
                "Google, kendi sayfasında hesabını sorar. Sonra YDKE'ye geri dön.",
                "Google'ın yanıtı YDKE'ye gönderildi. YDKE'nin yanıtı kontrol etmesi için geri dön. Bu sekmeyi kapatabilirsin."),
            ("de", "Mit Google anmelden",
                "Google fragt auf seiner eigenen Seite nach deinem Konto. Komm danach zu YDKE zurück.",
                "Googles Antwort wurde an YDKE gesendet. Geh zurück, damit YDKE sie prüfen kann. Du kannst diesen Tab schließen."),
            ("fr", "Se connecter avec Google",
                "Google te demande ton compte sur sa propre page. Reviens ensuite dans YDKE.",
                "La réponse de Google a été envoyée à YDKE. Reviens pour que YDKE la vérifie. Tu peux fermer cet onglet."),
            ("es", "Entrar con Google",
                "Google te pide tu cuenta en su propia página. Después, vuelve a YDKE.",
                "La respuesta de Google se envió a YDKE. Vuelve para que YDKE la revise. Puedes cerrar esta pestaña."),
            ("pt", "Entrar com o Google",
                "O Google pede a sua conta na própria página. Depois, volte ao YDKE.",
                "A resposta do Google foi enviada ao YDKE. Volte para o YDKE conferir a resposta. Pode fechar esta aba."),
            ("nl", "Inloggen met Google",
                "Google vraagt op zijn eigen pagina om je account. Ga daarna terug naar YDKE.",
                "Het antwoord van Google is naar YDKE gestuurd. Ga terug zodat YDKE het kan controleren. Je kunt dit tabblad sluiten."),
        };
        using var englishOptions = BrowserPageOptions(GoogleSignInPage.CreateHtml(state, nonce, "en"));
        var englishCopy = englishOptions.RootElement.GetProperty("copy");
        var englishClauses = englishCopy.EnumerateObject()
            .SelectMany(property => Regex.Split(property.Value.GetString()!, @"[.!?…]+"))
            .Select(clause => clause.Trim()).Where(clause => clause.Length >= 18).Distinct(StringComparer.Ordinal).ToArray();
        foreach (var item in cases)
        {
            var html = GoogleSignInPage.CreateHtml(state, nonce, item.Language);
            using var options = BrowserPageOptions(html);
            Equal(item.Language, options.RootElement.GetProperty("language").GetString());
            Check(html.Contains("<html lang=\"" + item.Language + "\">", StringComparison.Ordinal), "initial document language must match its copy");
            var copy = options.RootElement.GetProperty("copy");
            Equal(JsonValueKind.Object, copy.ValueKind);
            Check(keys.Order(StringComparer.Ordinal).SequenceEqual(copy.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)),
                $"{item.Language}: every page and state copy key must be present");
            Equal(item.Heading, copy.GetProperty("heading").GetString());
            Equal(item.Description, copy.GetProperty("description").GetString());
            Equal(item.ResponseSent, copy.GetProperty("responseSent").GetString());
            foreach (var key in keys)
            {
                var entry = copy.GetProperty(key);
                Equal(JsonValueKind.String, entry.ValueKind);
                var value = entry.GetString()!;
                Check(!string.IsNullOrWhiteSpace(value), $"{item.Language}: {key} must not be empty");
                if (item.Language == "en") continue;
                Check(value != englishCopy.GetProperty(key).GetString(), $"{item.Language}: {key} must not fall back to English");
                Check(!englishClauses.Any(clause => value.Contains(clause, StringComparison.OrdinalIgnoreCase)),
                    $"{item.Language}: {key} must translate whole clauses, not just brand names");
            }
            foreach (var binding in bindings)
            {
                var match = Regex.Match(html, binding.Pattern);
                Check(match.Success, $"{item.Language}: initial {binding.Key} markup is missing");
                Equal(WebUtility.HtmlEncode(copy.GetProperty(binding.Key).GetString()), match.Groups[1].Value);
            }
            Check(!Regex.IsMatch(options.RootElement.GetRawText(), @"[<>&'\u2028\u2029]"), "serialized copy must preserve HTML-safe JSON escaping");
            Equal(statusKeys.Length, Regex.Matches(html, @"status\.textContent\s*=").Count);
            foreach (var key in statusKeys)
                Check(html.Contains("status.textContent = options.copy." + key + ";", StringComparison.Ordinal), $"{item.Language}: {key} must use localized textContent");
            Equal(3, Regex.Matches(html, @"showButton\(options\.copy\.(?:Continue|returnapp)\);").Count);
            Check(options.RootElement.GetProperty("errors").EnumerateArray().Select(value => value.GetString()).SequenceEqual(GoogleBrowserSignIn.BrowserErrorCodes),
                "localization must not change the browser error allowlist");
        }
        return Task.CompletedTask;
    }

    private static async Task BrowserBridgeReturnsUntrustedToken()
    {
        await using var attempt = await BrowserAttempt.OpenAsync();
        await attempt.AcceptAsync();
        BrowserCheckPrefixReleased(attempt.Page);
    }

    private static async Task BrowserBridgeRejectsRoutesAndMethods()
    {
        await using var attempt = await BrowserAttempt.OpenAsync();
        var cases = new (HttpMethod Method, string Path, HttpStatusCode Status)[]
        {
            (HttpMethod.Get, "/favicon.ico", HttpStatusCode.NotFound),
            (HttpMethod.Get, "/", HttpStatusCode.NotFound),
            (HttpMethod.Get, attempt.Page.AbsolutePath + "result", HttpStatusCode.MethodNotAllowed),
            (HttpMethod.Get, attempt.Page.AbsolutePath + "?extra=1", HttpStatusCode.NotFound),
            (HttpMethod.Get, attempt.Page.AbsolutePath + "unknown", HttpStatusCode.NotFound),
            (HttpMethod.Post, attempt.Page.AbsolutePath, HttpStatusCode.MethodNotAllowed),
            (HttpMethod.Post, attempt.Page.AbsolutePath + "result?extra=1", HttpStatusCode.NotFound),
            (HttpMethod.Post, "/auth/other/result", HttpStatusCode.NotFound),
            (HttpMethod.Options, attempt.Page.AbsolutePath + "result", HttpStatusCode.MethodNotAllowed),
            (HttpMethod.Head, attempt.Page.AbsolutePath, HttpStatusCode.MethodNotAllowed),
            (HttpMethod.Put, attempt.Page.AbsolutePath + "result", HttpStatusCode.MethodNotAllowed),
            (HttpMethod.Delete, "/unknown", HttpStatusCode.MethodNotAllowed),
        };
        foreach (var item in cases)
        {
            using var request = BrowserPost(attempt.Page, attempt.State, BrowserTokenPayload());
            request.Method = item.Method;
            request.RequestUri = new Uri(attempt.Page, item.Path);
            if (item.Method != HttpMethod.Post) { request.Content?.Dispose(); request.Content = null; }
            using var response = await attempt.Http.SendAsync(request);
            Equal(item.Status, response.StatusCode);
            BrowserCheckHeaders(response);
            Check(!attempt.Result.IsCompleted, $"irrelevant {item.Method} request must not end sign-in");
        }
        await attempt.AcceptAsync();
    }

    private static async Task BrowserBridgeRejectsCsrf()
    {
        await using var attempt = await BrowserAttempt.OpenAsync();
        var origin = attempt.Page.GetLeftPart(UriPartial.Authority);
        var alteredState = (attempt.State[0] == 'A' ? "B" : "A") + attempt.State[1..];
        var cases = new (string Origin, string State)[]
        {
            ("", attempt.State), ("null", attempt.State), ("https://example.invalid", attempt.State),
            ("http://127.0.0.1:" + attempt.Page.Port, attempt.State),
            ("https://localhost:" + attempt.Page.Port, attempt.State), (origin + "/", attempt.State),
            (origin + ", " + origin, attempt.State), (origin, ""), (origin, "wrong"),
            (origin, alteredState), (origin, attempt.State + "x"), (origin, new string('!', 43)),
            (origin, attempt.State + "," + attempt.State),
        };
        foreach (var item in cases)
        {
            using var request = BrowserPost(attempt.Page, attempt.State, BrowserTokenPayload());
            request.Headers.Remove("Origin");
            request.Headers.Remove("X-YDKE-State");
            if (item.Origin.Length != 0) request.Headers.TryAddWithoutValidation("Origin", item.Origin);
            if (item.State.Length != 0) request.Headers.TryAddWithoutValidation("X-YDKE-State", item.State);
            using var response = await attempt.Http.SendAsync(request);
            Equal(HttpStatusCode.Forbidden, response.StatusCode);
            BrowserCheckHeaders(response);
            Check(!attempt.Result.IsCompleted, "failed CSRF must not end sign-in");
        }
        await attempt.AcceptAsync();
    }

    private static async Task BrowserBridgeRejectsHost()
    {
        await using var attempt = await BrowserAttempt.OpenAsync();
        foreach (var host in new[] { "localhost.example.invalid:" + attempt.Page.Port, "127.0.0.1:" + attempt.Page.Port,
            "localhost.:" + attempt.Page.Port, "LOCALHOST:" + attempt.Page.Port, "localhost:" + (attempt.Page.Port == 65535 ? 65534 : attempt.Page.Port + 1) })
        {
            foreach (var method in new[] { HttpMethod.Post, HttpMethod.Head })
            {
                using var request = BrowserPost(attempt.Page, attempt.State, BrowserTokenPayload());
                request.Method = method;
                if (method == HttpMethod.Head) { request.Content!.Dispose(); request.Content = null; }
                request.Headers.Host = host; // Destination still localhost; no external DNS/HTTP.
                using var response = await attempt.Http.SendAsync(request);
                // HTTP.sys can reject a Host before managed routing sees the request.
                Check(!response.IsSuccessStatusCode, "wrong Host must never be accepted");
                Check(!attempt.Result.IsCompleted, $"wrong Host on {method} must not end sign-in");
            }
        }
        await attempt.AcceptAsync();
    }

    private static async Task BrowserBridgeRejectsPayloads()
    {
        await using var attempt = await BrowserAttempt.OpenAsync();
        string[] payloads =
        [
            "", " ", "null", "[]", "42", "{}", "{", "{bad}",
            "{\"idToken\":null}", "{\"idToken\":\"\"}", "{\"idToken\":\" \"}",
            "{\"idToken\":42}", "{\"idToken\":true}", "{\"idToken\":[]}", "{\"idToken\":{}}",
            "{\"idToken\":\"token with space\"}", "{\"idToken\":\"token\\u0000\"}", "{\"idToken\":\"tökén\"}",
            "{\"idToken\":\"a\",\"idToken\":\"b\"}", "{\"idToken\":\"a\",\"idToken\":\"a\"}",
            "{\"idToken\":\"a\",\"\\u0069dToken\":\"b\"}",
            "{\"idToken\":\"a\",\"error\":\"auth/internal-error\"}", "{\"idToken\":\"a\",\"extra\":true}",
            "{\"idToken\":\"a\",}", "{\"idToken\":\"a\"}{}", "/* comment */{\"idToken\":\"a\"}",
            "{\"id_token\":\"a\"}", "{\"IdToken\":\"a\"}", "{\"error\":null}", "{\"error\":\"\"}",
            "{\"error\":\"test@example.invalid\"}", "{\"error\":\"<html>private details</html>\"}",
            "{\"error\":\"auth/unknown-error\"}", "{\"error\":\"auth/internal-error\",\"error\":\"auth/internal-error\"}",
            "{\"error\":\"auth/internal-error\",\"idToken\":null}", "{\"nested\":{\"a\":{\"b\":{\"c\":{}}}}}",
        ];
        foreach (var payload in payloads)
        {
            using var request = BrowserPost(attempt.Page, attempt.State, payload);
            using var response = await attempt.Http.SendAsync(request);
            Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Check(!attempt.Result.IsCompleted, "invalid payload must not end sign-in");
        }
        await attempt.AcceptAsync();
    }

    private static async Task BrowserBridgeRejectsContentTypes()
    {
        await using var attempt = await BrowserAttempt.OpenAsync();
        foreach (var contentType in new string?[] { null, "text/plain", "application/x-www-form-urlencoded", "multipart/form-data; boundary=test",
            "application/json; charset=utf-16", "application/json; charset=us-ascii", "application/json; extra=test", "application/json; charset=utf-8; charset=utf-8" })
        {
            using var request = BrowserPost(attempt.Page, attempt.State, BrowserTokenPayload());
            request.Content!.Headers.Remove("Content-Type");
            if (contentType is not null) request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
            using var response = await attempt.Http.SendAsync(request);
            Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
            Check(!attempt.Result.IsCompleted, "wrong content type must not end sign-in");
        }
        using (var request = BrowserPost(attempt.Page, attempt.State, BrowserTokenPayload()))
        {
            request.Content!.Headers.ContentEncoding.Add("gzip");
            using var response = await attempt.Http.SendAsync(request);
            Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        }
        using (var request = BrowserPost(attempt.Page, attempt.State, ""))
        {
            request.Content!.Dispose();
            request.Content = new ByteArrayContent([.. Encoding.ASCII.GetBytes("{\"idToken\":\""), 0xff, .. Encoding.ASCII.GetBytes("\"}")]);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            using var response = await attempt.Http.SendAsync(request);
            Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        Check(!attempt.Result.IsCompleted, "encoding errors must not end sign-in");
        await attempt.AcceptAsync();
    }

    private static async Task BrowserBridgeRejectsOversize(bool chunked)
    {
        await using var attempt = await BrowserAttempt.OpenAsync();
        using var request = BrowserPost(attempt.Page, attempt.State, BrowserTokenPayload(new string('a', GoogleBrowserSignIn.MaximumBodyBytes)));
        if (chunked) request.Headers.TransferEncodingChunked = true;
        using var response = await attempt.Http.SendAsync(request);
        Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Check(!attempt.Result.IsCompleted, "oversized body must not end sign-in");
        await attempt.AcceptAsync();
    }

    private static async Task BrowserBridgeExactBodyLimit()
    {
        await using var attempt = await BrowserAttempt.OpenAsync();
        var token = new string('a', GoogleBrowserSignIn.MaximumBodyBytes - Encoding.UTF8.GetByteCount(BrowserTokenPayload("")));
        var payload = BrowserTokenPayload(token);
        Equal(GoogleBrowserSignIn.MaximumBodyBytes, Encoding.UTF8.GetByteCount(payload));
        using var request = BrowserPost(attempt.Page, attempt.State, payload);
        using var response = await attempt.Http.SendAsync(request);
        Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await attempt.Result.WaitAsync(BrowserTestDeadline);
        Check(result.IdToken == token && result.ErrorMessage is null, "body at the exact limit should be transported, not authenticated");
        BrowserCheckPrefixReleased(attempt.Page);
    }

    private static async Task BrowserBridgeBrowserErrors()
    {
        foreach (var code in GoogleBrowserSignIn.BrowserErrorCodes)
        {
            await using var attempt = await BrowserAttempt.OpenAsync();
            using var request = BrowserPost(attempt.Page, attempt.State, JsonSerializer.Serialize(new { error = code }));
            using var response = await attempt.Http.SendAsync(request);
            Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await attempt.Result.WaitAsync(BrowserTestDeadline);
            Check(result.IdToken is null && result.ErrorMessage is not null, "browser failure must not be mistaken for native user cancellation");
            Check(result.ErrorMessage!.Contains(code, StringComparison.Ordinal), "only the allowlisted error code may be surfaced");
            Check(!result.ErrorMessage.Contains(attempt.State, StringComparison.Ordinal)
                && !result.ErrorMessage.Contains(attempt.Page.AbsoluteUri, StringComparison.Ordinal), "attempt secrets must not appear in errors");
            BrowserCheckPrefixReleased(attempt.Page);
        }
    }

    private static async Task BrowserBridgeAttemptIsolation()
    {
        await using var first = await BrowserAttempt.OpenAsync();
        await using var second = await BrowserAttempt.OpenAsync();
        Check(first.Page.AbsolutePath != second.Page.AbsolutePath && first.State != second.State, "attempt values must be independently random");
        Check(BrowserPageNonce(first.Html) != BrowserPageNonce(second.Html), "CSP nonces must be per attempt");
        using (var request = BrowserPost(second.Page, first.State, BrowserTokenPayload()))
        using (var response = await second.Http.SendAsync(request))
            Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using (var request = BrowserPost(second.Page, second.State, BrowserTokenPayload()))
        {
            request.RequestUri = new Uri(second.Page, first.Page.AbsolutePath + "result");
            using var response = await second.Http.SendAsync(request);
            Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        Check(!first.Result.IsCompleted && !second.Result.IsCompleted, "cross-attempt data must not finish either attempt");
        await first.AcceptAsync();
        await second.AcceptAsync();
    }

    private static async Task BrowserBridgeRejectsReplay()
    {
        await using var first = await BrowserAttempt.OpenAsync();
        await first.AcceptAsync();
        using (var request = BrowserPost(first.Page, first.State, BrowserTokenPayload("second-token-must-not-win")))
        {
            try
            {
                using var response = await first.Http.SendAsync(request);
                Check(!response.IsSuccessStatusCode, "a second callback must not be accepted");
            }
            catch (HttpRequestException) { /* The completed listener has been disposed. */ }
        }
        BrowserCheckToken(await first.Result);
        await using var second = await BrowserAttempt.OpenAsync();
        using (var request = BrowserPost(second.Page, first.State, BrowserTokenPayload()))
        using (var response = await second.Http.SendAsync(request))
            Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await second.AcceptAsync();
    }

    private static async Task BrowserBridgeCancelledBeforeLaunch()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var calls = 0;
        var result = await GoogleBrowserSignIn.RunAsync(cancellation.Token, _ => calls++, GoogleBrowserSignIn.DefaultTimeout);
        Equal(0, calls);
        BrowserCheckCancelled(result);
        BrowserCheckCancelled(await GoogleBrowserSignIn.RequestIdTokenAsync(cancellation.Token));
    }

    private static async Task BrowserBridgeCancelledDuringLaunch()
    {
        using var cancellation = new CancellationTokenSource();
        Uri? capturedPage = null;
        var result = await GoogleBrowserSignIn.RunAsync(cancellation.Token, page =>
        {
            capturedPage = page;
            cancellation.Cancel();
        }, GoogleBrowserSignIn.DefaultTimeout);
        Check(capturedPage is not null, "launch callback must run before cancellation");
        BrowserCheckCancelled(result);
        BrowserCheckPrefixReleased(capturedPage!);
    }

    private static async Task BrowserBridgeCancelledDuringWait()
    {
        await using var attempt = await BrowserAttempt.OpenAsync();
        attempt.Cancellation.Cancel();
        BrowserCheckCancelled(await attempt.Result.WaitAsync(BrowserTestDeadline));
        BrowserCheckPrefixReleased(attempt.Page);
    }

    private static async Task BrowserBridgeInterruptedBody(bool timeout)
    {
        await using var attempt = await BrowserAttempt.OpenAsync(timeout: timeout ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(10));
        using var partial = await BrowserPartialBody(attempt);
        if (!timeout) attempt.Cancellation.Cancel();
        var result = await attempt.Result.WaitAsync(BrowserTestDeadline);
        if (timeout) BrowserCheckTimeout(result);
        else BrowserCheckCancelled(result);
        BrowserCheckPrefixReleased(attempt.Page);
    }

    private static async Task BrowserBridgeWaitTimeout()
    {
        await using var attempt = await BrowserAttempt.OpenAsync(timeout: TimeSpan.FromMilliseconds(300), readPage: false);
        BrowserCheckTimeout(await attempt.Result.WaitAsync(BrowserTestDeadline));
        BrowserCheckPrefixReleased(attempt.Page);
    }

    private static async Task BrowserBridgeDisconnectedBody()
    {
        await using var attempt = await BrowserAttempt.OpenAsync();
        using (var partial = await BrowserPartialBody(attempt)) { }
        Check(!attempt.Result.IsCompleted, "disconnected request cannot finish sign-in");
        await attempt.AcceptAsync();
    }

    private static async Task BrowserBridgeLaunchException()
    {
        Uri? capturedPage = null;
        var result = await GoogleBrowserSignIn.RunAsync(CancellationToken.None, page =>
        {
            capturedPage = page;
            throw new InvalidOperationException("private-browser-details " + page.AbsoluteUri);
        }, TimeSpan.FromSeconds(10));
        Check(capturedPage is not null && result.IdToken is null && result.ErrorMessage is not null, "launch failure must return an error");
        Check(!result.ErrorMessage!.Contains("private-browser-details", StringComparison.Ordinal)
            && !result.ErrorMessage.Contains(capturedPage!.AbsoluteUri, StringComparison.Ordinal), "launch error must not leak details");
        BrowserCheckPrefixReleased(capturedPage!);
    }

    private static async Task BrowserBridgeInvalidTimeout()
    {
        var launches = 0;
        foreach (var timeout in new[] { TimeSpan.Zero, TimeSpan.FromMilliseconds(-1), TimeSpan.MaxValue })
            await Throws<ArgumentOutOfRangeException>(() => GoogleBrowserSignIn.RunAsync(CancellationToken.None, _ => launches++, timeout));
        Equal(0, launches);
    }

    private static HttpClient BrowserHttpClient() => new(new HttpClientHandler
    {
        UseProxy = false, UseCookies = false, AllowAutoRedirect = false,
    }) { Timeout = BrowserTestDeadline };

    private static string BrowserTokenPayload(string token = BrowserTestToken) => JsonSerializer.Serialize(new { idToken = token });

    private static HttpRequestMessage BrowserPost(Uri page, string state, string payload)
    {
        GoogleBrowserLauncher.ValidateLocalSignInPage(page);
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(page, "result"))
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Origin", page.GetLeftPart(UriPartial.Authority));
        request.Headers.Add("X-YDKE-State", state);
        request.Headers.ExpectContinue = false;
        return request;
    }

    private static JsonDocument BrowserPageOptions(string html)
    {
        var match = Regex.Match(html, @"const options = (\{[^\r\n]+\});");
        Check(match.Success, "real page must contain serialized per-attempt options");
        return JsonDocument.Parse(match.Groups[1].Value);
    }

    private static string BrowserPageState(string html)
    {
        using var options = BrowserPageOptions(html);
        return options.RootElement.GetProperty("state").GetString()!;
    }

    private static string BrowserPageNonce(string html) => Regex.Match(html, "<script nonce=\"([A-Za-z0-9_-]{43})\">").Groups[1].Value;

    private static void BrowserCheckToken(GoogleBrowserSignInResult result) =>
        Check(result.IdToken == BrowserTestToken && result.ErrorMessage is null, "expected only the untrusted synthetic Google token, no authentication state");

    private static void BrowserCheckCancelled(GoogleBrowserSignInResult result) =>
        Check(result.IdToken is null && result.ErrorMessage is null, "native user cancellation must return null token and null error");

    private static void BrowserCheckTimeout(GoogleBrowserSignInResult result) =>
        Check(result.IdToken is null && result.ErrorMessage?.Contains("timed out", StringComparison.OrdinalIgnoreCase) == true,
            "timeout must be a failure, not native user cancellation");

    private static void BrowserCheckHeaders(HttpResponseMessage response)
    {
        Check(response.Headers.CacheControl?.NoStore == true, "all app responses must be no-store");
        Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Equal("same-origin-allow-popups", response.Headers.GetValues("Cross-Origin-Opener-Policy").Single());
        Equal("same-origin", response.Headers.GetValues("Cross-Origin-Resource-Policy").Single());
        Check(!response.Headers.Any(h => h.Key.StartsWith("Access-Control-", StringComparison.OrdinalIgnoreCase)), "CORS must never be opened");
        Check(!response.Headers.Contains("Set-Cookie"), "listener must never set cookies");
    }

    private static void BrowserCheckPrefixReleased(Uri page)
    {
        // HTTP.sys may keep the TCP port open. Re-registering the exact owned prefix,
        // not assuming TCP connection refusal, proves this attempt released it.
        using var listener = new HttpListener();
        listener.Prefixes.Add(page.GetLeftPart(UriPartial.Authority) + "/");
        listener.Start();
        listener.Stop();
    }

    private static async Task<TcpClient> BrowserPartialBody(BrowserAttempt attempt)
    {
        var client = new TcpClient(AddressFamily.InterNetwork);
        try
        {
            await client.ConnectAsync(IPAddress.Loopback, attempt.Page.Port).WaitAsync(BrowserTestDeadline);
            var stream = client.GetStream();
            var headers = $"POST {attempt.Page.AbsolutePath}result HTTP/1.1\r\nHost: localhost:{attempt.Page.Port}\r\n"
                + $"Origin: {attempt.Page.GetLeftPart(UriPartial.Authority)}\r\nX-YDKE-State: {attempt.State}\r\n"
                + "Content-Type: application/json\r\nContent-Length: 4096\r\nExpect: 100-continue\r\nConnection: close\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(headers));
            // Wait for an actual body read, not a sleep or an assumed scheduling delay.
            var received = new StringBuilder();
            var one = new byte[1];
            using var deadline = new CancellationTokenSource(BrowserTestDeadline);
            while (!received.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal) && received.Length < 1024)
            {
                var count = await stream.ReadAsync(one, deadline.Token);
                Check(count == 1, "server must start receiving the partial request before interruption");
                received.Append((char)one[0]);
            }
            Check(received.ToString().StartsWith("HTTP/1.1 100", StringComparison.Ordinal), "body-read handshake was not reached");
            await stream.WriteAsync(Encoding.ASCII.GetBytes("{"));
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private sealed class BrowserAttempt : IAsyncDisposable
    {
        internal CancellationTokenSource Cancellation { get; } = new();
        internal HttpClient Http { get; } = BrowserHttpClient();
        internal Task<GoogleBrowserSignInResult> Result { get; }
        internal Uri Page { get; private set; } = null!;
        internal string Html { get; private set; } = "";
        internal string State { get; private set; } = "";
        private readonly TaskCompletionSource<Uri> _launched = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private BrowserAttempt(TimeSpan timeout, string language) => Result = GoogleBrowserSignIn.RunAsync(
            Cancellation.Token, page => _launched.TrySetResult(page), timeout, language);

        internal static async Task<BrowserAttempt> OpenAsync(TimeSpan? timeout = null, string language = "en", bool readPage = true)
        {
            var attempt = new BrowserAttempt(timeout ?? TimeSpan.FromSeconds(15), language);
            try
            {
                await Task.WhenAny(attempt._launched.Task, attempt.Result).WaitAsync(BrowserTestDeadline);
                Check(attempt._launched.Task.IsCompletedSuccessfully, "local listener failed to start before launch");
                attempt.Page = await attempt._launched.Task;
                GoogleBrowserLauncher.ValidateLocalSignInPage(attempt.Page);
                if (readPage)
                {
                    attempt.Html = await attempt.Http.GetStringAsync(attempt.Page);
                    attempt.State = BrowserPageState(attempt.Html);
                }
                return attempt;
            }
            catch
            {
                await attempt.DisposeAsync();
                throw;
            }
        }

        internal async Task AcceptAsync()
        {
            using var request = BrowserPost(Page, State, BrowserTokenPayload());
            using var response = await Http.SendAsync(request);
            Equal(HttpStatusCode.OK, response.StatusCode);
            BrowserCheckHeaders(response);
            using var acknowledgement = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Check(acknowledgement.RootElement.GetProperty("received").GetBoolean(), "only a delivery acknowledgement is expected");
            BrowserCheckToken(await Result.WaitAsync(BrowserTestDeadline));
        }

        public async ValueTask DisposeAsync()
        {
            Cancellation.Cancel();
            try { await Result.WaitAsync(BrowserTestDeadline); }
            finally { Http.Dispose(); Cancellation.Dispose(); }
        }
    }
}