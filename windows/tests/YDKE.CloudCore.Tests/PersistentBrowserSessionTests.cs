using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using YDKE_Windows;

internal static partial class Program
{
    private const string PersistentGoogleToken = "synthetic-browser-google-id";
    private const string PersistentFirebaseToken = "synthetic-native-firebase-id";
    private const string PersistentRefreshToken = "synthetic-native-firebase-refresh";
    private const string SensitiveErrorMarker = "synthetic-sensitive-token-do-not-echo";
    private const string BrowserFailureMessage = "Google sign-in did not finish (auth/internal-error). Please try again from YDKE.";

    private static void AddPersistentBrowserSessionTests(List<(string Name, Func<Task> Run)> tests)
    {
        tests.AddRange([
            ("Persistent session: no-input method and optional per-instance bridge contract", PersistentBrowserContract),
            ("Persistent session: omitted arguments use the default browser and English", PersistentBrowserDefaultArguments),
            ("Persistent session: invalid browser fails before bridge, HTTP and persistence", PersistentBrowserInvalidChoice),
            ("Persistent session: Firebase verification and persistence precede connected status", PersistentBrowserTrustBoundary),
            ("Persistent session: new browser login removes legacy fields and encrypts only Firebase refresh", PersistentBrowserRecordAndEncryption),
            ("Persistent session: storage failure cannot replace an existing account", PersistentBrowserSaveFailure),
            ("Persistent session: repeated restarts refresh only Firebase and persist every rotation", PersistentRestartsAndRotation),
            ("Persistent session: sign-out and reload retain browser but no account or tokens", PersistentSignOutAndReload),
            ("Persistent session: missing UID or refresh token never reports connected", PersistentIncompleteSession),
            ("Persistent session: Firestore permission failures preserve the refreshed session", PersistentFirestorePermissionFailure),
            ("Persistent session: refresh UID changes fail closed before Firestore", PersistentRefreshUidMismatch),
            ("Persistent session: revoked session can sign in afresh as another account", PersistentReauthentication),
            ("Persistent session: sign-out wins over a pending browser callback", () => PersistentSignOutRace(false)),
            ("Persistent session: sign-out wins over a pending Firebase exchange", () => PersistentSignOutRace(true)),
            ("Persistent session: sign-out wins over a pending Firebase refresh", PersistentRefreshSignOutRace),
            ("Persistent session: a late revocation cannot clear a newer account", PersistentLateRevocation),
            ("Persistent session: typed Firebase errors expose only known codes and HTTP status", PersistentFirebaseErrorSanitization),
            ("Persistent session: malformed successful Firebase responses never expose tokens", PersistentFirebaseInvalidResponses),
            ("Persistent session: direct refresh cancellation never sends HTTP", PersistentRefreshPreCancellation),
        ]);
        foreach (var browser in Enum.GetValues<GoogleSignInBrowser>())
            tests.Add(($"Persistent session: {browser} receives caller cancellation and language", () => PersistentBrowserArguments(browser)));
        foreach (var failure in Enum.GetValues<PersistentSignInFailure>())
            tests.Add(($"Persistent session: {failure} preserves both empty and existing sessions", () => PersistentBrowserFailure(failure)));
        foreach (var code in new[] { "TOKEN_EXPIRED", "INVALID_REFRESH_TOKEN", "USER_DISABLED", "USER_NOT_FOUND" })
            tests.Add(($"Persistent session: {code} clears credentials on save, load and delete", () => PersistentRevocation(code)));
        foreach (var failure in Enum.GetValues<PersistentRefreshFailure>())
            tests.Add(($"Persistent session: {failure} retains a reusable offline session", () => PersistentTransientRefresh(failure)));
    }

    private static Task PersistentBrowserContract()
    {
        var method = typeof(CloudSyncCoordinator).GetMethod(nameof(CloudSyncCoordinator.SignInWithBrowserAsync))!;
        Equal(typeof(Task<CloudResult>), method.ReturnType);
        var parameters = method.GetParameters();
        Equal(3, parameters.Length);
        Check(parameters.Select(p => p.ParameterType).SequenceEqual(new[]
            { typeof(CancellationToken), typeof(GoogleSignInBrowser), typeof(string) }));
        Check(parameters.All(p => p.IsOptional), "all browser sign-in arguments must be optional");
        Equal(GoogleSignInBrowser.Default, parameters[1].DefaultValue);
        Equal("en", parameters[2].DefaultValue);
        var constructor = typeof(CloudSyncCoordinator).GetConstructors().Single();
        var injection = constructor.GetParameters()[^1];
        Equal("browserSignIn", injection.Name);
        Equal(typeof(Func<CancellationToken, GoogleSignInBrowser, string, Task<GoogleBrowserSignInResult>>), injection.ParameterType);
        Check(injection.IsOptional && injection.DefaultValue is null, "the real bridge must remain the default");
        using var fixture = new PersistentSessionFixture();
        using var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("Contract inspection must not send HTTP."));
        using var http = new HttpClient(handler);
        var defaultCoordinator = new CloudSyncCoordinator(new GoogleAuthService(http), new FirebaseAuthService(http),
            new FirestoreSyncService(http), fixture.Store);
        var bridge = (Delegate)typeof(CloudSyncCoordinator).GetField("_browserSignIn", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(defaultCoordinator)!;
        Func<CancellationToken, GoogleSignInBrowser, string, Task<GoogleBrowserSignInResult>> expected = GoogleBrowserSignIn.RequestIdTokenAsync;
        Equal(expected.Method, bridge.Method);
        return Task.CompletedTask;
    }

    private static async Task PersistentBrowserDefaultArguments()
    {
        using var fixture = new PersistentSessionFixture();
        fixture.Browser = (token, browser, language) =>
        {
            Equal(CancellationToken.None, token);
            Equal(GoogleSignInBrowser.Default, browser);
            Equal("en", language);
            return Task.FromResult(new GoogleBrowserSignInResult(PersistentGoogleToken, null));
        };
        fixture.Firebase = PersistentExchange;
        var result = await fixture.Coordinator.SignInWithBrowserAsync();
        Check(result.Success, result.ErrorMessage ?? "default sign-in failed");
        Equal(1, fixture.BrowserCalls);
        Equal(1, fixture.AuthCalls);
        fixture.CheckNoLegacyOrFirestore();
    }

