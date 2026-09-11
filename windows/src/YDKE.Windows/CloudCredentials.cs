namespace YDKE_Windows;

/// <summary>Google/Firebase account linkage, kept entirely separate from UserSettings/
/// ProgressState: never touched by AppStorage export/import or any local backup envelope.</summary>
internal sealed class CloudCredentials
{
    // Legacy OAuth settings only; new browser sign-ins leave these unset.
    public string? GoogleClientId { get; set; }

    public string? GoogleClientSecret { get; set; }

    public GoogleSignInBrowser GoogleBrowser { get; set; } = GoogleSignInBrowser.Default;

    public string? GoogleRefreshToken { get; set; }

    // Long-lived until Firebase revokes it; no browser-cookie expiry applies here.
    public string? FirebaseRefreshToken { get; set; }

    public string? FirebaseUid { get; set; }

    public string? Email { get; set; }

    public string? DisplayName { get; set; }

    public bool HasFirebaseSession => !string.IsNullOrWhiteSpace(FirebaseUid)
        && !string.IsNullOrWhiteSpace(FirebaseRefreshToken);
}
