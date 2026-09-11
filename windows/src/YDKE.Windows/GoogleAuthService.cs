using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace YDKE_Windows;

internal sealed record GoogleTokenResponse(string IdToken, string AccessToken, string? RefreshToken, int ExpiresIn);

internal sealed class GoogleAuthService
{
    internal const string InvalidClientIdMessage =
        "Enter the complete OAuth client ID for a Desktop app from Google Cloud Console > APIs & Services > Credentials. " +
        "It must have a numeric project-number prefix and end in '.apps.googleusercontent.com'. " +
        "A project ID, email address or Firebase API key is not an OAuth client ID.";

    private static readonly Regex ClientIdPattern = new(
        @"\A[0-9]{1,20}-[A-Za-z0-9_-]{1,128}\.apps\.googleusercontent\.com\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private readonly HttpClient _httpClient;

    public GoogleAuthService(HttpClient? httpClient = null) => _httpClient = httpClient ?? new HttpClient();

    // Syntax alone cannot prove registration or the Desktop app client type.
    public static bool IsValidClientId(string? value) => value is not null && ClientIdPattern.IsMatch(value.Trim());

    /// <summary>RFC 7636 PKCE pair: a 32-byte random verifier (43 base64url characters, within
    /// the required 43-128 range) and its S256 challenge.</summary>
    public static (string Verifier, string Challenge) CreatePkce()
    {
        var verifier = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    public static string CreateState() => Base64UrlEncode(RandomNumberGenerator.GetBytes(24));

    public static string BuildAuthorizationUrl(string clientId, string codeChallenge, string state, string redirectUri)
    {
        var query = string.Join('&',
            $"client_id={Uri.EscapeDataString(clientId)}",
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}",
            $"response_type={Uri.EscapeDataString("code")}",
            $"scope={Uri.EscapeDataString("openid email profile")}",
            $"code_challenge={Uri.EscapeDataString(codeChallenge)}",
            $"code_challenge_method={Uri.EscapeDataString("S256")}",
            $"state={Uri.EscapeDataString(state)}",
            $"access_type={Uri.EscapeDataString("offline")}",
            $"prompt={Uri.EscapeDataString("select_account")}");
        return $"{CloudConfig.GoogleAuthEndpoint}?{query}";
    }

    public Task<GoogleTokenResponse> ExchangeCodeAsync(
        string clientId, string? clientSecret, string code, string codeVerifier, string redirectUri, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(codeVerifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(redirectUri);
        var fields = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri,
        };
        if (!string.IsNullOrEmpty(clientSecret)) fields["client_secret"] = clientSecret;
        return PostForTokenAsync("Google code exchange", fields, cancellationToken);
    }

    // Google's refresh response never includes refresh_token; RefreshToken is legitimately null here.
    public Task<GoogleTokenResponse> RefreshAsync(string clientId, string? clientSecret, string refreshToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        var fields = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token",
        };
        if (!string.IsNullOrEmpty(clientSecret)) fields["client_secret"] = clientSecret;
        return PostForTokenAsync("Google token refresh", fields);
    }

    private async Task<GoogleTokenResponse> PostForTokenAsync(
        string action, Dictionary<string, string> fields, CancellationToken cancellationToken = default)
    {
        using var content = new FormUrlEncodedContent(fields);
        using var response = await _httpClient.PostAsync(CloudConfig.GoogleTokenEndpoint, content, cancellationToken).ConfigureAwait(false);
        // Read the body before checking the status: EnsureSuccessStatusCode would discard the real reason.
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(DescribeError(action, response.StatusCode, body));
        var json = ParseObject(body, action);
        return new GoogleTokenResponse(
            IdToken: RequireString(json, "id_token"),
            AccessToken: RequireString(json, "access_token"),
            RefreshToken: OptionalString(json, "refresh_token"),
            ExpiresIn: RequireInt(json, "expires_in"));
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static JsonObject ParseObject(string body, string action)
    {
        try { return JsonNode.Parse(body) as JsonObject ?? throw new InvalidOperationException($"{action} returned an unexpected response body: {body}"); }
        catch (JsonException ex) { throw new InvalidOperationException($"{action} returned an unparseable response body: {body}", ex); }
    }

    private static string RequireString(JsonObject json, string field) =>
        json[field]?.GetValue<string>() ?? throw new InvalidOperationException($"Response is missing required field '{field}'.");

    private static string? OptionalString(JsonObject json, string field) => json[field]?.GetValue<string>();

    private static int RequireInt(JsonObject json, string field)
    {
        var node = json[field] ?? throw new InvalidOperationException($"Response is missing required field '{field}'.");
        return node.GetValueKind() == JsonValueKind.String
            ? int.Parse(node.GetValue<string>(), CultureInfo.InvariantCulture)
            : node.GetValue<int>();
    }

    private static string DescribeError(string action, HttpStatusCode status, string body)
    {
        string? message = null;
        string? description = null;
        try
        {
            if (JsonNode.Parse(body) is JsonObject root)
            {
                if (root["error"] is JsonValue errorValue && errorValue.TryGetValue<string>(out var errorText)) message = errorText;
                else if (root["error"] is JsonObject errorObject) message = errorObject["message"]?.GetValue<string>();
                description = root["error_description"]?.GetValue<string>();
            }
        }
        catch (JsonException) { }
        var reason = string.Join(" ", new[] { message, description }.Where(part => !string.IsNullOrWhiteSpace(part)));
        if (string.Equals(message, "invalid_client", StringComparison.Ordinal))
        {
            return $"{action} failed with HTTP {(int)status} {status}: {reason}. " +
                "Verify in Google Cloud Console that this OAuth client exists in the project used by Firebase, " +
                "has the Desktop app type, and matches the configured client secret, if required. " +
                "Use the complete OAuth client ID, not a project ID, email address or Firebase API key.";
        }
        return string.IsNullOrWhiteSpace(reason)
            ? $"{action} failed with HTTP {(int)status} {status}: {body}"
            : $"{action} failed with HTTP {(int)status} {status}: {reason}";
    }
}
