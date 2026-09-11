using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace YDKE_Windows;

/// <summary>Stores exactly one opaque backup string per Firebase uid; does not map every
/// UserSettings/ProgressState field into typed Firestore values.</summary>
internal sealed class FirestoreSyncService
{
    private readonly HttpClient _httpClient;

    public FirestoreSyncService(HttpClient? httpClient = null) => _httpClient = httpClient ?? new HttpClient();

    public async Task SaveBackupAsync(string uid, string firebaseIdToken, string backupJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uid);
        ArgumentException.ThrowIfNullOrWhiteSpace(firebaseIdToken);
        ArgumentNullException.ThrowIfNull(backupJson);
        var body = new JsonObject
        {
            ["fields"] = new JsonObject
            {
                ["backup"] = new JsonObject { ["stringValue"] = JsonValue.Create(backupJson) },
                // Firestore's integerValue must be a JSON string, not a bare JSON number.
                ["updatedAtMs"] = new JsonObject
                {
                    ["integerValue"] = JsonValue.Create(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)),
                },
            },
        };
        using var request = new HttpRequestMessage(HttpMethod.Patch, DocumentUrl(uid))
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", firebaseIdToken);
        using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
        if (response.IsSuccessStatusCode) return;
        var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        throw new InvalidOperationException(DescribeError("Firestore save", response.StatusCode, responseBody));
    }

    public async Task<string?> LoadBackupAsync(string uid, string firebaseIdToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uid);
        ArgumentException.ThrowIfNullOrWhiteSpace(firebaseIdToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, DocumentUrl(uid));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", firebaseIdToken);
        using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(DescribeError("Firestore load", response.StatusCode, body));
        var json = ParseObject(body, "Firestore load");
        return json["fields"]?["backup"]?["stringValue"]?.GetValue<string>();
    }

    public async Task DeleteBackupAsync(string uid, string firebaseIdToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uid);
        ArgumentException.ThrowIfNullOrWhiteSpace(firebaseIdToken);
        using var request = new HttpRequestMessage(HttpMethod.Delete, DocumentUrl(uid));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", firebaseIdToken);
        using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound) return;
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        throw new InvalidOperationException(DescribeError("Firestore delete", response.StatusCode, body));
    }

    private static string DocumentUrl(string uid) => string.Format(CultureInfo.InvariantCulture,
        CloudConfig.FirestoreBaseUrl, CloudConfig.FirebaseProjectId, CloudConfig.FirestoreCollection, uid);

    private static JsonObject ParseObject(string body, string action)
    {
        try { return JsonNode.Parse(body) as JsonObject ?? throw new InvalidOperationException($"{action} returned an unexpected response body: {body}"); }
        catch (JsonException ex) { throw new InvalidOperationException($"{action} returned an unparseable response body: {body}", ex); }
    }

    private static string DescribeError(string action, HttpStatusCode status, string body)
    {
        string? message = null;
        try
        {
            // Firestore uses Google API-style {"error":{"code":..,"message":"..","status":".."}}.
            if (JsonNode.Parse(body) is JsonObject root && root["error"] is JsonObject errorObject)
                message = errorObject["message"]?.GetValue<string>();
        }
        catch (JsonException) { }
        return string.IsNullOrWhiteSpace(message)
            ? $"{action} failed with HTTP {(int)status} {status}: {body}"
            : $"{action} failed with HTTP {(int)status} {status}: {message}";
    }
}