    private static async Task PersistentBrowserArguments(GoogleSignInBrowser browser)
    {
        using var fixture = new PersistentSessionFixture();
        using var cts = new CancellationTokenSource();
        fixture.Browser = (token, selected, language) =>
        {
            Equal(cts.Token, token);
            Equal(browser, selected);
            Equal("tr", language);
            return Task.FromResult(new GoogleBrowserSignInResult(PersistentGoogleToken, null));
        };
        fixture.Firebase = PersistentExchange;
        Check((await fixture.Coordinator.SignInWithBrowserAsync(cts.Token, browser, "tr")).Success);
        Equal(browser, fixture.Coordinator.LastGoogleBrowser);
        Equal(browser, (await fixture.Store.LoadAsync()).GoogleBrowser);
        Equal(1, fixture.BrowserCalls);
        Equal(1, fixture.AuthCalls);
        fixture.CheckNoLegacyOrFirestore();
    }

    private static async Task PersistentBrowserInvalidChoice()
    {
        using var fixture = new PersistentSessionFixture();
        await fixture.SeedAsync();
        var before = await fixture.RawAsync();
        foreach (var browser in new[] { (GoogleSignInBrowser)(-1), (GoogleSignInBrowser)999 })
        {
            var result = await fixture.Coordinator.SignInWithBrowserAsync(browser: browser);
            Check(!result.Success && result.ErrorMessage!.Contains("Choose System default", StringComparison.Ordinal));
        }
        await fixture.CheckUnchangedAsync(before);
        Equal(0, fixture.BrowserCalls);
        Equal(0, fixture.AuthCalls);
        fixture.CheckNoLegacyOrFirestore();
    }

    private static async Task PersistentBrowserTrustBoundary()
    {
        using var fixture = new PersistentSessionFixture();
        var callback = new TaskCompletionSource<GoogleBrowserSignInResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var exchangeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exchange = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var googleToken = "synthetic." + Base64UrlEncode(Encoding.UTF8.GetBytes(
            """{"sub":"untrusted-callback-uid","email":"untrusted@example.invalid"}""")) + ".signature";
        fixture.Browser = (_, _, _) => callback.Task;
        fixture.Firebase = async (request, _) =>
        {
            await CheckPersistentExchangeRequest(request, googleToken);
            Check(!fixture.Coordinator.IsConnected, "a callback must not connect before Firebase verification");
            Check(await fixture.RawAsync() is null, "unverified callback must not be stored");
            exchangeStarted.TrySetResult();
            return await exchange.Task;
        };
        var signIn = fixture.Coordinator.SignInWithBrowserAsync();
        try
        {
            Check(!fixture.Coordinator.IsConnected && fixture.AuthCalls == 0);
            callback.SetResult(new GoogleBrowserSignInResult(googleToken, null));
            await exchangeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Check(!fixture.Coordinator.IsConnected && !signIn.IsCompleted);
            exchange.SetResult(PersistentSignInResponse());
            Check((await signIn).Success);
            Check(fixture.Coordinator.IsConnected);
            Equal("server@example.invalid", fixture.Coordinator.Email);
            Equal("server-uid", (await fixture.Store.LoadAsync()).FirebaseUid);
        }
        finally
        {
            callback.TrySetResult(new GoogleBrowserSignInResult(null, null));
            if (!exchange.Task.IsCompleted) exchange.TrySetResult(PersistentSignInResponse());
            await signIn;
        }
        fixture.CheckNoLegacyOrFirestore();
    }

    private static async Task PersistentBrowserRecordAndEncryption()
    {
        using var fixture = new PersistentSessionFixture();
        await fixture.SeedAsync();
        fixture.Firebase = PersistentExchange;
        Check((await fixture.Coordinator.SignInWithBrowserAsync(browser: GoogleSignInBrowser.Chrome)).Success);
        var raw = (await fixture.RawAsync())!;
        var json = JsonNode.Parse(raw)!.AsObject();
        Check(json.Select(p => p.Key).Order().SequenceEqual(new[]
            { "GoogleBrowser", "ProtectedFirebaseRefreshToken", "FirebaseUid", "Email", "DisplayName" }.Order()),
            "new session must not serialize any legacy Google fields or ID tokens");
        foreach (var secret in new[] { PersistentGoogleToken, PersistentFirebaseToken, PersistentRefreshToken, "client-secret", "google-rt-seed" })
            Check(!raw.Contains(secret, StringComparison.Ordinal), "plaintext token/legacy grant found on disk");
        var decrypted = ProtectedData.Unprotect(Convert.FromBase64String(json["ProtectedFirebaseRefreshToken"]!.GetValue<string>()),
            null, DataProtectionScope.CurrentUser);
        try { Equal(PersistentRefreshToken, Encoding.UTF8.GetString(decrypted)); }
        finally { CryptographicOperations.ZeroMemory(decrypted); }
        var saved = await new CloudCredentialStore(fixture.Folder.Path).LoadAsync();
        Equal(PersistentRefreshToken, saved.FirebaseRefreshToken);
        Equal("server-uid", saved.FirebaseUid);
        Check(saved.GoogleClientId is null && saved.GoogleClientSecret is null && saved.GoogleRefreshToken is null);
        Check(fixture.Coordinator.LastGoogleClientId is null && fixture.Coordinator.LastGoogleClientSecret is null);
        var backup = Encoding.UTF8.GetString(AppStorage.EncodeBackup(new UserSettings(), new ProgressState()));
        foreach (var value in new[] { PersistentRefreshToken, raw, json["ProtectedFirebaseRefreshToken"]!.GetValue<string>() })
            Check(!backup.Contains(value, StringComparison.Ordinal), "credentials must stay outside backups");
        Equal(0, Directory.GetFiles(fixture.Folder.Path, "*.tmp").Length);
        fixture.CheckNoLegacyOrFirestore();
    }

