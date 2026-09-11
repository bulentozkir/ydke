using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using YDKE_Windows;

internal static partial class Program
{
    private const string SyntheticClientId = "123456789012-ydke_test_desktop_client_00000001.apps.googleusercontent.com";
    private const string OtherSyntheticClientId = "987654321098-ydke_test_desktop_client_00000002.apps.googleusercontent.com";
    private const string AttemptRedirect = "http://127.0.0.1:49152/";
    private const string GoogleSignInJson = """{"id_token":"synthetic-google-id","access_token":"synthetic-access","expires_in":3600}""";
    private const string FirebaseSignInJson = """{"idToken":"synthetic-firebase-id","refreshToken":"synthetic-firebase-refresh","localId":"synthetic-uid","email":"test@example.invalid","displayName":"Test Account","expiresIn":"3600"}""";

    private static void AddGoogleSignInTests(List<(string Name, Func<Task> Run)> tests)
    {
        tests.AddRange([
            ("Client-ID validation accepts bounded, trimmed Google OAuth syntax without proving registration", ClientIdValidShapes),
            ("Client-ID validation rejects malformed, non-ASCII, unbounded and wrong-suffix input", ClientIdInvalidShapes),
            ("Malformed coordinator client IDs fail before the browser, HTTP or persistence", InvalidClientIdsHaveNoSideEffects),
            ("An unknown requested browser fails before the browser or HTTP", UnknownRequestedBrowserHasNoSideEffects),
            ("System-default browser command preserves shell URL handling without executable discovery", DefaultBrowserCommand),
            ("Edge command uses its resolved executable and one escaped URL argument", () => ExplicitBrowserCommand(GoogleSignInBrowser.Edge)),
            ("Chrome command uses its resolved executable and one escaped URL argument", () => ExplicitBrowserCommand(GoogleSignInBrowser.Chrome)),
            ("Missing Edge reports an actionable error without falling back or requesting installation", () => MissingBrowserCommand(GoogleSignInBrowser.Edge)),
            ("Missing Chrome reports an actionable error without falling back or requesting installation", () => MissingBrowserCommand(GoogleSignInBrowser.Chrome)),
            ("Browser commands reject unsafe URLs before any executable discovery", BrowserCommandsRejectUnsafeUrls),
            ("Browser command construction rejects unknown choices", BrowserCommandRejectsUnknownChoice),
            ("Executable discovery rejects PATH names, network paths, other executables and command strings", ExecutableDiscoveryRejectsUnsafeCandidates),
            ("Executable discovery skips stale paths and accepts quoted native, x86 and local installation paths", ExecutableDiscoveryUsesExistingCandidates),
            ("Form parsing distinguishes spaces from literal plus characters", FormParsingHandlesPlus),
            ("Browser preferences survive credential reload for Default, Edge and Chrome", BrowserPreferencesRoundTrip),
            ("Legacy and unknown stored browser values default safely without losing credentials", LegacyAndUnknownBrowserPreferences),
            ("A pre-cancelled credential save leaves the previous disconnected record unchanged", CancelledCredentialSavePreservesRecord),
            ("Pre-cancelled sign-in never invokes the browser or HTTP", () => CancelledSignInAtStage(CancelStage.BeforeStart)),
            ("Cancellation during browser authorization returns a clean disconnected result", () => CancelledSignInAtStage(CancelStage.DuringBrowser)),
            ("Cancellation after a successful callback never exchanges the code", () => CancelledSignInAtStage(CancelStage.AfterCallback)),
            ("Cancellation reaches the Google HTTP request and prevents connection", () => CancelledSignInAtStage(CancelStage.DuringGoogleRequest)),
            ("Cancellation after Google response disposal prevents Firebase sign-in", () => CancelledSignInAtStage(CancelStage.AfterGoogleResponse)),
            ("Cancellation reaches the Firebase HTTP request and prevents connection", () => CancelledSignInAtStage(CancelStage.DuringFirebaseRequest)),
            ("Cancellation after Firebase response disposal prevents credential persistence", () => CancelledSignInAtStage(CancelStage.AfterFirebaseResponse)),
            ("Direct Google and Firebase exchanges reject pre-cancelled calls without HTTP", AuthServicesHonorPreCancellation),
            ("An interrupted HTTP request without user cancellation reports a failure, not a user cancel", InterruptedRequestIsNotUserCancellation),
            ("Real authorization errors stay errors even if an unexpected code accompanies them", AuthorizationErrorsStayFailures),
            ("Google invalid_client retains the real HTTP error and explains Desktop OAuth setup", InvalidClientTokenExchangeIsActionable),
            ("Firebase exchange errors never connect or masquerade as cancellation", FirebaseExchangeFailureStaysDisconnected),
            ("Sign-in, sign-out, reload and second-account sign-in use fresh OAuth and the remembered browser", SignInSignOutReloadSecondAccount),
            ("Missing Google refresh token is retained for the same account and client only", () => RepeatSignInRefreshToken(true, true)),
            ("Missing Google refresh token is not borrowed from a different account", () => RepeatSignInRefreshToken(false, true)),
            ("Missing Google refresh token is not borrowed from a different OAuth client", () => RepeatSignInRefreshToken(true, false)),
            ("Missing Google refresh token is not borrowed when both account and client change", () => RepeatSignInRefreshToken(false, false)),
        ]);
    }

