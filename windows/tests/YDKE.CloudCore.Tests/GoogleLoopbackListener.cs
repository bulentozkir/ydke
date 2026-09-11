namespace YDKE_Windows;

/// <summary>Code is null for a genuine user cancel (no ErrorMessage); a real failure (Google
/// error, CSRF state mismatch, timeout) also has Code null but a non-null ErrorMessage.
/// RedirectUri is the exact value used for THIS attempt -- the token exchange must reuse it.</summary>
internal sealed record GoogleAuthorizationResult(string? Code, string RedirectUri, string? ErrorMessage);

// Test-only stand-in: the real GoogleLoopbackListener opens a system browser and a local HTTP
// listener, neither of which this dependency-free console harness can drive (a separate
// assembly from the real one -- this type never coexists with it). Tests script the
// authorization result via Handler before calling into CloudSyncCoordinator, so
// CloudSyncCoordinator's own real, unmodified logic is exercised end-to-end; only the
// browser/listener plumbing itself is stood in for.
internal static class GoogleLoopbackListener
{
    public static Func<string, string, string, CancellationToken, GoogleSignInBrowser, Task<GoogleAuthorizationResult>>? Handler { get; set; }

    public static Task<GoogleAuthorizationResult> RequestAuthorizationCodeAsync(
        string clientId, string codeChallenge, string state, CancellationToken cancellationToken,
        GoogleSignInBrowser browser = GoogleSignInBrowser.Default) =>
        (Handler ?? throw new InvalidOperationException("No GoogleLoopbackListener.Handler was configured for this test."))
            (clientId, codeChallenge, state, cancellationToken, browser);
}
