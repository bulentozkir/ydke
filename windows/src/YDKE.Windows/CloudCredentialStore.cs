using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YDKE_Windows;

// Keep account credentials separate from backups and protect both refresh tokens with DPAPI.
internal sealed class CloudCredentialStore
{
    private const string FileName = "cloudcredentials.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new GoogleBrowserConverter() },
    };

    private readonly string _folder;

    public CloudCredentialStore(string? folder = null) =>
        _folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YDKE")));

    public string FolderPath => _folder;

    private string FilePath => Path.Combine(_folder, FileName);

    public async Task<CloudCredentials> LoadAsync()
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(FilePath).ConfigureAwait(false);
            var record = JsonSerializer.Deserialize<StoredRecord>(bytes, JsonOptions);
            if (record is null) return new CloudCredentials();
            return new CloudCredentials
            {
                GoogleClientId = record.GoogleClientId,
                GoogleClientSecret = record.GoogleClientSecret,
                GoogleBrowser = record.GoogleBrowser,
                GoogleRefreshToken = Unprotect(record.ProtectedGoogleRefreshToken),
                FirebaseRefreshToken = Unprotect(record.ProtectedFirebaseRefreshToken),
                FirebaseUid = record.FirebaseUid,
                Email = record.Email,
                DisplayName = record.DisplayName,
            };
        }
        // Missing file, unreadable file, malformed JSON, or a corrupted protected token must
        // all fall back to an empty result rather than throw.
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
            or FormatException or CryptographicException)
        {
            return new CloudCredentials();
        }
    }

    public async Task SaveAsync(CloudCredentials credentials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(_folder);
        var record = new StoredRecord
        {
            GoogleClientId = credentials.GoogleClientId,
            GoogleClientSecret = credentials.GoogleClientSecret,
            GoogleBrowser = credentials.GoogleBrowser,
            ProtectedGoogleRefreshToken = Protect(credentials.GoogleRefreshToken),
            ProtectedFirebaseRefreshToken = Protect(credentials.FirebaseRefreshToken),
            FirebaseUid = credentials.FirebaseUid,
            Email = credentials.Email,
            DisplayName = credentials.DisplayName,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(record, JsonOptions);
        var temporary = Path.Combine(_folder, $"{FileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, FilePath, overwrite: true);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    public Task ClearAsync()
    {
        try { File.Delete(FilePath); }
        catch (DirectoryNotFoundException) { }
        return Task.CompletedTask;
    }

    private static string? Protect(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return null;
        var protectedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plainText), null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    private static string? Unprotect(string? protectedBase64)
    {
        if (string.IsNullOrEmpty(protectedBase64)) return null;
        var plainBytes = ProtectedData.Unprotect(Convert.FromBase64String(protectedBase64), null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plainBytes);
    }

    private sealed class StoredRecord
    {
        public string? GoogleClientId { get; set; }
        public string? GoogleClientSecret { get; set; }
        public GoogleSignInBrowser GoogleBrowser { get; set; } = GoogleSignInBrowser.Default;
        public string? ProtectedGoogleRefreshToken { get; set; }
        public string? ProtectedFirebaseRefreshToken { get; set; }
        public string? FirebaseUid { get; set; }
        public string? Email { get; set; }
        public string? DisplayName { get; set; }
    }

    private sealed class GoogleBrowserConverter : JsonConverter<GoogleSignInBrowser>
    {
        public override GoogleSignInBrowser Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var number)
                && Enum.IsDefined((GoogleSignInBrowser)number)) return (GoogleSignInBrowser)number;
            if (reader.TokenType == JsonTokenType.String
                && Enum.TryParse<GoogleSignInBrowser>(reader.GetString(), out var browser)
                && Enum.IsDefined(browser)) return browser;
            reader.Skip();
            return GoogleSignInBrowser.Default;
        }

        public override void Write(Utf8JsonWriter writer, GoogleSignInBrowser value, JsonSerializerOptions options) =>
            writer.WriteNumberValue((int)(Enum.IsDefined(value) ? value : GoogleSignInBrowser.Default));
    }
}
