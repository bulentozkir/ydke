using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YDKE_Windows;

internal static partial class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Opt-in, real-browser verification; never enter the mock/credential tests.
        if (args is ["--browser-page-probe"])
            return await BrowserPageProbe.RunAsync();

        var tests = new List<(string Name, Func<Task> Run)>
        {
            ("PKCE verifier/challenge have a valid RFC 7636 shape", PkceShape),
            ("PKCE verifier/challenge are unique across repeated calls", PkceUniqueness),
            ("PKCE challenge is exactly base64url(SHA256(verifier))", PkceChallengeMatchesVerifier),
            ("CreateState returns a unique URL-safe token each call", StateIsUniqueAndUrlSafe),
            ("Authorization URL contains every required query parameter, correctly URL-encoded", AuthorizationUrlContainsRequiredParams),
            ("Google code exchange sends the expected form fields and parses a successful response", GoogleExchangeSuccess),
            ("Google code exchange surfaces the real error/error_description on a non-2xx response", GoogleExchangeErrorSurfacing),
            ("Google refresh returns a null RefreshToken (Google never returns one on refresh)", GoogleRefreshReturnsNullRefreshToken),
            ("Firebase signInWithIdp success parses all fields and expiresIn string->int", FirebaseSignInSuccessAndExpiresInStringParsing),
            ("Firebase signInWithIdp surfaces the real error message on a non-2xx response", FirebaseSignInErrorSurfacing),
            ("Firebase refresh maps snake_case fields and nulls Email/DisplayName", FirebaseRefreshSnakeCaseMapping),
            ("Firestore save encodes updatedAtMs as a JSON STRING, not a bare number", FirestoreSaveUsesStringUpdatedAtMs),
            ("Firestore load returns null on 404 without throwing", FirestoreLoadNotFoundReturnsNull),
            ("Firestore load parses fields.backup.stringValue on 200", FirestoreLoadSuccessParsesBackup),
            ("Firestore load surfaces the real error message on other non-2xx responses", FirestoreLoadOtherErrorSurfacesReason),
            ("Firestore delete treats 404 as an already-deleted success", FirestoreDeleteNotFoundIsSuccess),
            ("Firestore delete succeeds on 200 using HTTP DELETE", FirestoreDeleteSuccess),
            ("Firestore delete surfaces the real error message on other non-2xx responses", FirestoreDeleteOtherErrorSurfacesReason),
            ("CloudCredentialStore round-trip encrypts the refresh token at rest", CloudCredentialStoreEncryptsRefreshTokenAtRest),
            ("CloudCredentialStore.LoadAsync returns empty for a missing file", CloudCredentialStoreLoadMissingFileReturnsEmpty),
            ("CloudCredentialStore.LoadAsync returns empty for a malformed JSON file", CloudCredentialStoreLoadCorruptedFileReturnsEmpty),
            ("CloudCredentialStore.LoadAsync returns empty when the protected token itself is corrupted", CloudCredentialStoreLoadCorruptedProtectedTokenReturnsEmpty),
            ("CloudCredentialStore.ClearAsync deletes an existing file and is idempotent when missing", CloudCredentialStoreClearDeletesFileAndIsIdempotent),
            ("CloudSyncCoordinator.SignInAsync full success path persists correct credentials and flips IsConnected true", SignInAsyncSuccessPersistsCredentialsAndConnects),
            ("CloudSyncCoordinator.SignInAsync when the window returns a null code produces a non-throwing 'cancelled' CloudResult", SignInAsyncCancelledReturnsNonThrowingResult),
            ("CloudSyncCoordinator.SaveToCloudAsync refuses when not connected, without any network call", SaveToCloudAsyncRefusesWhenNotConnected),
            ("CloudSyncCoordinator.LoadFromCloudAsync refuses when not connected, without any network call", LoadFromCloudAsyncRefusesWhenNotConnected),
            ("CloudSyncCoordinator.LoadFromCloudAsync's 'no backup yet' (404) case returns a clear non-crashing result", LoadFromCloudAsyncNoBackupYetReturnsClearResult),
            ("CloudSyncCoordinator.LoadFromCloudAsync surfaces AppStorage.DecodeBackup's InvalidDataException message for tampered cloud JSON", LoadFromCloudAsyncSurfacesDecodeBackupErrorForTamperedJson),
            ("CloudSyncCoordinator.SaveToCloudAsync refreshes the Firebase session first and persists a rotated refresh token", SaveToCloudAsyncSuccessRefreshesSessionAndPersistsRotatedToken),
            ("CloudSyncCoordinator.SignOutAsync clears credentials and never calls FirestoreSyncService", SignOutAsyncClearsCredentialsAndNeverCallsFirestore),
            ("CloudSyncCoordinator.SignOutAsync preserves GoogleClientId/GoogleClientSecret for one-click re-login", SignOutAsyncPreservesGoogleClientIdAndSecret),
        };
        AddGoogleSignInTests(tests);
        AddGoogleBrowserSignInTests(tests);
        AddPersistentBrowserSessionTests(tests);
        AddCloudUiTests(tests);

        var failures = 0;
        foreach (var (name, run) in tests)
        {
            try
            {
                GoogleLoopbackListener.Handler = null;
                // This is a deadlock guard, not a timing assertion.
                await run().WaitAsync(TimeSpan.FromSeconds(30));
                Console.WriteLine($"PASS {name}");
            }
            catch (TimeoutException ex)
            {
                Console.Error.WriteLine($"FAIL {name}: possible hang\n{ex}");
                return 2;
            }
            catch (Exception ex)
            {
                failures++;
                Console.Error.WriteLine($"FAIL {name}\n{ex}");
            }
        }
        Console.WriteLine($"RESULT: {tests.Count - failures}/{tests.Count} passed");
        return failures == 0 ? 0 : 1;
    }

    // ---- GoogleAuthService: PKCE / state / authorization URL ----

    private static Task PkceShape()
    {
        var (verifier, challenge) = GoogleAuthService.CreatePkce();
        Check(verifier.Length is >= 43 and <= 128, $"verifier length {verifier.Length} outside RFC 7636 43-128 range");
        Check(challenge.Length is >= 43 and <= 128, $"challenge length {challenge.Length} outside RFC 7636 43-128 range");
        Check(IsUrlSafeNoPadding(verifier), $"verifier is not URL-safe/unpadded base64: {verifier}");
        Check(IsUrlSafeNoPadding(challenge), $"challenge is not URL-safe/unpadded base64: {challenge}");
        return Task.CompletedTask;
    }

    private static Task PkceUniqueness()
    {
        var (verifier1, challenge1) = GoogleAuthService.CreatePkce();
        var (verifier2, challenge2) = GoogleAuthService.CreatePkce();
        Check(verifier1 != verifier2, "two CreatePkce() calls returned the same verifier");
        Check(challenge1 != challenge2, "two CreatePkce() calls returned the same challenge");
        return Task.CompletedTask;
    }

    private static Task PkceChallengeMatchesVerifier()
    {
        var (verifier, challenge) = GoogleAuthService.CreatePkce();
        var expected = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        Equal(expected, challenge);
        return Task.CompletedTask;
    }

    private static Task StateIsUniqueAndUrlSafe()
    {
        var state1 = GoogleAuthService.CreateState();
        var state2 = GoogleAuthService.CreateState();
        Check(state1 != state2, "two CreateState() calls returned the same value");
        Check(IsUrlSafeNoPadding(state1), $"state is not URL-safe/unpadded base64: {state1}");
        return Task.CompletedTask;
    }

    private static Task AuthorizationUrlContainsRequiredParams()
    {
        const string clientId = "client id/with special+chars";
        const string challenge = "chal lenge+/=";
        const string state = "st ate&x=y";
        const string redirectUri = "http://127.0.0.1:9999/";
        var url = GoogleAuthService.BuildAuthorizationUrl(clientId, challenge, state, redirectUri);
        Check(url.StartsWith(CloudConfig.GoogleAuthEndpoint + "?", StringComparison.Ordinal), $"wrong base endpoint: {url}");
        Check(!url.Contains(' '), $"url must not contain a literal space: {url}");

        var query = ParseFormOrQuery(url);
        Check(query.Count == 9, $"expected exactly 9 query parameters, got {query.Count}: {string.Join(',', query.Keys)}");
        Equal(clientId, query.GetValueOrDefault("client_id"));
        Equal(redirectUri, query.GetValueOrDefault("redirect_uri"));
        Equal("code", query.GetValueOrDefault("response_type"));
        Equal("openid email profile", query.GetValueOrDefault("scope"));
        Equal(challenge, query.GetValueOrDefault("code_challenge"));
        Equal("S256", query.GetValueOrDefault("code_challenge_method"));
        Equal(state, query.GetValueOrDefault("state"));
        Equal("offline", query.GetValueOrDefault("access_type"));
        Equal("select_account", query.GetValueOrDefault("prompt"));
        return Task.CompletedTask;
    }

    // ---- GoogleAuthService: token exchange / refresh ----

    private static async Task GoogleExchangeSuccess()
    {
        const string redirectUri = "http://127.0.0.1:9999/";
        using var handler = new FakeHttpMessageHandler(async request =>
        {
            Equal(HttpMethod.Post, request.Method);
            Equal(CloudConfig.GoogleTokenEndpoint, request.RequestUri!.ToString());
            var form = ParseFormOrQuery(await request.Content!.ReadAsStringAsync());
            Equal("cid", form.GetValueOrDefault("client_id"));
            Equal("csecret", form.GetValueOrDefault("client_secret"));
            Equal("authorization_code", form.GetValueOrDefault("grant_type"));
            Equal(redirectUri, form.GetValueOrDefault("redirect_uri"));
            Equal("thecode", form.GetValueOrDefault("code"));
            Equal("verifier123", form.GetValueOrDefault("code_verifier"));
            return JsonResponse(HttpStatusCode.OK, """{"id_token":"idt","access_token":"act","refresh_token":"rt","expires_in":3600}""");
        });
        var service = new GoogleAuthService(new HttpClient(handler));
        var result = await service.ExchangeCodeAsync("cid", "csecret", "thecode", "verifier123", redirectUri);
        Equal("idt", result.IdToken);
        Equal("act", result.AccessToken);
        Equal("rt", result.RefreshToken);
        Equal(3600, result.ExpiresIn);
    }

    private static async Task GoogleExchangeErrorSurfacing()
    {
        using var handler = new FakeHttpMessageHandler(_ => Task.FromResult(
            JsonResponse(HttpStatusCode.BadRequest, """{"error":"invalid_grant","error_description":"Bad Request: code expired"}""")));
        var service = new GoogleAuthService(new HttpClient(handler));
        var ex = await Throws<InvalidOperationException>(() => service.ExchangeCodeAsync("cid", null, "thecode", "verifier123", "http://127.0.0.1:9999/"));
        Check(ex.Message.Contains("invalid_grant", StringComparison.Ordinal), $"message missing error code: {ex.Message}");
        Check(ex.Message.Contains("code expired", StringComparison.Ordinal), $"message missing description: {ex.Message}");
        Check(ex.Message.Contains("400", StringComparison.Ordinal), $"message missing HTTP status: {ex.Message}");
    }

    private static async Task GoogleRefreshReturnsNullRefreshToken()
    {
        using var handler = new FakeHttpMessageHandler(async request =>
        {
            var form = ParseFormOrQuery(await request.Content!.ReadAsStringAsync());
            Equal("refresh_token", form.GetValueOrDefault("grant_type"));
            Equal("rt123", form.GetValueOrDefault("refresh_token"));
            // Google's real refresh response never includes a refresh_token field at all.
            return JsonResponse(HttpStatusCode.OK, """{"id_token":"idt2","access_token":"act2","expires_in":1800}""");
        });
        var service = new GoogleAuthService(new HttpClient(handler));
        var result = await service.RefreshAsync("cid", null, "rt123");
        Equal("idt2", result.IdToken);
        Equal("act2", result.AccessToken);
        Check(result.RefreshToken is null, $"RefreshToken should be null on a refresh response, was '{result.RefreshToken}'");
        Equal(1800, result.ExpiresIn);
    }

    // ---- FirebaseAuthService ----

    private static async Task FirebaseSignInSuccessAndExpiresInStringParsing()
    {
        using var handler = new FakeHttpMessageHandler(async request =>
        {
            Equal(HttpMethod.Post, request.Method);
            Check(request.RequestUri!.ToString().StartsWith(CloudConfig.FirebaseSignInWithIdpEndpoint, StringComparison.Ordinal), "wrong endpoint");
            Check(request.RequestUri!.ToString().Contains("key=" + CloudConfig.FirebaseApiKey, StringComparison.Ordinal), "missing api key");
            var requestJson = JsonNode.Parse(await request.Content!.ReadAsStringAsync())!.AsObject();
            var postBody = requestJson["postBody"]!.GetValue<string>();
            Check(postBody.Contains("id_token=google-id-token-xyz", StringComparison.Ordinal), $"postBody missing id token: {postBody}");
            Check(postBody.Contains("providerId=google.com", StringComparison.Ordinal), $"postBody missing providerId: {postBody}");
            Equal(CloudConfig.FirebaseRequestUriPlaceholder, requestJson["requestUri"]!.GetValue<string>());
            Check(requestJson["returnIdpCredential"]!.GetValue<bool>(), "returnIdpCredential should be true");
            Check(requestJson["returnSecureToken"]!.GetValue<bool>(), "returnSecureToken should be true");
            return JsonResponse(HttpStatusCode.OK,
                """{"idToken":"fid","refreshToken":"frt","localId":"uid1","email":"a@b.com","displayName":"A B","expiresIn":"3600"}""");
        });
        var service = new FirebaseAuthService(new HttpClient(handler));
        var session = await service.SignInWithGoogleIdTokenAsync("google-id-token-xyz");
        Equal("fid", session.IdToken);
        Equal("frt", session.RefreshToken);
        Equal("uid1", session.LocalId);
        Equal("a@b.com", session.Email);
        Equal("A B", session.DisplayName);
        Equal(3600, session.ExpiresIn); // parsed from a JSON string, not a bare number
    }

    private static async Task FirebaseSignInErrorSurfacing()
    {
        using var handler = new FakeHttpMessageHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.BadRequest,
            """{"error":{"code":400,"message":"INVALID_ID_TOKEN","errors":[{"message":"INVALID_ID_TOKEN","domain":"global","reason":"invalid"}]}}""")));
        var service = new FirebaseAuthService(new HttpClient(handler));
        var ex = await Throws<InvalidOperationException>(() => service.SignInWithGoogleIdTokenAsync("bad-token"));
        Check(ex.Message.Contains("INVALID_ID_TOKEN", StringComparison.Ordinal), $"message missing reason: {ex.Message}");
        Check(ex.Message.Contains("400", StringComparison.Ordinal), $"message missing HTTP status: {ex.Message}");
    }

    private static async Task FirebaseRefreshSnakeCaseMapping()
    {
        using var handler = new FakeHttpMessageHandler(async request =>
        {
            var form = ParseFormOrQuery(await request.Content!.ReadAsStringAsync());
            Equal("refresh_token", form.GetValueOrDefault("grant_type"));
            Equal("firebase-rt", form.GetValueOrDefault("refresh_token"));
            return JsonResponse(HttpStatusCode.OK, """{"id_token":"newid","refresh_token":"newrt","user_id":"uid2","expires_in":"3600"}""");
        });
        var service = new FirebaseAuthService(new HttpClient(handler));
        var session = await service.RefreshAsync("firebase-rt");
        Equal("newid", session.IdToken);
        Equal("newrt", session.RefreshToken);
        Equal("uid2", session.LocalId); // mapped from user_id
        Check(session.Email is null, $"Email should be null on refresh, was '{session.Email}'");
        Check(session.DisplayName is null, $"DisplayName should be null on refresh, was '{session.DisplayName}'");
        Equal(3600, session.ExpiresIn);
    }

    // ---- FirestoreSyncService ----

    private static async Task FirestoreSaveUsesStringUpdatedAtMs()
    {
        string? capturedBody = null;
        HttpMethod? capturedMethod = null;
        Uri? capturedUri = null;
        string? capturedAuth = null;
        using var handler = new FakeHttpMessageHandler(async request =>
        {
            capturedMethod = request.Method;
            capturedUri = request.RequestUri;
            capturedAuth = request.Headers.Authorization?.ToString();
            capturedBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse(HttpStatusCode.OK, "{}");
        });
        var service = new FirestoreSyncService(new HttpClient(handler));
        await service.SaveBackupAsync("uid-42", "idtok", "the-backup-json-blob");

        Equal(HttpMethod.Patch, capturedMethod);
        Equal(ExpectedDocumentUrl("uid-42"), capturedUri!.ToString());
        Equal("Bearer idtok", capturedAuth);
        Check(capturedBody is not null, "missing request body");
        var json = JsonNode.Parse(capturedBody!)!.AsObject();
        var fields = json["fields"]!.AsObject();
        Equal("the-backup-json-blob", fields["backup"]!["stringValue"]!.GetValue<string>());
        var updatedAtNode = fields["updatedAtMs"]!["integerValue"]!;
        Check(updatedAtNode.GetValueKind() == JsonValueKind.String,
            $"integerValue must be encoded as a JSON string, was {updatedAtNode.GetValueKind()}");
        Check(long.TryParse(updatedAtNode.GetValue<string>(), out _), "integerValue string must parse as a long");
        // Belt-and-braces: confirm the RAW wire text has a quoted number, not a bare one.
        Check(Regex.IsMatch(capturedBody!, "\"integerValue\"\\s*:\\s*\"\\d+\""), $"raw body must contain a quoted integerValue: {capturedBody}");
    }

    private static async Task FirestoreLoadNotFoundReturnsNull()
    {
        using var handler = new FakeHttpMessageHandler(_ => Task.FromResult(
            JsonResponse(HttpStatusCode.NotFound, """{"error":{"code":404,"message":"NOT_FOUND","status":"NOT_FOUND"}}""")));
        var service = new FirestoreSyncService(new HttpClient(handler));
        var result = await service.LoadBackupAsync("uid-1", "idtok");
        Check(result is null, $"404 must return null, got '{result}'");
    }

    private static async Task FirestoreLoadSuccessParsesBackup()
    {
        using var handler = new FakeHttpMessageHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK,
            """{"name":"projects/p/databases/(default)/documents/ydke_users/uid-1","fields":{"backup":{"stringValue":"backup-content-here"},"updatedAtMs":{"integerValue":"1700000000000"}},"createTime":"x","updateTime":"y"}""")));
        var service = new FirestoreSyncService(new HttpClient(handler));
        var result = await service.LoadBackupAsync("uid-1", "idtok");
        Equal("backup-content-here", result);
    }

    private static async Task FirestoreLoadOtherErrorSurfacesReason()
    {
        using var handler = new FakeHttpMessageHandler(_ => Task.FromResult(
            JsonResponse(HttpStatusCode.Forbidden, """{"error":{"code":403,"message":"PERMISSION_DENIED","status":"PERMISSION_DENIED"}}""")));
        var service = new FirestoreSyncService(new HttpClient(handler));
        var ex = await Throws<InvalidOperationException>(() => service.LoadBackupAsync("uid-1", "idtok"));
        Check(ex.Message.Contains("PERMISSION_DENIED", StringComparison.Ordinal), $"message missing reason: {ex.Message}");
        Check(ex.Message.Contains("403", StringComparison.Ordinal), $"message missing HTTP status: {ex.Message}");
    }

    private static async Task FirestoreDeleteNotFoundIsSuccess()
    {
        using var handler = new FakeHttpMessageHandler(_ => Task.FromResult(
            JsonResponse(HttpStatusCode.NotFound, """{"error":{"code":404,"message":"NOT_FOUND"}}""")));
        var service = new FirestoreSyncService(new HttpClient(handler));
        await service.DeleteBackupAsync("uid-1", "idtok"); // must not throw
    }

    private static async Task FirestoreDeleteSuccess()
    {
        HttpMethod? capturedMethod = null;
        using var handler = new FakeHttpMessageHandler(request =>
        {
            capturedMethod = request.Method;
            return Task.FromResult(JsonResponse(HttpStatusCode.OK, "{}"));
        });
        var service = new FirestoreSyncService(new HttpClient(handler));
        await service.DeleteBackupAsync("uid-1", "idtok");
        Equal(HttpMethod.Delete, capturedMethod);
    }

    private static async Task FirestoreDeleteOtherErrorSurfacesReason()
    {
        using var handler = new FakeHttpMessageHandler(_ => Task.FromResult(
            JsonResponse(HttpStatusCode.InternalServerError, """{"error":{"code":500,"message":"INTERNAL"}}""")));
        var service = new FirestoreSyncService(new HttpClient(handler));
        var ex = await Throws<InvalidOperationException>(() => service.DeleteBackupAsync("uid-1", "idtok"));
        Check(ex.Message.Contains("INTERNAL", StringComparison.Ordinal), $"message missing reason: {ex.Message}");
    }

    // ---- CloudCredentialStore ----

    private static async Task CloudCredentialStoreEncryptsRefreshTokenAtRest()
    {
        using var folder = new TestFolder();
        const string secretToken = "super-secret-refresh-token-value-12345";
        const string firebaseToken = "synthetic-firebase-refresh-token-value-67890";
        var store = new CloudCredentialStore(folder.Path);
        await store.SaveAsync(new CloudCredentials
        {
            GoogleClientId = "cid",
            GoogleClientSecret = "csecret",
            GoogleBrowser = GoogleSignInBrowser.Chrome,
            GoogleRefreshToken = secretToken,
            FirebaseRefreshToken = firebaseToken,
            FirebaseUid = "uid-9",
            Email = "person@example.com",
            DisplayName = "Person Name",
        });

        var rawText = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(folder.File("cloudcredentials.json")));
        Check(!rawText.Contains(secretToken, StringComparison.Ordinal), "plaintext refresh token must NOT appear in the saved file");
        Check(!rawText.Contains(firebaseToken, StringComparison.Ordinal), "plaintext Firebase refresh token must NOT appear in the saved file");
        Check(rawText.Contains("csecret", StringComparison.Ordinal), "client secret should be stored in plain JSON");
        Check(rawText.Contains("person@example.com", StringComparison.Ordinal), "email should be stored in plain JSON");

        var loaded = await store.LoadAsync(); // fresh load from disk; decrypts the token back
        Equal(secretToken, loaded.GoogleRefreshToken);
        Equal(firebaseToken, loaded.FirebaseRefreshToken);
        Equal(GoogleSignInBrowser.Chrome, loaded.GoogleBrowser);
        Equal("cid", loaded.GoogleClientId);
        Equal("csecret", loaded.GoogleClientSecret);
        Equal("uid-9", loaded.FirebaseUid);
        Equal("person@example.com", loaded.Email);
        Equal("Person Name", loaded.DisplayName);
    }

    private static async Task CloudCredentialStoreLoadMissingFileReturnsEmpty()
    {
        using var folder = new TestFolder();
        var store = new CloudCredentialStore(Path.Combine(folder.Path, "does-not-exist"));
        var result = await store.LoadAsync();
        Check(result.GoogleRefreshToken is null, "GoogleRefreshToken should be null");
        Check(result.GoogleClientId is null, "GoogleClientId should be null");
        Check(result.FirebaseUid is null, "FirebaseUid should be null");
    }

    private static async Task CloudCredentialStoreLoadCorruptedFileReturnsEmpty()
    {
        using var folder = new TestFolder();
        await File.WriteAllTextAsync(folder.File("cloudcredentials.json"), "{ this is not valid json !!! ");
        var store = new CloudCredentialStore(folder.Path);
        var result = await store.LoadAsync();
        Check(result.GoogleRefreshToken is null, "GoogleRefreshToken should be null");
        Check(result.GoogleClientId is null, "GoogleClientId should be null");
    }

    private static async Task CloudCredentialStoreLoadCorruptedProtectedTokenReturnsEmpty()
    {
        using var folder = new TestFolder();
        const string badJson = """{"GoogleClientId":"cid","ProtectedGoogleRefreshToken":"not-valid-base64!!","FirebaseUid":"u"}""";
        await File.WriteAllTextAsync(folder.File("cloudcredentials.json"), badJson);
        var store = new CloudCredentialStore(folder.Path);
        var result = await store.LoadAsync();
        Check(result.GoogleRefreshToken is null, "GoogleRefreshToken should be null");
        Check(result.GoogleClientId is null, "the whole load should fall back to empty, not just the token field");
    }

    private static async Task CloudCredentialStoreClearDeletesFileAndIsIdempotent()
    {
        using var folder = new TestFolder();
        var store = new CloudCredentialStore(folder.Path);
        await store.SaveAsync(new CloudCredentials { GoogleClientId = "cid" });
        Check(File.Exists(folder.File("cloudcredentials.json")), "file should exist after save");

        await store.ClearAsync();
        Check(!File.Exists(folder.File("cloudcredentials.json")), "file should be deleted after clear");

        await store.ClearAsync(); // must not throw when the file is already missing
        var reloaded = await store.LoadAsync();
        Check(reloaded.GoogleClientId is null, "reloading after clear should be empty");
    }

    // ---- CloudSyncCoordinator ----
    // GoogleLoopbackListener (the real system-browser + HTTP listener flow) cannot run in this
    // console harness -- see GoogleLoopbackListener.cs in this project for the test-only stand-in.
    // Only CloudSyncCoordinator's own orchestration logic is exercised here.

    private static async Task SeedConnectedCredentialsAsync(TestFolder folder)
    {
        var store = new CloudCredentialStore(folder.Path);
        await store.SaveAsync(new CloudCredentials
        {
            GoogleClientId = SyntheticClientId,
            GoogleClientSecret = "client-secret",
            GoogleBrowser = GoogleSignInBrowser.Edge,
            GoogleRefreshToken = "google-rt-seed",
            FirebaseRefreshToken = "fb-rt-seed",
            FirebaseUid = "uid-77",
            Email = "person@example.com",
            DisplayName = "Person Name",
        });
    }

    private static async Task SignInAsyncSuccessPersistsCredentialsAndConnects()
    {
        using var folder = new TestFolder();
        GoogleLoopbackListener.Handler = (clientId, challenge, state, _, browser) =>
        {
            Equal(SyntheticClientId, clientId);
            Equal(GoogleSignInBrowser.Default, browser);
            Check(!string.IsNullOrEmpty(challenge), "challenge should be non-empty");
            Check(!string.IsNullOrEmpty(state), "state should be non-empty");
            return Task.FromResult(new GoogleAuthorizationResult("auth-code-xyz", "http://127.0.0.1:9999/", null));
        };
        using var googleHandler = new FakeHttpMessageHandler(async request =>
        {
            Equal(CloudConfig.GoogleTokenEndpoint, request.RequestUri!.ToString());
            var form = ParseFormOrQuery(await request.Content!.ReadAsStringAsync());
            Equal("auth-code-xyz", form.GetValueOrDefault("code"));
            Equal(SyntheticClientId, form.GetValueOrDefault("client_id"));
            Equal("client-secret", form.GetValueOrDefault("client_secret"));
            Equal("http://127.0.0.1:9999/", form.GetValueOrDefault("redirect_uri"));
            return JsonResponse(HttpStatusCode.OK,
                """{"id_token":"google-idtok","access_token":"gact","refresh_token":"google-rt","expires_in":3600}""");
        });
        using var firebaseHandler = new FakeHttpMessageHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK,
            """{"idToken":"fb-idtok","refreshToken":"fb-rt","localId":"uid-77","email":"person@example.com","displayName":"Person Name","expiresIn":"3600"}""")));
        using var deadFirestoreHandler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("Firestore must not be called from SignInAsync."));

        var coordinator = new CloudSyncCoordinator(
            new GoogleAuthService(new HttpClient(googleHandler)),
            new FirebaseAuthService(new HttpClient(firebaseHandler)),
            new FirestoreSyncService(new HttpClient(deadFirestoreHandler)),
            new CloudCredentialStore(folder.Path));

        var result = await coordinator.SignInAsync($" \t{SyntheticClientId}\r\n", "client-secret");
        Check(result.Success, $"expected success, got error: {result.ErrorMessage}");
        Check(result.ErrorMessage is null, "ErrorMessage should be null on success");
        Check(coordinator.IsConnected, "IsConnected should be true after sign-in");
        Equal("person@example.com", coordinator.Email);
        Equal("Person Name", coordinator.DisplayName);

        // Reload from a FRESH store pointed at the same folder to prove it was actually persisted.
        var reloaded = await new CloudCredentialStore(folder.Path).LoadAsync();
        Equal("uid-77", reloaded.FirebaseUid);
        Equal("fb-rt", reloaded.FirebaseRefreshToken);
        Equal("google-rt", reloaded.GoogleRefreshToken);
        Equal(SyntheticClientId, reloaded.GoogleClientId);
        Equal("client-secret", reloaded.GoogleClientSecret);
        Equal(GoogleSignInBrowser.Default, reloaded.GoogleBrowser);
        Equal(GoogleSignInBrowser.Default, coordinator.LastGoogleBrowser);
        Equal("person@example.com", reloaded.Email);
        Equal("Person Name", reloaded.DisplayName);
    }

    private static async Task SignInAsyncCancelledReturnsNonThrowingResult()
    {
        using var folder = new TestFolder();
        GoogleLoopbackListener.Handler = (_, _, _, _, _) => Task.FromResult(new GoogleAuthorizationResult(null, "http://127.0.0.1:9999/", null));
        using var deadHandler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("No HTTP call should happen when sign-in is cancelled."));
        var coordinator = new CloudSyncCoordinator(
            new GoogleAuthService(new HttpClient(deadHandler)),
            new FirebaseAuthService(new HttpClient(deadHandler)),
            new FirestoreSyncService(new HttpClient(deadHandler)),
            new CloudCredentialStore(folder.Path));

        var result = await coordinator.SignInAsync(SyntheticClientId, null);
        Check(!result.Success, "a cancelled sign-in must report Success = false");
        Equal("Sign-in was cancelled.", result.ErrorMessage);
        Check(!coordinator.IsConnected, "IsConnected must remain false after a cancelled sign-in");
        Equal(0, deadHandler.CallCount);
    }

    private static async Task SaveToCloudAsyncRefusesWhenNotConnected()
    {
        using var folder = new TestFolder();
        using var deadHandler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("No HTTP call should happen when not connected."));
        var coordinator = new CloudSyncCoordinator(
            new GoogleAuthService(new HttpClient(deadHandler)),
            new FirebaseAuthService(new HttpClient(deadHandler)),
            new FirestoreSyncService(new HttpClient(deadHandler)),
            new CloudCredentialStore(folder.Path));
        await coordinator.LoadCredentialsAsync(); // empty folder -> stays not connected

        var result = await coordinator.SaveToCloudAsync(new UserSettings(), new ProgressState());
        Check(!result.Success, "must refuse to save when not connected");
        Check(!string.IsNullOrWhiteSpace(result.ErrorMessage), "must have a clear not-connected message");
    }

    private static async Task LoadFromCloudAsyncRefusesWhenNotConnected()
    {
        using var folder = new TestFolder();
        using var deadHandler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("No HTTP call should happen when not connected."));
        var coordinator = new CloudSyncCoordinator(
            new GoogleAuthService(new HttpClient(deadHandler)),
            new FirebaseAuthService(new HttpClient(deadHandler)),
            new FirestoreSyncService(new HttpClient(deadHandler)),
            new CloudCredentialStore(folder.Path));

        var (result, settings, progress) = await coordinator.LoadFromCloudAsync();
        Check(!result.Success, "must refuse to load when not connected");
        Check(settings is null, "Settings must be null when not connected");
        Check(progress is null, "Progress must be null when not connected");
    }

    private static async Task LoadFromCloudAsyncNoBackupYetReturnsClearResult()
    {
        using var folder = new TestFolder();
        await SeedConnectedCredentialsAsync(folder);

        using var firebaseHandler = new FakeHttpMessageHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK,
            """{"id_token":"fb-idtok2","refresh_token":"fb-rt2","user_id":"uid-77","expires_in":"3600"}""")));
        using var firestoreHandler = new FakeHttpMessageHandler(_ => Task.FromResult(
            JsonResponse(HttpStatusCode.NotFound, """{"error":{"code":404,"message":"NOT_FOUND"}}""")));
        using var deadGoogleHandler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("Google must not be called from Save/Load/Delete."));

        var coordinator = new CloudSyncCoordinator(
            new GoogleAuthService(new HttpClient(deadGoogleHandler)),
            new FirebaseAuthService(new HttpClient(firebaseHandler)),
            new FirestoreSyncService(new HttpClient(firestoreHandler)),
            new CloudCredentialStore(folder.Path));
        await coordinator.LoadCredentialsAsync();

        var (result, settings, progress) = await coordinator.LoadFromCloudAsync();
        Check(!result.Success, "no backup yet must not be reported as Success");
        Check(!string.IsNullOrWhiteSpace(result.ErrorMessage), "must have a clear, non-crashing message");
        Check(settings is null, "Settings must be null when there is no backup yet");
        Check(progress is null, "Progress must be null when there is no backup yet");
    }

    private static async Task LoadFromCloudAsyncSurfacesDecodeBackupErrorForTamperedJson()
    {
        using var folder = new TestFolder();
        await SeedConnectedCredentialsAsync(folder);

        using var firebaseHandler = new FakeHttpMessageHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK,
            """{"id_token":"fb-idtok3","refresh_token":"fb-rt3","user_id":"uid-77","expires_in":"3600"}""")));
        using var firestoreHandler = new FakeHttpMessageHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK,
            """{"fields":{"backup":{"stringValue":"not valid backup bytes at all"}}}""")));
        using var deadGoogleHandler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("Google must not be called from Save/Load/Delete."));

        var coordinator = new CloudSyncCoordinator(
            new GoogleAuthService(new HttpClient(deadGoogleHandler)),
            new FirebaseAuthService(new HttpClient(firebaseHandler)),
            new FirestoreSyncService(new HttpClient(firestoreHandler)),
            new CloudCredentialStore(folder.Path));
        await coordinator.LoadCredentialsAsync();

        var (result, settings, progress) = await coordinator.LoadFromCloudAsync();
        Check(!result.Success, "a tampered backup must not be reported as Success");
        Check(!string.IsNullOrWhiteSpace(result.ErrorMessage), "must surface a real error message, not swallow it");
        Check(result.ErrorMessage!.Contains("JSON", StringComparison.OrdinalIgnoreCase), $"expected AppStorage.DecodeBackup's JSON error to flow through verbatim, got: {result.ErrorMessage}");
        Check(settings is null, "Settings must be null for a tampered backup");
        Check(progress is null, "Progress must be null for a tampered backup");
    }

    private static async Task SaveToCloudAsyncSuccessRefreshesSessionAndPersistsRotatedToken()
    {
        using var folder = new TestFolder();
        await SeedConnectedCredentialsAsync(folder);

        string? capturedIdToken = null;
        string? capturedBackupJson = null;
        using var firebaseHandler = new FakeHttpMessageHandler(_ => Task.FromResult(JsonResponse(HttpStatusCode.OK,
            """{"id_token":"fb-idtok-fresh","refresh_token":"fb-rt-rotated","user_id":"uid-77","expires_in":"3600"}""")));
        using var firestoreHandler = new FakeHttpMessageHandler(async request =>
        {
            capturedIdToken = request.Headers.Authorization?.Parameter;
            var json = JsonNode.Parse(await request.Content!.ReadAsStringAsync())!.AsObject();
            capturedBackupJson = json["fields"]!["backup"]!["stringValue"]!.GetValue<string>();
            return JsonResponse(HttpStatusCode.OK, "{}");
        });
        using var deadGoogleHandler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("Google must not be called from Save/Load/Delete."));

        var coordinator = new CloudSyncCoordinator(
            new GoogleAuthService(new HttpClient(deadGoogleHandler)),
            new FirebaseAuthService(new HttpClient(firebaseHandler)),
            new FirestoreSyncService(new HttpClient(firestoreHandler)),
            new CloudCredentialStore(folder.Path));
        await coordinator.LoadCredentialsAsync();

        var result = await coordinator.SaveToCloudAsync(new UserSettings(), new ProgressState());
        Check(result.Success, $"expected success, got: {result.ErrorMessage}");
        Equal("fb-idtok-fresh", capturedIdToken);
        Check(!string.IsNullOrEmpty(capturedBackupJson), "backup JSON should have been sent to Firestore");

        // The rotated Firebase refresh token/uid returned by RefreshAsync must be persisted.
        var reloaded = await new CloudCredentialStore(folder.Path).LoadAsync();
        Equal("fb-rt-rotated", reloaded.FirebaseRefreshToken);
        Equal("uid-77", reloaded.FirebaseUid);
    }

    private static async Task SignOutAsyncClearsCredentialsAndNeverCallsFirestore()
    {
        using var folder = new TestFolder();
        await SeedConnectedCredentialsAsync(folder);

        using var deadHandler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("SignOutAsync must never call any cloud HTTP endpoint."));
        var coordinator = new CloudSyncCoordinator(
            new GoogleAuthService(new HttpClient(deadHandler)),
            new FirebaseAuthService(new HttpClient(deadHandler)),
            new FirestoreSyncService(new HttpClient(deadHandler)),
            new CloudCredentialStore(folder.Path));
        await coordinator.LoadCredentialsAsync();
        Check(coordinator.IsConnected, "precondition failed: should start connected");

        await coordinator.SignOutAsync(); // must not throw -- proves the always-throwing fake handler was never invoked
        Check(!coordinator.IsConnected, "IsConnected must be false after sign-out");

        var reloaded = await new CloudCredentialStore(folder.Path).LoadAsync();
        Check(reloaded.FirebaseUid is null, "credentials file must be cleared after sign-out");
    }

    private static async Task SignOutAsyncPreservesGoogleClientIdAndSecret()
    {
        using var folder = new TestFolder();
        await SeedConnectedCredentialsAsync(folder);

        using var deadHandler = new FakeHttpMessageHandler(_ => throw new InvalidOperationException("SignOutAsync must never call any cloud HTTP endpoint."));
        var coordinator = new CloudSyncCoordinator(
            new GoogleAuthService(new HttpClient(deadHandler)),
            new FirebaseAuthService(new HttpClient(deadHandler)),
            new FirestoreSyncService(new HttpClient(deadHandler)),
            new CloudCredentialStore(folder.Path));
        await coordinator.LoadCredentialsAsync();

        await coordinator.SignOutAsync();
        Check(!coordinator.IsConnected, "IsConnected must be false after sign-out");
        Equal(SyntheticClientId, coordinator.LastGoogleClientId);
        Equal("client-secret", coordinator.LastGoogleClientSecret);
        Equal(GoogleSignInBrowser.Edge, coordinator.LastGoogleBrowser);
        Equal(0, deadHandler.CallCount);
        Check(coordinator.Email is null, "Email must be cleared after sign-out");
        Check(coordinator.DisplayName is null, "DisplayName must be cleared after sign-out");

        // Reload from a FRESH store to prove the client id/secret survive a full app restart, not just the in-memory object.
        var reloaded = await new CloudCredentialStore(folder.Path).LoadAsync();
        Equal(SyntheticClientId, reloaded.GoogleClientId);
        Equal("client-secret", reloaded.GoogleClientSecret);
        Equal(GoogleSignInBrowser.Edge, reloaded.GoogleBrowser);
        Check(reloaded.FirebaseUid is null, "FirebaseUid must be cleared after sign-out");
        Check(reloaded.GoogleRefreshToken is null, "GoogleRefreshToken must be cleared after sign-out");
        Check(reloaded.FirebaseRefreshToken is null, "FirebaseRefreshToken must be cleared after sign-out");
        Check(reloaded.Email is null, "persisted Email must be cleared after sign-out");
        Check(reloaded.DisplayName is null, "persisted DisplayName must be cleared after sign-out");
    }

    // ---- Shared helpers ----

    private static void Check(bool condition, string message = "Assertion failed")
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Equal<T>(T expected, T actual) =>
        Check(EqualityComparer<T>.Default.Equals(expected, actual), $"Expected {expected}; got {actual}");

    private static async Task<T> Throws<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T ex) { return ex; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}");
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static string ExpectedDocumentUrl(string uid) =>
        string.Format(CloudConfig.FirestoreBaseUrl, CloudConfig.FirebaseProjectId, CloudConfig.FirestoreCollection, uid);

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool IsUrlSafeNoPadding(string value) =>
        value.Length > 0 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    // Form spaces use '+', while an encoded '%2B' must remain a literal plus.
    private static Dictionary<string, string> ParseFormOrQuery(string encoded)
    {
        var start = encoded.IndexOf('?');
        var text = start >= 0 ? encoded[(start + 1)..] : encoded;
        var result = new Dictionary<string, string>();
        foreach (var pair in text.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            result[Uri.UnescapeDataString(parts[0].Replace('+', ' '))] = parts.Length > 1
                ? Uri.UnescapeDataString(parts[1].Replace('+', ' ')) : "";
        }
        return result;
    }
}

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : this((request, _) => handler(request)) { }

    public FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler) => _handler = handler;

    public int CallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        return _handler(request, cancellationToken);
    }
}

internal sealed class TestFolder : IDisposable
{
    // Never use the OS/user-profile temp directory or the default CloudCredentialStore folder.
    public string Path { get; } = System.IO.Path.Combine(AppContext.BaseDirectory, "scratch", Guid.NewGuid().ToString("N"));
    public TestFolder() => Directory.CreateDirectory(Path);
    public string File(string name) => System.IO.Path.Combine(Path, name);
    public void Dispose()
    {
        if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
    }
}