    private static Task ClientIdValidShapes()
    {
        foreach (var value in new[]
        {
            SyntheticClientId,
            OtherSyntheticClientId,
            $" \t{SyntheticClientId}\r\n",
            "1-a.apps.googleusercontent.com",
            new string('9', 20) + "-" + new string('A', 128) + ".apps.googleusercontent.com",
            "123456789012-Mixed_CASE-client_1234567890123456.apps.googleusercontent.com",
        })
            Check(GoogleAuthService.IsValidClientId(value), "valid syntax should not be rejected");
        return Task.CompletedTask;
    }

    private static IEnumerable<string?> InvalidClientIds() =>
    [
        null, "", " \t\r\n", "client-abc", "synthetic-project-id", "synthetic@example.invalid", "AIza-synthetic-firebase-key",
        "123456789012.apps.googleusercontent.com", "123456789012-.apps.googleusercontent.com",
        "-opaque.apps.googleusercontent.com", "notnumeric-opaque.apps.googleusercontent.com",
        "１２３456789012-opaque.apps.googleusercontent.com", "123456789012-nonascii_é.apps.googleusercontent.com",
        "123456789012-client portion.apps.googleusercontent.com", "123456789012-client+portion.apps.googleusercontent.com",
        "123456789012-client/portion.apps.googleusercontent.com", "123456789012-client.portion.apps.googleusercontent.com",
        "123456789012-opaque.APPS.GOOGLEUSERCONTENT.COM", SyntheticClientId + ".example.invalid", SyntheticClientId + "?extra=1",
        "https://" + SyntheticClientId, "{\"installed\":{\"client_id\":\"synthetic\"}}",
        new string('9', 21) + "-opaque.apps.googleusercontent.com",
        "123456789012-" + new string('a', 129) + ".apps.googleusercontent.com",
    ];

    private static Task ClientIdInvalidShapes()
    {
        foreach (var value in InvalidClientIds())
            Check(!GoogleAuthService.IsValidClientId(value), "malformed client ID must be rejected");
        return Task.CompletedTask;
    }

    private static async Task InvalidClientIdsHaveNoSideEffects()
    {
        using var folder = new TestFolder();
        await SaveDisconnectedConfiguration(folder);
        var before = await File.ReadAllTextAsync(folder.File("cloudcredentials.json"));
        var browserCalls = 0;
        GoogleLoopbackListener.Handler = (_, _, _, _, _) =>
        {
            browserCalls++;
            throw new InvalidOperationException("The fake browser must not run for malformed client IDs.");
        };
        using var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("No HTTP is allowed for malformed client IDs."));
        using var http = new HttpClient(handler);
        var coordinator = CreateCoordinator(folder, http);
        await coordinator.LoadCredentialsAsync();

        foreach (var value in InvalidClientIds())
        {
            var result = await coordinator.SignInAsync(value!, "synthetic-secret", browser: GoogleSignInBrowser.Chrome);
            Check(!result.Success, "invalid ID must not connect");
            Equal(GoogleAuthService.InvalidClientIdMessage, result.ErrorMessage);
            Check(result.ErrorMessage!.Contains("Desktop app", StringComparison.Ordinal), "missing setup guidance");
            if (!string.IsNullOrWhiteSpace(value))
                Check(!result.ErrorMessage.Contains(value, StringComparison.Ordinal), "invalid submitted input must not be echoed");
        }