    private enum PersistentSignInFailure
    {
        PreCancelled, CancelledInBrowser, CancelledAfterCallback, CancelledInFirebase, CancelledAfterFirebaseResponse,
        NoIdToken, EmptyIdToken, WhitespaceIdToken, BrowserError, BrowserErrorWithToken, EmptyBrowserError,
        BrowserTimeout, BrowserTimeoutException, BrowserNetworkFailure, UnexpectedBrowserFailure,
        FirebaseTimeout, FirebaseNetworkFailure, ExpiredGoogleToken, RejectedGoogleToken, FirebasePermissionDenied,
        FirebaseServerError, MalformedFirebaseResponse,
    }

    private static async Task PersistentBrowserFailure(PersistentSignInFailure failure)
    {
        foreach (var seeded in new[] { false, true })
        {
            using var fixture = new PersistentSessionFixture();
            if (seeded) await fixture.SeedAsync();
            var before = await fixture.RawAsync();
            using var cts = new CancellationTokenSource();
            fixture.Browser = (token, browser, language) =>
            {
                Equal(cts.Token, token);
                Equal(GoogleSignInBrowser.Chrome, browser);
                Equal("de", language);
                if (failure == PersistentSignInFailure.CancelledInBrowser)
                {
                    cts.Cancel();
                    return Task.FromCanceled<GoogleBrowserSignInResult>(token);
                }
                if (failure == PersistentSignInFailure.CancelledAfterCallback) cts.Cancel();
                return failure switch
                {
                    PersistentSignInFailure.NoIdToken => Task.FromResult(new GoogleBrowserSignInResult(null, null)),
                    PersistentSignInFailure.EmptyIdToken => Task.FromResult(new GoogleBrowserSignInResult("", null)),
                    PersistentSignInFailure.WhitespaceIdToken => Task.FromResult(new GoogleBrowserSignInResult(" \t", null)),
                    PersistentSignInFailure.BrowserError => Task.FromResult(new GoogleBrowserSignInResult(null, BrowserFailureMessage)),
                    PersistentSignInFailure.BrowserErrorWithToken => Task.FromResult(new GoogleBrowserSignInResult(PersistentGoogleToken, BrowserFailureMessage)),
                    PersistentSignInFailure.EmptyBrowserError => Task.FromResult(new GoogleBrowserSignInResult(PersistentGoogleToken, "")),
                    PersistentSignInFailure.BrowserTimeout => Task.FromResult(new GoogleBrowserSignInResult(null, "Google sign-in timed out. Please try again.")),
                    PersistentSignInFailure.BrowserTimeoutException => throw new TimeoutException(SensitiveErrorMarker),
                    PersistentSignInFailure.BrowserNetworkFailure => throw new HttpRequestException(SensitiveErrorMarker),
                    PersistentSignInFailure.UnexpectedBrowserFailure => throw new InvalidOperationException(SensitiveErrorMarker),
                    _ => Task.FromResult(new GoogleBrowserSignInResult(PersistentGoogleToken, null)),
                };
            };
            fixture.Firebase = async (request, token) =>
            {
                await CheckPersistentExchangeRequest(request, PersistentGoogleToken);
                if (failure == PersistentSignInFailure.CancelledInFirebase)
                {
                    cts.Cancel();
                    Check(token.IsCancellationRequested, "caller cancellation must reach Firebase HTTP");
                    token.ThrowIfCancellationRequested();
                }
                return failure switch
                {
                    PersistentSignInFailure.CancelledAfterFirebaseResponse => new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new CancelOnDisposeContent(PersistentSignInBody(), cts),
                    },
                    PersistentSignInFailure.FirebaseTimeout => throw new TaskCanceledException(SensitiveErrorMarker),
                    PersistentSignInFailure.FirebaseNetworkFailure => throw new HttpRequestException(SensitiveErrorMarker),
                    PersistentSignInFailure.ExpiredGoogleToken => PersistentError(HttpStatusCode.BadRequest, "TOKEN_EXPIRED"),
                    PersistentSignInFailure.RejectedGoogleToken => PersistentError(HttpStatusCode.BadRequest, "INVALID_ID_TOKEN"),
                    PersistentSignInFailure.FirebasePermissionDenied => PersistentError(HttpStatusCode.Forbidden, "PERMISSION_DENIED"),
                    PersistentSignInFailure.FirebaseServerError => PersistentError(HttpStatusCode.ServiceUnavailable, "INTERNAL_ERROR"),
                    PersistentSignInFailure.MalformedFirebaseResponse => JsonResponse(HttpStatusCode.OK, "not JSON " + SensitiveErrorMarker),
                    _ => throw new InvalidOperationException("This failure must stop before Firebase HTTP."),
                };
            };
            if (failure == PersistentSignInFailure.PreCancelled) cts.Cancel();
            var result = await fixture.Coordinator.SignInWithBrowserAsync(cts.Token, GoogleSignInBrowser.Chrome, "de");
            Check(!result.Success && !string.IsNullOrWhiteSpace(result.ErrorMessage), "failed sign-in must return an error");
            var cancelled = failure is PersistentSignInFailure.PreCancelled or PersistentSignInFailure.CancelledInBrowser
                or PersistentSignInFailure.CancelledAfterCallback or PersistentSignInFailure.CancelledInFirebase
                or PersistentSignInFailure.CancelledAfterFirebaseResponse or PersistentSignInFailure.NoIdToken
                or PersistentSignInFailure.EmptyIdToken or PersistentSignInFailure.WhitespaceIdToken;
            Equal(cancelled, result.ErrorMessage == "Sign-in was cancelled.");
            if (failure is PersistentSignInFailure.BrowserTimeout or PersistentSignInFailure.BrowserTimeoutException or PersistentSignInFailure.FirebaseTimeout)
                Check(result.ErrorMessage!.Contains("timed out", StringComparison.Ordinal));
            if (failure is PersistentSignInFailure.BrowserError or PersistentSignInFailure.BrowserErrorWithToken)
                Equal(BrowserFailureMessage, result.ErrorMessage);
            Check(!result.ErrorMessage!.Contains(SensitiveErrorMarker, StringComparison.Ordinal), "error leaked sensitive details");
            var reachesFirebase = failure is PersistentSignInFailure.CancelledInFirebase or PersistentSignInFailure.CancelledAfterFirebaseResponse
                or PersistentSignInFailure.FirebaseTimeout or PersistentSignInFailure.FirebaseNetworkFailure or PersistentSignInFailure.ExpiredGoogleToken
                or PersistentSignInFailure.RejectedGoogleToken or PersistentSignInFailure.FirebasePermissionDenied
                or PersistentSignInFailure.FirebaseServerError or PersistentSignInFailure.MalformedFirebaseResponse;
            Equal(failure == PersistentSignInFailure.PreCancelled ? 0 : 1, fixture.BrowserCalls);
            Equal(reachesFirebase ? 1 : 0, fixture.AuthCalls);
            await fixture.CheckUnchangedAsync(before);
            Equal(seeded, fixture.Coordinator.IsConnected);
            fixture.CheckNoLegacyOrFirestore();
        }
    }

    private static async Task PersistentBrowserSaveFailure()
    {
        using var fixture = new PersistentSessionFixture();
        await fixture.SeedAsync();
        var before = await fixture.RawAsync();
        fixture.Firebase = PersistentExchange;
        using (var locked = new FileStream(fixture.Folder.File("cloudcredentials.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var result = await fixture.Coordinator.SignInWithBrowserAsync();
            Check(!result.Success, "a failed disk commit must not publish a new session");
        }
        await fixture.CheckUnchangedAsync(before);
        fixture.CheckNoLegacyOrFirestore();
    }

    private static async Task PersistentRestartsAndRotation()
    {
        using var fixture = new PersistentSessionFixture();
        fixture.Firebase = PersistentExchange;
        Check((await fixture.Coordinator.SignInWithBrowserAsync(browser: GoogleSignInBrowser.Chrome)).Success);
        File.SetLastWriteTimeUtc(fixture.Folder.File("cloudcredentials.json"), DateTime.UtcNow.AddYears(-5));
        var refreshToken = PersistentRefreshToken;
        var rotation = 0;
        fixture.Browser = (_, _, _) => throw new InvalidOperationException("Restart and sync must not launch a browser.");
        fixture.Firebase = async (request, _) =>
        {
            await CheckPersistentRefreshRequest(request, refreshToken);
            refreshToken = "synthetic-rotation-" + ++rotation;
            return PersistentRefreshResponse("server-uid", refreshToken, "synthetic-refreshed-id-" + rotation);
        };
        fixture.Firestore = (request, _) =>
        {
            Equal(ExpectedDocumentUrl("server-uid"), request.RequestUri!.AbsoluteUri);
            Equal("synthetic-refreshed-id-" + rotation, request.Headers.Authorization!.Parameter);
            return Task.FromResult(PersistentFirestoreSuccess(request));
        };
        foreach (var operation in new[] { "save", "load", "delete", "save" })
        {
            var restarted = fixture.CreateCoordinator();
            var authCalls = fixture.AuthCalls;
            await restarted.LoadCredentialsAsync();
            await restarted.LoadCredentialsAsync();
            Equal(authCalls, fixture.AuthCalls);
            Equal(1, fixture.BrowserCalls);
            Check(restarted.IsConnected);
            Equal("server@example.invalid", restarted.Email);
            Check((await PersistentCloudOperation(restarted, operation)).Success);
            var saved = await new CloudCredentialStore(fixture.Folder.Path).LoadAsync();
            Equal(refreshToken, saved.FirebaseRefreshToken);
            Equal("server-uid", saved.FirebaseUid);
            Equal("Server Account", saved.DisplayName);
            Equal(GoogleSignInBrowser.Chrome, saved.GoogleBrowser);
            Check(saved.GoogleClientId is null && saved.GoogleClientSecret is null && saved.GoogleRefreshToken is null);
        }
        Equal(4, rotation);
        Equal(5, fixture.AuthCalls);
        Equal(4, fixture.FirestoreCalls);
        Equal(0, fixture.GoogleCalls);
    }

    private static async Task PersistentSignOutAndReload()
    {
        using var fixture = new PersistentSessionFixture();
        fixture.Firebase = PersistentExchange;
        Check((await fixture.Coordinator.SignInWithBrowserAsync(browser: GoogleSignInBrowser.Edge)).Success);
        await fixture.Coordinator.SignOutAsync();
        await CheckDisconnectedCredentials(fixture.Coordinator, fixture.Folder);
        var stored = JsonNode.Parse((await fixture.RawAsync())!)!.AsObject();
        Check(stored.Count == 1 && stored.ContainsKey("GoogleBrowser"), "sign-out must store only the browser for new sessions");
        var restarted = fixture.CreateCoordinator();
        await restarted.LoadCredentialsAsync();
        await CheckDisconnectedCredentials(restarted, fixture.Folder);
        Equal(GoogleSignInBrowser.Edge, restarted.LastGoogleBrowser);
        foreach (var operation in new[] { "save", "load", "delete" })
            Check(!(await PersistentCloudOperation(restarted, operation)).Success);
        Equal(1, fixture.BrowserCalls);
        Equal(1, fixture.AuthCalls);
        fixture.CheckNoLegacyOrFirestore();
    }

    private static async Task PersistentIncompleteSession()
    {
        using var fixture = new PersistentSessionFixture();
        foreach (var credentials in new[]
        {
            new CloudCredentials { FirebaseUid = "synthetic-uid" },
            new CloudCredentials { FirebaseRefreshToken = "synthetic-token" },
            new CloudCredentials { FirebaseUid = " ", FirebaseRefreshToken = "synthetic-token" },
            new CloudCredentials { FirebaseUid = "synthetic-uid", FirebaseRefreshToken = " " },
        })
        {
            await fixture.Store.SaveAsync(credentials);
            await fixture.Coordinator.LoadCredentialsAsync();
            Check(!fixture.Coordinator.IsConnected);
            Check(!(await fixture.Coordinator.DeleteCloudProfileAsync()).Success);
        }
        Equal(0, fixture.AuthCalls);
        Equal(0, fixture.BrowserCalls);
        fixture.CheckNoLegacyOrFirestore();
    }

    private static async Task PersistentRevocation(string code)
    {
        foreach (var operation in new[] { "save", "load", "delete" })
        {
            using var fixture = new PersistentSessionFixture();
            await fixture.SeedAsync();
            fixture.Firebase = async (request, _) =>
            {
                await CheckPersistentRefreshRequest(request, "fb-rt-seed");
                return PersistentError(HttpStatusCode.BadRequest, code);
            };
            var result = await PersistentCloudOperation(fixture.Coordinator, operation);
            Check(!result.Success && result.ErrorMessage!.Contains("Sign in with Google again", StringComparison.Ordinal));
            Check(!result.ErrorMessage!.Contains(SensitiveErrorMarker, StringComparison.Ordinal));
            await CheckDisconnectedCredentials(fixture.Coordinator, fixture.Folder);
            Equal(GoogleSignInBrowser.Edge, fixture.Coordinator.LastGoogleBrowser);
            Equal(SyntheticClientId, fixture.Coordinator.LastGoogleClientId);
            Equal("client-secret", fixture.Coordinator.LastGoogleClientSecret);
            var restarted = fixture.CreateCoordinator();
            await restarted.LoadCredentialsAsync();
            await CheckDisconnectedCredentials(restarted, fixture.Folder);
            Check(!(await PersistentCloudOperation(restarted, operation)).Success);
            Equal(1, fixture.AuthCalls);
            Equal(0, fixture.BrowserCalls);
            fixture.CheckNoLegacyOrFirestore();
        }
    }

    private enum PersistentRefreshFailure
    {
        ServiceUnavailable, ServerErrorWithRevocationText, TooManyRequestsWithRevocationText,
        RequestTimeoutWithRevocationText, PermissionDenied, UnknownCodeWithRevocationDescription,
        NetworkUnavailable, HttpTimeout, MalformedResponse, IncompleteResponse,
    }

    private static async Task PersistentTransientRefresh(PersistentRefreshFailure failure)
    {
        using var fixture = new PersistentSessionFixture();
        await fixture.SeedAsync();
        var before = await fixture.RawAsync();
        fixture.Firebase = async (request, _) =>
        {
            await CheckPersistentRefreshRequest(request, "fb-rt-seed");
            return failure switch
            {
                PersistentRefreshFailure.ServiceUnavailable => PersistentError(HttpStatusCode.ServiceUnavailable, "UNAVAILABLE"),
                PersistentRefreshFailure.ServerErrorWithRevocationText => PersistentError(HttpStatusCode.InternalServerError, "USER_DISABLED"),
                PersistentRefreshFailure.TooManyRequestsWithRevocationText => PersistentError(HttpStatusCode.TooManyRequests, "TOKEN_EXPIRED"),
                PersistentRefreshFailure.RequestTimeoutWithRevocationText => PersistentError(HttpStatusCode.RequestTimeout, "INVALID_REFRESH_TOKEN"),
                PersistentRefreshFailure.PermissionDenied => PersistentError(HttpStatusCode.Forbidden, "PERMISSION_DENIED"),
                PersistentRefreshFailure.UnknownCodeWithRevocationDescription => JsonResponse(HttpStatusCode.BadRequest,
                    """{"error":"unknown_error","error_description":"TOKEN_EXPIRED","details":"USER_DISABLED"}"""),
                PersistentRefreshFailure.NetworkUnavailable => throw new HttpRequestException("Synthetic offline transport."),
                PersistentRefreshFailure.HttpTimeout => throw new TaskCanceledException("Synthetic refresh timeout."),
                PersistentRefreshFailure.MalformedResponse => JsonResponse(HttpStatusCode.OK, "not JSON " + SensitiveErrorMarker),
                PersistentRefreshFailure.IncompleteResponse => JsonResponse(HttpStatusCode.OK,
                    """{"id_token":"synthetic-id","refresh_token":"","user_id":"uid-77","expires_in":"3600"}"""),
                _ => throw new InvalidOperationException("Unexpected test case."),
            };
        };
        var result = await fixture.Coordinator.SaveToCloudAsync(new UserSettings(), new ProgressState());
        Check(!result.Success);
        Check(!result.ErrorMessage!.Contains(SensitiveErrorMarker, StringComparison.Ordinal));
        await fixture.CheckUnchangedAsync(before);
        Equal(0, fixture.FirestoreCalls);
        fixture.Firebase = async (request, _) =>
        {
            await CheckPersistentRefreshRequest(request, "fb-rt-seed");
            return PersistentRefreshResponse("uid-77", "synthetic-retry-rotation");
        };
        fixture.Firestore = (request, _) => Task.FromResult(PersistentFirestoreSuccess(request));
        var restarted = fixture.CreateCoordinator();
        await restarted.LoadCredentialsAsync();
        Check((await restarted.SaveToCloudAsync(new UserSettings(), new ProgressState())).Success);
        Equal("synthetic-retry-rotation", (await fixture.Store.LoadAsync()).FirebaseRefreshToken);
        Equal(2, fixture.AuthCalls);
        Equal(1, fixture.FirestoreCalls);
        Equal(0, fixture.BrowserCalls);
        Equal(0, fixture.GoogleCalls);
    }

    private static async Task PersistentFirestorePermissionFailure()
    {
        using var fixture = new PersistentSessionFixture();
        await fixture.SeedAsync();
        var token = "fb-rt-seed";
        fixture.Firebase = async (request, _) =>
        {
            await CheckPersistentRefreshRequest(request, token);
            token += "-rotated";
            return PersistentRefreshResponse("uid-77", token);
        };
        fixture.Firestore = (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.Forbidden,
            """{"error":{"message":"PERMISSION_DENIED; TOKEN_EXPIRED is not an auth-endpoint result"}}"""));
        foreach (var operation in new[] { "save", "load", "delete" })
        {
            Check(!(await PersistentCloudOperation(fixture.Coordinator, operation)).Success);
            Check(fixture.Coordinator.IsConnected);
            var saved = await fixture.Store.LoadAsync();
            Equal(token, saved.FirebaseRefreshToken);
            Equal("uid-77", saved.FirebaseUid);
        }
        Equal(3, fixture.AuthCalls);
        Equal(3, fixture.FirestoreCalls);
        Equal(0, fixture.BrowserCalls);
        Equal(0, fixture.GoogleCalls);
    }

    private static async Task PersistentRefreshUidMismatch()
    {
        using var fixture = new PersistentSessionFixture();
        await fixture.SeedAsync();
        fixture.Firebase = (_, _) => Task.FromResult(PersistentRefreshResponse("unexpected-server-uid", "must-not-persist"));
        var result = await fixture.Coordinator.DeleteCloudProfileAsync();
        Check(!result.Success && result.ErrorMessage!.Contains("Sign in with Google again", StringComparison.Ordinal));
        await CheckDisconnectedCredentials(fixture.Coordinator, fixture.Folder);
        Check(!(await fixture.RawAsync())!.Contains("unexpected-server-uid", StringComparison.Ordinal));
        Equal(GoogleSignInBrowser.Edge, fixture.Coordinator.LastGoogleBrowser);
        fixture.CheckNoLegacyOrFirestore();
    }

    private static async Task PersistentReauthentication()
    {
        using var fixture = new PersistentSessionFixture();
        var attempts = 0;
        fixture.Browser = (_, _, _) => Task.FromResult(new GoogleBrowserSignInResult(PersistentGoogleToken + ++attempts, null));
        fixture.Firebase = async (request, _) =>
        {
            if (request.RequestUri!.AbsoluteUri.StartsWith(CloudConfig.FirebaseRefreshEndpoint, StringComparison.Ordinal))
                return PersistentError(HttpStatusCode.BadRequest, "INVALID_REFRESH_TOKEN");
            await CheckPersistentExchangeRequest(request, PersistentGoogleToken + attempts);
            return PersistentSignInResponse("server-uid-" + attempts, "synthetic-refresh-" + attempts);
        };
        Check((await fixture.Coordinator.SignInWithBrowserAsync()).Success);
        Check(!(await fixture.Coordinator.DeleteCloudProfileAsync()).Success);
        Check(!fixture.Coordinator.IsConnected);
        Check((await fixture.Coordinator.SignInWithBrowserAsync(browser: GoogleSignInBrowser.Chrome)).Success);
        var saved = await fixture.Store.LoadAsync();
        Equal("server-uid-2", saved.FirebaseUid);
        Equal("synthetic-refresh-2", saved.FirebaseRefreshToken);
        Equal(GoogleSignInBrowser.Chrome, saved.GoogleBrowser);
        Equal(2, attempts);
        Equal(2, fixture.BrowserCalls);
        Equal(3, fixture.AuthCalls);
        fixture.CheckNoLegacyOrFirestore();
    }

    private static async Task PersistentSignOutRace(bool duringFirebase)
    {
        using var fixture = new PersistentSessionFixture();
        await fixture.SeedAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Browser = async (_, _, _) =>
        {
            if (!duringFirebase) { entered.TrySetResult(); await release.Task; }
            return new GoogleBrowserSignInResult(PersistentGoogleToken, null);
        };
        fixture.Firebase = async (request, token) =>
        {
            if (duringFirebase) { entered.TrySetResult(); await release.Task; }
            return await PersistentExchange(request, token);
        };
        var pending = fixture.Coordinator.SignInWithBrowserAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.Coordinator.SignOutAsync().WaitAsync(TimeSpan.FromSeconds(5));
            release.SetResult();
            Check(!(await pending).Success, "a late exchange must not resurrect a signed-out session");
        }
        finally { release.TrySetResult(); await pending; }
        await CheckDisconnectedCredentials(fixture.Coordinator, fixture.Folder);
        Equal(GoogleSignInBrowser.Edge, fixture.Coordinator.LastGoogleBrowser);
        fixture.CheckNoLegacyOrFirestore();
    }

    private static async Task PersistentRefreshSignOutRace()
    {
        using var fixture = new PersistentSessionFixture();
        await fixture.SeedAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Firebase = async (request, _) =>
        {
            await CheckPersistentRefreshRequest(request, "fb-rt-seed");
            entered.TrySetResult();
            await release.Task;
            return PersistentRefreshResponse("uid-77", "late-refresh-token");
        };
        var pending = fixture.Coordinator.DeleteCloudProfileAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await fixture.Coordinator.SignOutAsync().WaitAsync(TimeSpan.FromSeconds(5));
            release.SetResult();
            Check(!(await pending).Success);
        }
        finally { release.TrySetResult(); await pending; }
        await CheckDisconnectedCredentials(fixture.Coordinator, fixture.Folder);
        fixture.CheckNoLegacyOrFirestore();
    }

    private static async Task PersistentLateRevocation()
    {
        using var fixture = new PersistentSessionFixture();
        await fixture.SeedAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Firebase = async (request, token) =>
        {
            if (request.RequestUri!.AbsoluteUri.StartsWith(CloudConfig.FirebaseRefreshEndpoint, StringComparison.Ordinal))
            {
                entered.TrySetResult();
                await release.Task;
                return PersistentError(HttpStatusCode.BadRequest, "USER_NOT_FOUND");
            }
            return await PersistentExchange(request, token);
        };
        var pending = fixture.Coordinator.DeleteCloudProfileAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Check((await fixture.Coordinator.SignInWithBrowserAsync()).Success);
            var newRecord = await fixture.RawAsync();
            release.SetResult();
            Check(!(await pending).Success);
            Equal(newRecord, await fixture.RawAsync());
            Check(fixture.Coordinator.IsConnected);
            Equal("server-uid", (await fixture.Store.LoadAsync()).FirebaseUid);
        }
        finally { release.TrySetResult(); await pending; }
        fixture.CheckNoLegacyOrFirestore();
    }

    private static async Task PersistentFirebaseErrorSanitization()
    {
        var cases = new (HttpStatusCode Status, string Body, string? Code, bool Revoked)[]
        {
            (HttpStatusCode.BadRequest, """{"error":{"message":"INVALID_ID_TOKEN : synthetic-sensitive-token-do-not-echo","idToken":"synthetic-sensitive-token-do-not-echo"}}""", "INVALID_ID_TOKEN", false),
            (HttpStatusCode.BadRequest, """{"error":"INVALID_REFRESH_TOKEN","error_description":"synthetic-sensitive-token-do-not-echo"}""", "INVALID_REFRESH_TOKEN", true),
            (HttpStatusCode.Unauthorized, """{"error":{"message":"TOKEN_EXPIRED"}}""", "TOKEN_EXPIRED", true),
            (HttpStatusCode.Forbidden, """{"error":{"message":"USER_DISABLED"}}""", "USER_DISABLED", true),
            (HttpStatusCode.NotFound, """{"error":{"message":"USER_NOT_FOUND"}}""", "USER_NOT_FOUND", true),
            (HttpStatusCode.ServiceUnavailable, """{"error":{"message":"USER_DISABLED"}}""", "USER_DISABLED", false),
            (HttpStatusCode.TooManyRequests, """{"error":{"message":"TOKEN_EXPIRED"}}""", "TOKEN_EXPIRED", false),
            (HttpStatusCode.Forbidden, """{"error":{"message":"PERMISSION_DENIED : synthetic-sensitive-token-do-not-echo","errors":[{"refreshToken":"synthetic-sensitive-token-do-not-echo"}]}}""", "PERMISSION_DENIED", false),
            (HttpStatusCode.BadRequest, """{"error":"invalid_grant","error_description":"TOKEN_EXPIRED synthetic-sensitive-token-do-not-echo"}""", "invalid_grant", false),
            (HttpStatusCode.BadRequest, """{"error_description":"TOKEN_EXPIRED","message":"USER_DISABLED synthetic-sensitive-token-do-not-echo"}""", null, false),
            (HttpStatusCode.BadRequest, """{"error":{"message":"synthetic-sensitive-token-do-not-echo TOKEN_EXPIRED"}}""", null, false),
            (HttpStatusCode.BadRequest, """{"error":{"message":{"token":"synthetic-sensitive-token-do-not-echo"}}}""", null, false),
            (HttpStatusCode.BadRequest, """{"error":["TOKEN_EXPIRED","synthetic-sensitive-token-do-not-echo"]}""", null, false),
            (HttpStatusCode.BadRequest, """{"error":42}""", null, false),
            (HttpStatusCode.BadRequest, """{"synthetic-sensitive-token-do-not-echo":1,"synthetic-sensitive-token-do-not-echo":2,"error":{"message":"TOKEN_EXPIRED"}}""", null, false),
            (HttpStatusCode.BadRequest, """{"error":{"synthetic-sensitive-token-do-not-echo":1,"synthetic-sensitive-token-do-not-echo":2,"message":"TOKEN_EXPIRED"}}""", null, false),
            (HttpStatusCode.BadRequest, """{"error":{"message":"TOKEN_EXPIRED","message":"INVALID_ID_TOKEN"}}""", null, false),
            (HttpStatusCode.BadRequest, "<html>synthetic-sensitive-token-do-not-echo</html>", null, false),
            (HttpStatusCode.BadRequest, "\"synthetic-sensitive-token-do-not-echo\"", null, false),
        };
        foreach (var test in cases)
        {
            using var handler = new FakeHttpMessageHandler(_ => Task.FromResult(JsonResponse(test.Status, test.Body)));
            using var http = new HttpClient(handler);
            var auth = new FirebaseAuthService(http);
            foreach (var refresh in new[] { false, true })
            {
                var ex = await Throws<FirebaseAuthException>(() => refresh
                    ? auth.RefreshAsync("synthetic-refresh") : auth.SignInWithGoogleIdTokenAsync(PersistentGoogleToken));
                Equal(test.Status, ex.StatusCode);
                Equal(test.Code, ex.ErrorCode);
                Equal(test.Revoked, ex.IsSessionRevoked);
                Check(ex.Message.Contains(((int)test.Status).ToString(), StringComparison.Ordinal));
                Check(!ex.ToString().Contains(SensitiveErrorMarker, StringComparison.Ordinal), "typed errors must never carry raw tokens");
                Check(ex.InnerException is null);
            }
        }
    }

    private static async Task PersistentFirebaseInvalidResponses()
    {
        foreach (var refresh in new[] { false, true })
        {
            var valid = refresh
                ? """{"id_token":"synthetic-id","refresh_token":"synthetic-refresh","user_id":"synthetic-uid","expires_in":"3600"}"""
                : PersistentSignInBody();
            var fields = refresh ? new[] { "id_token", "refresh_token", "user_id", "expires_in" }
                : new[] { "idToken", "refreshToken", "localId", "expiresIn" };
            var bodies = new List<string>
            {
                "not JSON " + SensitiveErrorMarker, "[]", "null", "\"" + SensitiveErrorMarker + "\"",
                "{\"" + SensitiveErrorMarker + "\":1,\"" + SensitiveErrorMarker + "\":2," + valid[1..],
            };
            foreach (var field in fields)
            {
                foreach (var replacement in new JsonNode?[] { null, JsonValue.Create(" "), JsonValue.Create(-1), new JsonObject { ["token"] = SensitiveErrorMarker } })
                {
                    var json = JsonNode.Parse(valid)!.AsObject();
                    json[field] = replacement?.DeepClone();
                    bodies.Add(json.ToJsonString());
                }
            }
            foreach (var body in bodies)
            {
                using var handler = new FakeHttpMessageHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK, body)));
                using var http = new HttpClient(handler);
                var auth = new FirebaseAuthService(http);
                var ex = await Throws<InvalidOperationException>(() => refresh
                    ? auth.RefreshAsync("synthetic-refresh") : auth.SignInWithGoogleIdTokenAsync(PersistentGoogleToken));
                Check(ex is not FirebaseAuthException, "malformed success is not authoritative revocation");
                Check(!ex.ToString().Contains(SensitiveErrorMarker, StringComparison.Ordinal));
            }
        }
    }

    private static async Task PersistentRefreshPreCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        using var handler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("Cancelled refresh must not send HTTP."));
        using var http = new HttpClient(handler);
        await Throws<OperationCanceledException>(() => new FirebaseAuthService(http).RefreshAsync("synthetic-refresh", cts.Token));
        Equal(0, handler.CallCount);
    }

    private static async Task<HttpResponseMessage> PersistentExchange(HttpRequestMessage request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        await CheckPersistentExchangeRequest(request, PersistentGoogleToken);
        return PersistentSignInResponse();
    }

    private static async Task CheckPersistentExchangeRequest(HttpRequestMessage request, string googleToken)
    {
        Equal(HttpMethod.Post, request.Method);
        Equal(CloudConfig.FirebaseSignInWithIdpEndpoint, request.RequestUri!.GetLeftPart(UriPartial.Path));
        var json = JsonNode.Parse(await request.Content!.ReadAsStringAsync())!.AsObject();
        var form = ParseFormOrQuery(json["postBody"]!.GetValue<string>());
        Equal(2, form.Count);
        Equal(googleToken, form["id_token"]);
        Equal("google.com", form["providerId"]);
        Check(!form.ContainsKey("client_id") && !form.ContainsKey("client_secret"));
    }

    private static async Task CheckPersistentRefreshRequest(HttpRequestMessage request, string token)
    {
        Equal(HttpMethod.Post, request.Method);
        Equal(CloudConfig.FirebaseRefreshEndpoint, request.RequestUri!.GetLeftPart(UriPartial.Path));
        var form = ParseFormOrQuery(await request.Content!.ReadAsStringAsync());
        Equal(2, form.Count);
        Equal("refresh_token", form["grant_type"]);
        Equal(token, form["refresh_token"]);
    }

    private static string PersistentSignInBody(string uid = "server-uid", string refreshToken = PersistentRefreshToken) =>
        JsonSerializer.Serialize(new
        {
            idToken = PersistentFirebaseToken, refreshToken, localId = uid,
            email = "server@example.invalid", displayName = "Server Account", expiresIn = "3600",
        });

    private static HttpResponseMessage PersistentSignInResponse(string uid = "server-uid", string refreshToken = PersistentRefreshToken) =>
        JsonResponse(HttpStatusCode.OK, PersistentSignInBody(uid, refreshToken));

    private static HttpResponseMessage PersistentRefreshResponse(string uid, string refreshToken, string idToken = "synthetic-refreshed-id") =>
        JsonResponse(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            id_token = idToken, refresh_token = refreshToken, user_id = uid, expires_in = "3600",
        }));

    private static HttpResponseMessage PersistentError(HttpStatusCode status, string code) => JsonResponse(status,
        JsonSerializer.Serialize(new { error = new { message = code + " : " + SensitiveErrorMarker, refreshToken = SensitiveErrorMarker } }));

    private static HttpResponseMessage PersistentFirestoreSuccess(HttpRequestMessage request) => request.Method == HttpMethod.Get
        ? JsonResponse(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            fields = new { backup = new { stringValue = Encoding.UTF8.GetString(AppStorage.EncodeBackup(new UserSettings(), new ProgressState())) } },
        }))
        : JsonResponse(HttpStatusCode.OK, "{}");

    private static async Task<CloudResult> PersistentCloudOperation(CloudSyncCoordinator coordinator, string operation) => operation switch
    {
        "save" => await coordinator.SaveToCloudAsync(new UserSettings(), new ProgressState()),
        "load" => (await coordinator.LoadFromCloudAsync()).Result,
        "delete" => await coordinator.DeleteCloudProfileAsync(),
        _ => throw new InvalidOperationException("Unknown test operation."),
    };

    private sealed class PersistentSessionFixture : IDisposable
    {
        public TestFolder Folder { get; } = new();
        public CloudCredentialStore Store { get; }
        public CloudSyncCoordinator Coordinator { get; }
        public Func<CancellationToken, GoogleSignInBrowser, string, Task<GoogleBrowserSignInResult>> Browser { get; set; } =
            (_, _, _) => Task.FromResult(new GoogleBrowserSignInResult(PersistentGoogleToken, null));
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Firebase { get; set; } =
            (_, _) => throw new InvalidOperationException("Unexpected fake Firebase request.");
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Firestore { get; set; } =
            (_, _) => throw new InvalidOperationException("Unexpected fake Firestore request.");
        public int BrowserCalls { get; private set; }
        public int GoogleCalls => _googleHandler.CallCount;
        public int AuthCalls => _authHandler.CallCount;
        public int FirestoreCalls => _firestoreHandler.CallCount;
        private readonly FakeHttpMessageHandler _googleHandler;
        private readonly FakeHttpMessageHandler _authHandler;
        private readonly FakeHttpMessageHandler _firestoreHandler;
        private readonly HttpClient _googleHttp;
        private readonly HttpClient _authHttp;
        private readonly HttpClient _firestoreHttp;

        public PersistentSessionFixture()
        {
            Store = new CloudCredentialStore(Folder.Path);
            _googleHandler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("Legacy Google OAuth HTTP is forbidden."));
            _authHandler = new FakeHttpMessageHandler((request, token) => Firebase(request, token));
            _firestoreHandler = new FakeHttpMessageHandler((request, token) => Firestore(request, token));
            _googleHttp = new HttpClient(_googleHandler);
            _authHttp = new HttpClient(_authHandler);
            _firestoreHttp = new HttpClient(_firestoreHandler);
            Coordinator = CreateCoordinator();
        }

        public CloudSyncCoordinator CreateCoordinator() => new(new GoogleAuthService(_googleHttp), new FirebaseAuthService(_authHttp),
            new FirestoreSyncService(_firestoreHttp), new CloudCredentialStore(Folder.Path), (token, browser, language) =>
            {
                BrowserCalls++;
                return Browser(token, browser, language);
            });

        public async Task SeedAsync()
        {
            await SeedConnectedCredentialsAsync(Folder);
            await Coordinator.LoadCredentialsAsync();
        }

        public async Task<string?> RawAsync() => File.Exists(Folder.File("cloudcredentials.json"))
            ? await File.ReadAllTextAsync(Folder.File("cloudcredentials.json")) : null;

        public async Task CheckUnchangedAsync(string? before)
        {
            Equal(before, await RawAsync());
            var saved = await Store.LoadAsync();
            Equal(saved.HasFirebaseSession, Coordinator.IsConnected);
            Equal(saved.Email, Coordinator.Email);
            Equal(saved.DisplayName, Coordinator.DisplayName);
            Equal(saved.GoogleClientId, Coordinator.LastGoogleClientId);
            Equal(saved.GoogleClientSecret, Coordinator.LastGoogleClientSecret);
            Equal(saved.GoogleBrowser, Coordinator.LastGoogleBrowser);
            Equal(0, Directory.GetFiles(Folder.Path, "*.tmp").Length);
        }

        public void CheckNoLegacyOrFirestore()
        {
            Equal(0, GoogleCalls);
            Equal(0, FirestoreCalls);
            Check(GoogleLoopbackListener.Handler is null, "new tests must not configure the legacy global browser fake");
        }

        public void Dispose()
        {
            _googleHttp.Dispose();
            _authHttp.Dispose();
            _firestoreHttp.Dispose();
            Folder.Dispose();
        }
    }
}