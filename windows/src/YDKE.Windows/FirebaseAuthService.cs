using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace YDKE_Windows;

internal sealed record FirebaseSession(string IdToken, string RefreshToken, string LocalId, string? Email, string? DisplayName, int ExpiresIn);

internal sealed class FirebaseAuthException : InvalidOperationException
{
    internal const string ReauthenticationMessage = "Your saved session is no longer valid. Sign in with Google again.";

    public string? ErrorCode { get; }
    public HttpStatusCode StatusCode { get; }

    public bool IsSessionRevoked => (StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized
        or HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        && (ErrorCode is "TOKEN_EXPIRED" or "INVALID_REFRESH_TOKEN" or "USER_DISABLED" or "USER_NOT_FOUND");

    public FirebaseAuthException(string action, HttpStatusCode statusCode, string? errorCode)
        : base(Describe(action, statusCode, errorCode))
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }

    private static string Describe(string action, HttpStatusCode status, string? code)
    {
        var guidance = status == HttpStatusCode.TooManyRequests
            ? "Too many requests. Please try again shortly."
            : (int)status >= 500
                ? "The sign-in service is temporarily unavailable. Please try again later."
                : code is "TOKEN_EXPIRED" or "INVALID_REFRESH_TOKEN" or "USER_DISABLED" or "USER_NOT_FOUND"
                    ? ReauthenticationMessage
                    : "Google sign-in could not be verified. Please try again.";
        return $"{action} failed (HTTP {(int)status}{(code is null ? "" : ", " + code)}). {guidance}";
    }
}

internal sealed class FirebaseAuthService
{
    private readonly HttpClient _httpClient;

    public FirebaseAuthService(HttpClient? httpClient = null) => _httpClient = httpClient ?? new HttpClient();

    public async Task<FirebaseSession> SignInWithGoogleIdTokenAsync(string googleIdToken, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(googleIdToken);
        var requestBody = new JsonObject
        {
            ["postBody"] = JsonValue.Create($"id_token={Uri.EscapeDataString(googleIdToken)}&providerId=google.com"),
            ["requestUri"] = JsonValue.Create(CloudConfig.FirebaseRequestUriPlaceholder),
            ["returnIdpCredential"] = JsonValue.Create(true),
            ["returnSecureToken"] = JsonValue.Create(true),
        };
        var url = $"{CloudConfig.FirebaseSignInWithIdpEndpoint}?key={Uri.EscapeDataString(CloudConfig.FirebaseApiKey)}";
        using var content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
        // Read the body before checking the status: EnsureSuccessStatusCode would discard the real reason.
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!response.IsSuccessStatusCode) throw CreateAuthError("Firebase sign-in", response.StatusCode, body);
        var json = ParseObject(body, "Firebase sign-in");
        return new FirebaseSession(
            IdToken: RequireString(json, "idToken"),
            RefreshToken: RequireString(json, "refreshToken"),
            LocalId: RequireString(json, "localId"),
            Email: OptionalString(json, "email"),
            DisplayName: OptionalString(json, "displayName"),
            // expiresIn arrives as a JSON string on this endpoint, not a number.
            ExpiresIn: RequireInt(json, "expiresIn"));
    }

    // Firebase's refresh endpoint returns no email/displayName; callers keep their previously known values.
    public async Task<FirebaseSession> RefreshAsync(string firebaseRefreshToken, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(firebaseRefreshToken);
        var fields = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = firebaseRefreshToken,
        };
        var url = $"{CloudConfig.FirebaseRefreshEndpoint}?key={Uri.EscapeDataString(CloudConfig.FirebaseApiKey)}";
        using var content = new FormUrlEncodedContent(fields);
        using var response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!response.IsSuccessStatusCode) throw CreateAuthError("Firebase token refresh", response.StatusCode, body);
        var json = ParseObject(body, "Firebase token refresh");
        return new FirebaseSession(
            IdToken: RequireString(json, "id_token"),
            RefreshToken: RequireString(json, "refresh_token"),
            LocalId: RequireString(json, "user_id"),
            Email: null,
            DisplayName: null,
            ExpiresIn: RequireInt(json, "expires_in"));
    }

    private static JsonObject ParseObject(string body, string action)
    {
        try
        {
            var json = JsonNode.Parse(body) as JsonObject
                ?? throw new InvalidOperationException($"{action} returned an unexpected response. Please try again.");
            _ = json.Count; // Validate duplicate keys before reading any fields.
            return json;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException)
        {
            throw new InvalidOperationException($"{action} returned an unreadable response. Please try again.");
        }
    }

    private static string RequireString(JsonObject json, string field)
    {
        var value = OptionalString(json, field);
        return !string.IsNullOrWhiteSpace(value) ? value
            : throw new InvalidOperationException($"Firebase returned an invalid '{field}' field. Please try again.");
    }

    private static string? OptionalString(JsonObject json, string field) =>
        json[field] is JsonValue node && node.TryGetValue<string>(out var value) ? value : null;

    private static int RequireInt(JsonObject json, string field)
    {
        if (json[field] is JsonValue node)
        {
            if (node.TryGetValue<int>(out var number) && number > 0) return number;
            if (node.TryGetValue<string>(out var text)
                && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) && number > 0) return number;
        }
        throw new InvalidOperationException($"Firebase returned an invalid '{field}' field. Please try again.");
    }

    private static FirebaseAuthException CreateAuthError(string action, HttpStatusCode status, string body)
    {
        string? code = null;
        try
        {
            if (JsonNode.Parse(body) is JsonObject root)
            {
                var message = root["error"] is JsonObject error
                    ? OptionalString(error, "message") : OptionalString(root, "error");
                code = KnownErrorCode(message);
            }
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException) { }
        return new FirebaseAuthException(action, status, code);
    }

    // Firebase may append ': details'; never expose those or arbitrary body fields.
    private static string? KnownErrorCode(string? message)
    {
        var code = message?.Split(':', 2)[0].Trim();
        return code is "TOKEN_EXPIRED" or "INVALID_REFRESH_TOKEN" or "USER_DISABLED" or "USER_NOT_FOUND"
            or "INVALID_ID_TOKEN" or "INVALID_IDP_RESPONSE" or "INVALID_CREDENTIAL" or "CREDENTIAL_TOO_OLD_LOGIN_AGAIN"
            or "EMAIL_EXISTS" or "FEDERATED_USER_ID_ALREADY_LINKED" or "OPERATION_NOT_ALLOWED"
            or "TOO_MANY_ATTEMPTS_TRY_LATER" or "PERMISSION_DENIED" or "INVALID_ARGUMENT" or "INVALID_GRANT"
            or "INVALID_API_KEY" or "PROJECT_NUMBER_MISMATCH" or "MISSING_REFRESH_TOKEN" or "MISSING_ID_TOKEN"
            or "INTERNAL" or "INTERNAL_ERROR" or "UNAVAILABLE" or "invalid_grant" or "invalid_request"
            ? code : null;
    }
}