        Equal(0, browserCalls);
        Equal(0, handler.CallCount);
        await CheckDisconnectedCredentials(coordinator, folder);
        Equal(SyntheticClientId, coordinator.LastGoogleClientId);
        Equal(GoogleSignInBrowser.Edge, coordinator.LastGoogleBrowser);
        Equal(before, await File.ReadAllTextAsync(folder.File("cloudcredentials.json")));
    }

    private static async Task UnknownRequestedBrowserHasNoSideEffects()
    {
        using var folder = new TestFolder();
        var browserCalls = 0;
        GoogleLoopbackListener.Handler = (_, _, _, _, _) =>
        {
            browserCalls++;
            throw new InvalidOperationException("An unknown browser must be rejected before authorization.");
        };
        using var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("No HTTP is allowed."));
        using var http = new HttpClient(handler);
        var coordinator = CreateCoordinator(folder, http);
        var result = await coordinator.SignInAsync(SyntheticClientId, null, browser: (GoogleSignInBrowser)999);
        Check(!result.Success, "unknown browser must fail");
        Check(result.ErrorMessage!.Contains("Choose System default", StringComparison.Ordinal), "missing browser guidance");
        Equal(0, browserCalls);
        Equal(0, handler.CallCount);
        await CheckDisconnectedCredentials(coordinator, folder);
        Check(!File.Exists(folder.File("cloudcredentials.json")), "failed sign-in must not create credentials");
    }

    private static string TestAuthorizationUrl() => GoogleAuthService.BuildAuthorizationUrl(
        SyntheticClientId, "challenge +/=&\"", "state & --flag=\"not-an-argument\"", AttemptRedirect);

    private static string TestExecutablePath(GoogleSignInBrowser browser, string root = "Fake Program Files") =>
        Path.Combine(AppContext.BaseDirectory, root,
            browser == GoogleSignInBrowser.Edge ? "Microsoft" : "Google",
            browser == GoogleSignInBrowser.Edge ? "Edge" : "Chrome", "Application",
            browser == GoogleSignInBrowser.Edge ? "msedge.exe" : "chrome.exe");

    private static Task DefaultBrowserCommand()
    {
        var url = TestAuthorizationUrl();
        var command = GoogleBrowserLauncher.CreateStartInfo(url, GoogleSignInBrowser.Default,
            _ => throw new InvalidOperationException("System default must not resolve an executable."));
        Equal(url, command.FileName);
        Check(command.UseShellExecute, "system default must use the Windows URL association");
        Equal(0, command.ArgumentList.Count);
        Equal(string.Empty, command.Arguments);
        return Task.CompletedTask;
    }

    private static Task ExplicitBrowserCommand(GoogleSignInBrowser browser)
    {
        var url = TestAuthorizationUrl();
        var executable = TestExecutablePath(browser);
        var resolutions = 0;
        var command = GoogleBrowserLauncher.CreateStartInfo(url, browser, requested =>
        {
            Equal(browser, requested);
            resolutions++;
            return executable;
        });
        Equal(1, resolutions);
        Equal(executable, command.FileName);
        Check(!command.UseShellExecute, "explicit browser must execute the resolved binary directly");
        Equal(1, command.ArgumentList.Count);
        Equal(url, command.ArgumentList[0]);
        Equal(string.Empty, command.Arguments);
        return Task.CompletedTask;
    }

    private static async Task MissingBrowserCommand(GoogleSignInBrowser browser)
    {
        var resolutions = 0;
        var ex = await Throws<InvalidOperationException>(() =>
        {
            GoogleBrowserLauncher.CreateStartInfo(TestAuthorizationUrl(), browser, requested =>
            {
                Equal(browser, requested);
                resolutions++;
                return null;
            });
            return Task.CompletedTask;
        });
        Equal(1, resolutions);
        Check(ex.Message.Contains(browser.ToString(), StringComparison.Ordinal), "error must name the requested browser");
        Check(ex.Message.Contains("Choose System default", StringComparison.Ordinal), "error must offer an available alternative");
        Check(!ex.Message.Contains("install ", StringComparison.OrdinalIgnoreCase), "error must not prompt for installation");
    }

    private static async Task BrowserCommandsRejectUnsafeUrls()
    {
        string?[] urls =
        [
            null, "", "not a URL", "/o/oauth2/v2/auth", "javascript:alert(1)", "file:///C:/fake/chrome.exe",
            "http://accounts.google.com/o/oauth2/v2/auth", "https://example.invalid/o/oauth2/v2/auth",
            "https://accounts.google.com.example.invalid/o/oauth2/v2/auth",
            "https://accounts.google.com@example.invalid/o/oauth2/v2/auth",
            "https://unexpected@accounts.google.com/o/oauth2/v2/auth",
            "https://accounts.google.com:8443/o/oauth2/v2/auth",
            "https://accounts.google.com/o/oauth2/v2/auth#fragment", "https://accounts.google.com/other/path",
            "https://accounts.google.com/o/oauth2/v2/auth/../other", CloudConfig.GoogleTokenEndpoint,
            " " + TestAuthorizationUrl(), TestAuthorizationUrl() + "\r\n", TestAuthorizationUrl() + " --incognito",
            @"https://accounts.google.com\o\oauth2\v2\auth",
        ];
        var resolutions = 0;
        foreach (var browser in Enum.GetValues<GoogleSignInBrowser>())
        {
            foreach (var url in urls)
            {
                await Throws<ArgumentException>(() =>
                {
                    GoogleBrowserLauncher.CreateStartInfo(url!, browser, _ =>
                    {
                        resolutions++;
                        return TestExecutablePath(browser);
                    });
                    return Task.CompletedTask;
                });
            }
        }
        Equal(0, resolutions);
    }

    private static async Task BrowserCommandRejectsUnknownChoice()
    {
        var resolutions = 0;
        await Throws<ArgumentOutOfRangeException>(() =>
        {
            GoogleBrowserLauncher.CreateStartInfo(TestAuthorizationUrl(), (GoogleSignInBrowser)999, _ =>
            {
                resolutions++;
                return TestExecutablePath(GoogleSignInBrowser.Chrome);
            });
            return Task.CompletedTask;
        });
        Equal(0, resolutions);
    }

    private static async Task ExecutableDiscoveryRejectsUnsafeCandidates()
    {
        foreach (var browser in new[] { GoogleSignInBrowser.Edge, GoogleSignInBrowser.Chrome })
        {
            var executable = TestExecutablePath(browser);
            var fileName = Path.GetFileName(executable);
            string?[] candidates =
            [
                null, "", " ", fileName, Path.Combine("relative", fileName),
                @"\\example.invalid\share\" + fileName,
                Path.Combine(AppContext.BaseDirectory, "cmd.exe"), executable + ".cmd", executable + " --extra",
                "\"" + executable + "\" --extra", executable + "\0", "\"" + executable,
            ];
            var probes = 0;
            var selected = GoogleBrowserLauncher.FindExecutable(browser, candidates, _ => { probes++; return true; });
            Check(selected is null, "untrusted candidates must not resolve a browser");
            Equal(0, probes);
            foreach (var candidate in candidates)
            {
                await Throws<InvalidOperationException>(() =>
                {
                    GoogleBrowserLauncher.CreateStartInfo(TestAuthorizationUrl(), browser, _ => candidate);
                    return Task.CompletedTask;
                });
            }
        }
    }

    private static Task ExecutableDiscoveryUsesExistingCandidates()
    {
        foreach (var browser in new[] { GoogleSignInBrowser.Edge, GoogleSignInBrowser.Chrome })
        {
            foreach (var root in new[] { "Fake Program Files", "Fake Program Files (x86)", "Fake LocalAppData" })
            {
                var stale = TestExecutablePath(browser, "Stale App Paths Entry");
                var installed = TestExecutablePath(browser, root);
                var probes = new List<string>();
                var selected = GoogleBrowserLauncher.FindExecutable(browser, [stale, " \"" + installed + "\" "], path =>
                {
                    probes.Add(path);
                    return path == installed;
                });
                Equal(installed, selected);
                Check(probes.SequenceEqual(new[] { stale, installed }), "discovery must skip a stale entry and check the next candidate");
            }
        }
        return Task.CompletedTask;
    }

    private static Task FormParsingHandlesPlus()
    {
        var parsed = ParseFormOrQuery("a+b=literal%2Bplus+and+space&empty=&encoded%2Bkey=%2B");
        Equal("literal+plus and space", parsed["a b"]);
        Equal(string.Empty, parsed["empty"]);
        Equal("+", parsed["encoded+key"]);
        return Task.CompletedTask;
    }

    private static async Task BrowserPreferencesRoundTrip()
    {
        using var folder = new TestFolder();
        var store = new CloudCredentialStore(folder.Path);
        Equal(GoogleSignInBrowser.Default, new CloudCredentials().GoogleBrowser);
        Equal(GoogleSignInBrowser.Default, (await store.LoadAsync()).GoogleBrowser);
        foreach (var browser in Enum.GetValues<GoogleSignInBrowser>())
        {
            await store.SaveAsync(new CloudCredentials
            {
                GoogleClientId = SyntheticClientId,
                GoogleClientSecret = "synthetic-secret",
                GoogleBrowser = browser,
            });
            var loaded = await new CloudCredentialStore(folder.Path).LoadAsync();
            Equal(browser, loaded.GoogleBrowser);
            Equal(SyntheticClientId, loaded.GoogleClientId);
        }
        await store.SaveAsync(new CloudCredentials { GoogleClientId = SyntheticClientId, GoogleBrowser = (GoogleSignInBrowser)999 });
        Equal(GoogleSignInBrowser.Default, (await store.LoadAsync()).GoogleBrowser);
    }

    private static async Task LegacyAndUnknownBrowserPreferences()
    {
        using var folder = new TestFolder();
        await SeedConnectedCredentialsAsync(folder);
        var seed = JsonNode.Parse(await File.ReadAllTextAsync(folder.File("cloudcredentials.json")))!.AsObject();
        string?[] values = [null, "999", "-1", "2147483648", "1.5", "\"UnknownBrowser\"", "\"999\"", "null", "{}", "[]"];
        foreach (var value in values)
        {
            var record = seed.DeepClone().AsObject();
            record.Remove("GoogleBrowser");
            if (value is not null) record["GoogleBrowser"] = JsonNode.Parse(value);
            await File.WriteAllTextAsync(folder.File("cloudcredentials.json"), record.ToJsonString());
            var loaded = await new CloudCredentialStore(folder.Path).LoadAsync();
            Equal(GoogleSignInBrowser.Default, loaded.GoogleBrowser);
            Equal(SyntheticClientId, loaded.GoogleClientId);
            Equal("client-secret", loaded.GoogleClientSecret);
            Equal("uid-77", loaded.FirebaseUid);
            Equal("google-rt-seed", loaded.GoogleRefreshToken);
            Equal("fb-rt-seed", loaded.FirebaseRefreshToken);
        }
        seed["GoogleBrowser"] = "Chrome";
        await File.WriteAllTextAsync(folder.File("cloudcredentials.json"), seed.ToJsonString());
        Equal(GoogleSignInBrowser.Chrome, (await new CloudCredentialStore(folder.Path).LoadAsync()).GoogleBrowser);
    }

    private static async Task CancelledCredentialSavePreservesRecord()
    {
        using var folder = new TestFolder();
        await SaveDisconnectedConfiguration(folder);
        var before = await File.ReadAllTextAsync(folder.File("cloudcredentials.json"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Throws<OperationCanceledException>(() => new CloudCredentialStore(folder.Path).SaveAsync(
            new CloudCredentials { FirebaseUid = "must-not-persist", FirebaseRefreshToken = "must-not-persist" }, cts.Token));
        Equal(before, await File.ReadAllTextAsync(folder.File("cloudcredentials.json")));
        Equal(0, Directory.GetFiles(folder.Path, "*.tmp").Length);
    }

    private enum CancelStage
    {
        BeforeStart,
        DuringBrowser,
        AfterCallback,
        DuringGoogleRequest,
        AfterGoogleResponse,
        DuringFirebaseRequest,
        AfterFirebaseResponse,
    }

    private static async Task CancelledSignInAtStage(CancelStage stage)
    {
        using var folder = new TestFolder();
        await SaveDisconnectedConfiguration(folder);
        var before = await File.ReadAllTextAsync(folder.File("cloudcredentials.json"));
        using var cts = new CancellationTokenSource();
        var browserCalls = 0;
        GoogleLoopbackListener.Handler = (_, _, _, token, browser) =>
        {
            browserCalls++;
            Equal(cts.Token, token);
            Equal(GoogleSignInBrowser.Chrome, browser);
            if (stage == CancelStage.DuringBrowser)
            {
                cts.Cancel();
                return Task.FromCanceled<GoogleAuthorizationResult>(token);
            }
            if (stage == CancelStage.AfterCallback) cts.Cancel();
            return Task.FromResult(new GoogleAuthorizationResult("synthetic-code", AttemptRedirect, null));
        };
        using var handler = new FakeHttpMessageHandler((request, token) =>
        {
            var isGoogle = request.RequestUri!.AbsoluteUri == CloudConfig.GoogleTokenEndpoint;
            Check(isGoogle || request.RequestUri.AbsoluteUri.StartsWith(CloudConfig.FirebaseSignInWithIdpEndpoint, StringComparison.Ordinal), "unexpected HTTP endpoint");
            var cancelDuringRequest = isGoogle ? stage == CancelStage.DuringGoogleRequest : stage == CancelStage.DuringFirebaseRequest;
            if (cancelDuringRequest)
            {
                Check(!token.IsCancellationRequested, "HTTP token was already cancelled");
                cts.Cancel();
                Check(token.IsCancellationRequested, "the caller's cancellation must reach the HTTP handler");
            }
            var cancelAfterResponse = isGoogle ? stage == CancelStage.AfterGoogleResponse : stage == CancelStage.AfterFirebaseResponse;
            var json = isGoogle ? GoogleSignInJson : FirebaseSignInJson;
            HttpContent content = cancelAfterResponse
                ? new CancelOnDisposeContent(json, cts)
                : new StringContent(json, Encoding.UTF8, "application/json");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });
        using var http = new HttpClient(handler);
        var coordinator = CreateCoordinator(folder, http);
        await coordinator.LoadCredentialsAsync();
        if (stage == CancelStage.BeforeStart) cts.Cancel();

        var result = await coordinator.SignInAsync(SyntheticClientId, "synthetic-secret", cts.Token, GoogleSignInBrowser.Chrome);
        Check(!result.Success, "cancelled sign-in must not report success");
        Equal("Sign-in was cancelled.", result.ErrorMessage);
        Equal(stage == CancelStage.BeforeStart ? 0 : 1, browserCalls);
        var expectedHttpCalls = stage switch
        {
            CancelStage.BeforeStart or CancelStage.DuringBrowser or CancelStage.AfterCallback => 0,
            CancelStage.DuringGoogleRequest or CancelStage.AfterGoogleResponse => 1,
            _ => 2,
        };
        Equal(expectedHttpCalls, handler.CallCount);
        await CheckDisconnectedCredentials(coordinator, folder);
        Equal(GoogleSignInBrowser.Edge, coordinator.LastGoogleBrowser);
        Equal(before, await File.ReadAllTextAsync(folder.File("cloudcredentials.json")));
        Equal(0, Directory.GetFiles(folder.Path, "*.tmp").Length);
    }

    private static async Task AuthServicesHonorPreCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        using var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("Pre-cancelled services must not use HTTP."));
        using var http = new HttpClient(handler);
        await Throws<OperationCanceledException>(() => new GoogleAuthService(http).ExchangeCodeAsync(
            "cid", null, "code", "verifier", AttemptRedirect, cts.Token));
        await Throws<OperationCanceledException>(() => new FirebaseAuthService(http).SignInWithGoogleIdTokenAsync("id-token", cts.Token));
        Equal(0, handler.CallCount);
    }

    private static async Task InterruptedRequestIsNotUserCancellation()
    {
        using var folder = new TestFolder();
        GoogleLoopbackListener.Handler = (_, _, _, _, _) => Task.FromResult(new GoogleAuthorizationResult("code", AttemptRedirect, null));
        using var handler = new FakeHttpMessageHandler(_ => Task.FromException<HttpResponseMessage>(new TaskCanceledException("Synthetic HTTP timeout.")));
        using var http = new HttpClient(handler);
        var coordinator = CreateCoordinator(folder, http);
        var result = await coordinator.SignInAsync(SyntheticClientId, null);
        Check(!result.Success, "an interrupted request must fail");
        Equal("The sign-in request was interrupted or timed out. Check your connection and try again.", result.ErrorMessage);
        Equal(1, handler.CallCount);
        await CheckDisconnectedCredentials(coordinator, folder);
        Check(!File.Exists(folder.File("cloudcredentials.json")), "interrupted sign-in must not persist credentials");
    }

    private static async Task AuthorizationErrorsStayFailures()
    {
        using var folder = new TestFolder();
        using var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("Authorization failures must never use HTTP."));
        using var http = new HttpClient(handler);
        var coordinator = CreateCoordinator(folder, http);
        foreach (var error in new[]
        {
            "Google reported an error: invalid_client (Client missing a project id.)",
            "Google reported an error: access_denied",
            "Sign-in response failed a security check and was rejected.",
            "Sign-in timed out waiting for your browser. Try again.",
            "Google did not return an authorization code.",
        })
        {
            foreach (var code in new string?[] { null, "unexpected-code-with-error" })
            {
                GoogleLoopbackListener.Handler = (_, _, _, _, _) => Task.FromResult(new GoogleAuthorizationResult(code, AttemptRedirect, error));
                var result = await coordinator.SignInAsync(SyntheticClientId, null);
                Check(!result.Success, "an authorization error must not become success");
                Equal(error, result.ErrorMessage);
            }
        }
        Equal(0, handler.CallCount);
        await CheckDisconnectedCredentials(coordinator, folder);
        Check(!File.Exists(folder.File("cloudcredentials.json")), "failed sign-in must not persist credentials");
    }

    private static async Task InvalidClientTokenExchangeIsActionable()
    {
        using var folder = new TestFolder();
        GoogleLoopbackListener.Handler = (_, _, _, _, _) => Task.FromResult(new GoogleAuthorizationResult("code", AttemptRedirect, null));
        using var handler = new FakeHttpMessageHandler(request =>
        {
            Equal(CloudConfig.GoogleTokenEndpoint, request.RequestUri!.AbsoluteUri);
            return Task.FromResult(JsonResponse(HttpStatusCode.Unauthorized,
                """{"error":"invalid_client","error_description":"Client missing a project id."}"""));
        });
        using var http = new HttpClient(handler);
        var coordinator = CreateCoordinator(folder, http);
        var result = await coordinator.SignInAsync(SyntheticClientId, "synthetic-submitted-secret");
        Check(!result.Success, "invalid_client must fail sign-in");
        foreach (var text in new[] { "401", "invalid_client", "Client missing a project id.", "Desktop app", "Google Cloud Console", "Firebase" })
            Check(result.ErrorMessage!.Contains(text, StringComparison.Ordinal), "missing OAuth error detail or setup guidance");
        Check(!result.ErrorMessage!.Contains("synthetic-submitted-secret", StringComparison.Ordinal), "error must not echo the client secret");
        Equal(1, handler.CallCount);
        await CheckDisconnectedCredentials(coordinator, folder);
        Check(!File.Exists(folder.File("cloudcredentials.json")), "invalid_client must not persist credentials");
    }

    private static async Task FirebaseExchangeFailureStaysDisconnected()
    {
        using var folder = new TestFolder();
        GoogleLoopbackListener.Handler = (_, _, _, _, _) => Task.FromResult(new GoogleAuthorizationResult("code", AttemptRedirect, null));
        using var handler = new FakeHttpMessageHandler(request => Task.FromResult(
            request.RequestUri!.AbsoluteUri == CloudConfig.GoogleTokenEndpoint
                ? JsonResponse(HttpStatusCode.OK, GoogleSignInJson)
                : JsonResponse(HttpStatusCode.BadRequest, """{"error":{"message":"INVALID_ID_TOKEN"}}""")));
        using var http = new HttpClient(handler);
        var coordinator = CreateCoordinator(folder, http);
        var result = await coordinator.SignInAsync(SyntheticClientId, null);
        Check(!result.Success, "Firebase failure must not connect");
        Check(result.ErrorMessage!.Contains("INVALID_ID_TOKEN", StringComparison.Ordinal), "Firebase error must be surfaced");
        Check(result.ErrorMessage != "Sign-in was cancelled.", "Firebase failure is not a cancellation");
        Equal(2, handler.CallCount);
        await CheckDisconnectedCredentials(coordinator, folder);
        Check(!File.Exists(folder.File("cloudcredentials.json")), "failed Firebase exchange must not persist credentials");
    }

    private static async Task SignInSignOutReloadSecondAccount()
    {
        using var folder = new TestFolder();
        const string settings = "{\"syntheticSetting\":\"preserve\"}";
        const string progress = "{\"syntheticProgress\":17}";
        const string clientSecret = "synthetic secret+literal";
        await File.WriteAllTextAsync(folder.File("settings.json"), settings);
        await File.WriteAllTextAsync(folder.File("progress.json"), progress);
        var attempts = new List<(string Challenge, string State, string Redirect, string Code)>();
        GoogleLoopbackListener.Handler = (clientId, challenge, state, _, browser) =>
        {
            Equal(SyntheticClientId, clientId);
            Equal(GoogleSignInBrowser.Chrome, browser);
            var attempt = attempts.Count + 1;
            var redirect = $"http://127.0.0.1:{49152 + attempt}/";
            var code = $"synthetic-auth-code-{attempt}";
            var query = ParseFormOrQuery(GoogleAuthService.BuildAuthorizationUrl(clientId, challenge, state, redirect));
            Equal(redirect, query["redirect_uri"]);
            Equal(state, query["state"]);
            Equal("select_account", query["prompt"]);
            attempts.Add((challenge, state, redirect, code));
            return Task.FromResult(new GoogleAuthorizationResult(code, redirect, null));
        };
        var googleCalls = 0;
        var firebaseCalls = 0;
        using var handler = new FakeHttpMessageHandler(async request =>
        {
            if (request.RequestUri!.AbsoluteUri == CloudConfig.GoogleTokenEndpoint)
            {
                googleCalls++;
                var attempt = attempts[^1];
                var form = ParseFormOrQuery(await request.Content!.ReadAsStringAsync());
                Equal(SyntheticClientId, form["client_id"]);
                Equal(clientSecret, form["client_secret"]);
                Equal("authorization_code", form["grant_type"]);
                Equal(attempt.Code, form["code"]);
                Equal(attempt.Redirect, form["redirect_uri"]);
                Equal(attempt.Challenge, Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(form["code_verifier"]))));
                var body = new JsonObject
                {
                    ["id_token"] = $"synthetic-google-id-{googleCalls}",
                    ["access_token"] = $"synthetic-access-{googleCalls}",
                    ["expires_in"] = 3600,
                };
                if (googleCalls == 1) body["refresh_token"] = "synthetic-google-refresh-first";
                return JsonResponse(HttpStatusCode.OK, body.ToJsonString());
            }
            Check(request.RequestUri.AbsoluteUri.StartsWith(CloudConfig.FirebaseSignInWithIdpEndpoint, StringComparison.Ordinal), "sign-in must not call refresh or Firestore endpoints");
            firebaseCalls++;
            var firebaseRequest = JsonNode.Parse(await request.Content!.ReadAsStringAsync())!.AsObject();
            var postBody = ParseFormOrQuery(firebaseRequest["postBody"]!.GetValue<string>());
            Equal($"synthetic-google-id-{firebaseCalls}", postBody["id_token"]);
            return JsonResponse(HttpStatusCode.OK, JsonSerializer.Serialize(new
            {
                idToken = $"synthetic-firebase-id-{firebaseCalls}",
                refreshToken = $"synthetic-firebase-refresh-{firebaseCalls}",
                localId = $"synthetic-uid-{firebaseCalls}",
                email = $"account{firebaseCalls}@example.invalid",
                displayName = $"Account {firebaseCalls}",
                expiresIn = "3600",
            }));
        });
        using var http = new HttpClient(handler);
        var coordinator = CreateCoordinator(folder, http);
        var first = await coordinator.SignInAsync($"  {SyntheticClientId}  ", clientSecret, browser: GoogleSignInBrowser.Chrome);
        Check(first.Success, first.ErrorMessage ?? "first sign-in failed");
        Check(coordinator.IsConnected, "first sign-in should connect");
        Equal("account1@example.invalid", coordinator.Email);
        Equal(GoogleSignInBrowser.Chrome, coordinator.LastGoogleBrowser);
        Equal("synthetic-google-refresh-first", (await new CloudCredentialStore(folder.Path).LoadAsync()).GoogleRefreshToken);

        await coordinator.SignOutAsync();
        await CheckDisconnectedCredentials(coordinator, folder);
        Equal(1, attempts.Count);
        Equal(1, googleCalls);
        Equal(1, firebaseCalls);
        Equal(2, handler.CallCount);
        Equal(SyntheticClientId, coordinator.LastGoogleClientId);
        Equal(clientSecret, coordinator.LastGoogleClientSecret);
        Equal(GoogleSignInBrowser.Chrome, coordinator.LastGoogleBrowser);
        var signedOutRecord = JsonNode.Parse(await File.ReadAllTextAsync(folder.File("cloudcredentials.json")))!.AsObject();
        Check(signedOutRecord["ProtectedGoogleRefreshToken"] is null, "Google token must be removed on disk");
        Check(signedOutRecord["ProtectedFirebaseRefreshToken"] is null, "Firebase token must be removed on disk");

        var reloaded = CreateCoordinator(folder, http);
        await reloaded.LoadCredentialsAsync();
        await CheckDisconnectedCredentials(reloaded, folder);
        Equal(GoogleSignInBrowser.Chrome, reloaded.LastGoogleBrowser);
        Equal(clientSecret, reloaded.LastGoogleClientSecret);
        var second = await reloaded.SignInAsync(reloaded.LastGoogleClientId!, reloaded.LastGoogleClientSecret, browser: reloaded.LastGoogleBrowser);
        Check(second.Success, second.ErrorMessage ?? "second sign-in failed");
        Check(reloaded.IsConnected, "second account should connect");
        Equal("account2@example.invalid", reloaded.Email);
        Equal("Account 2", reloaded.DisplayName);
        Equal(2, attempts.Count);
        Equal(2, googleCalls);
        Equal(2, firebaseCalls);
        Equal(4, handler.CallCount);
        Check(attempts[0].State != attempts[1].State, "each attempt needs fresh CSRF state");
        Check(attempts[0].Challenge != attempts[1].Challenge, "each attempt needs fresh PKCE");
        Check(attempts[0].Redirect != attempts[1].Redirect, "test must exercise distinct exact redirects");
        var saved = await new CloudCredentialStore(folder.Path).LoadAsync();
        Equal(SyntheticClientId, saved.GoogleClientId);
        Equal(clientSecret, saved.GoogleClientSecret);
        Equal(GoogleSignInBrowser.Chrome, saved.GoogleBrowser);
        Equal("synthetic-uid-2", saved.FirebaseUid);
        Equal("synthetic-firebase-refresh-2", saved.FirebaseRefreshToken);
        Check(saved.GoogleRefreshToken is null, "second account must not inherit the first account's Google token");
        Equal(settings, await File.ReadAllTextAsync(folder.File("settings.json")));
        Equal(progress, await File.ReadAllTextAsync(folder.File("progress.json")));
    }

    private static async Task RepeatSignInRefreshToken(bool sameAccount, bool sameClient)
    {
        using var folder = new TestFolder();
        await SeedConnectedCredentialsAsync(folder);
        var clientId = sameClient ? SyntheticClientId : OtherSyntheticClientId;
        var uid = sameAccount ? "uid-77" : "synthetic-other-uid";
        GoogleLoopbackListener.Handler = (requestedClientId, _, _, _, browser) =>
        {
            Equal(clientId, requestedClientId);
            Equal(GoogleSignInBrowser.Edge, browser);
            return Task.FromResult(new GoogleAuthorizationResult("synthetic-repeat-code", AttemptRedirect, null));
        };
        using var handler = new FakeHttpMessageHandler(async request =>
        {
            if (request.RequestUri!.AbsoluteUri == CloudConfig.GoogleTokenEndpoint)
            {
                var form = ParseFormOrQuery(await request.Content!.ReadAsStringAsync());
                Equal(clientId, form["client_id"]);
                Equal(AttemptRedirect, form["redirect_uri"]);
                return JsonResponse(HttpStatusCode.OK, GoogleSignInJson);
            }
            Check(request.RequestUri.AbsoluteUri.StartsWith(CloudConfig.FirebaseSignInWithIdpEndpoint, StringComparison.Ordinal), "unexpected HTTP endpoint");
            return JsonResponse(HttpStatusCode.OK, JsonSerializer.Serialize(new
            {
                idToken = "synthetic-new-firebase-id", refreshToken = "synthetic-new-firebase-refresh", localId = uid, expiresIn = "3600",
            }));
        });
        using var http = new HttpClient(handler);
        var coordinator = CreateCoordinator(folder, http);
        await coordinator.LoadCredentialsAsync();
        var result = await coordinator.SignInAsync($" \t{clientId}\r\n", "synthetic-secret", browser: GoogleSignInBrowser.Edge);
        Check(result.Success, result.ErrorMessage ?? "repeat sign-in failed");
        Equal(2, handler.CallCount);
        var saved = await new CloudCredentialStore(folder.Path).LoadAsync();
        Equal(uid, saved.FirebaseUid);
        Equal(clientId, saved.GoogleClientId);
        Equal(sameAccount && sameClient ? "google-rt-seed" : null, saved.GoogleRefreshToken);
        Equal("synthetic-new-firebase-refresh", saved.FirebaseRefreshToken);
        Equal(GoogleSignInBrowser.Edge, saved.GoogleBrowser);
    }

    private static CloudSyncCoordinator CreateCoordinator(TestFolder folder, HttpClient http) => new(
        new GoogleAuthService(http), new FirebaseAuthService(http), new FirestoreSyncService(http), new CloudCredentialStore(folder.Path));

    private static Task SaveDisconnectedConfiguration(TestFolder folder) => new CloudCredentialStore(folder.Path).SaveAsync(new CloudCredentials
    {
        GoogleClientId = SyntheticClientId,
        GoogleClientSecret = "synthetic-saved-secret",
        GoogleBrowser = GoogleSignInBrowser.Edge,
    });

    private static async Task CheckDisconnectedCredentials(CloudSyncCoordinator coordinator, TestFolder folder)
    {
        Check(!coordinator.IsConnected, "coordinator must remain disconnected");
        Check(coordinator.Email is null && coordinator.DisplayName is null, "disconnected coordinator must not retain account identity");
        var saved = await new CloudCredentialStore(folder.Path).LoadAsync();
        Check(saved.FirebaseUid is null && saved.Email is null && saved.DisplayName is null, "stored credentials must not retain account identity");
        Check(saved.GoogleRefreshToken is null && saved.FirebaseRefreshToken is null, "stored credentials must not retain refresh tokens");
    }

    // Disposal occurs after HTTP and JSON parsing succeed, exposing the final pre-persistence cancellation check.
    private sealed class CancelOnDisposeContent(string json, CancellationTokenSource cts) : StringContent(json, Encoding.UTF8, "application/json")
    {
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && !cts.IsCancellationRequested) cts.Cancel();
        }
    }
}