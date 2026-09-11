using System.Text;

namespace YDKE_Windows;

internal sealed record CloudResult(bool Success, string? ErrorMessage);

// Browser delivery is untrusted until Firebase verifies the Google ID token.
internal sealed class CloudSyncCoordinator(
    GoogleAuthService? googleAuthService = null,
    FirebaseAuthService? firebaseAuthService = null,
    FirestoreSyncService? firestoreSyncService = null,
    CloudCredentialStore? credentialStore = null,
    Func<CancellationToken, GoogleSignInBrowser, string, Task<GoogleBrowserSignInResult>>? browserSignIn = null)
{
    private const string AccountChangedMessage = "The account changed during this request. Please try again.";
    private readonly GoogleAuthService _googleAuthService = googleAuthService ?? new();
    private readonly FirebaseAuthService _firebaseAuthService = firebaseAuthService ?? new();
    private readonly FirestoreSyncService _firestoreSyncService = firestoreSyncService ?? new();
    private readonly CloudCredentialStore _credentialStore = credentialStore ?? new();
    private readonly Func<CancellationToken, GoogleSignInBrowser, string, Task<GoogleBrowserSignInResult>> _browserSignIn =
        browserSignIn ?? GoogleBrowserSignIn.RequestIdTokenAsync;
    private readonly SemaphoreSlim _credentialGate = new(1, 1);

    private CloudCredentials _credentials = new();

    public bool IsConnected => _credentials.HasFirebaseSession;

    public string? Email => _credentials.Email;

    public string? DisplayName => _credentials.DisplayName;

    // Legacy compatibility only; the browser flow never needs OAuth client settings.
    public string? LastGoogleClientId => _credentials.GoogleClientId;

    public string? LastGoogleClientSecret => _credentials.GoogleClientSecret;

    public GoogleSignInBrowser LastGoogleBrowser => _credentials.GoogleBrowser;

    // Loading a saved session is local-only, regardless of its age.
    public async Task LoadCredentialsAsync()
    {
        await _credentialGate.WaitAsync().ConfigureAwait(false);
        try { _credentials = await _credentialStore.LoadAsync().ConfigureAwait(false); }
        finally { _credentialGate.Release(); }
    }

    public async Task<CloudResult> SignInWithBrowserAsync(
        CancellationToken cancellationToken = default,
        GoogleSignInBrowser browser = GoogleSignInBrowser.Default, string language = "en")
    {
        var previous = _credentials;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Enum.IsDefined(browser)) return new(false, "Choose System default, Microsoft Edge or Google Chrome for Google sign-in.");
            var authorization = await _browserSignIn(cancellationToken, browser, language).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (authorization.ErrorMessage is not null)
                return new(false, string.IsNullOrWhiteSpace(authorization.ErrorMessage)
                    ? "Google sign-in did not finish. Please try again." : authorization.ErrorMessage);
            if (string.IsNullOrWhiteSpace(authorization.IdToken)) return new(false, "Sign-in was cancelled.");

            var firebase = await _firebaseAuthService.SignInWithGoogleIdTokenAsync(authorization.IdToken, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var credentials = new CloudCredentials
            {
                GoogleBrowser = browser,
                FirebaseRefreshToken = firebase.RefreshToken,
                FirebaseUid = firebase.LocalId,
                Email = firebase.Email,
                DisplayName = firebase.DisplayName,
            };
            var saved = await CommitCredentialsAsync(previous, credentials, cancellationToken).ConfigureAwait(false);
            return new(saved, saved ? null : AccountChangedMessage);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(false, "Sign-in was cancelled.");
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
            return new(false, "The sign-in request was interrupted or timed out. Check your connection and try again.");
        }
        catch (FirebaseAuthException ex)
        {
            return new(false, ex.Message);
        }
        catch (HttpRequestException)
        {
            return new(false, "Could not reach the sign-in service. Check your connection and try again.");
        }
        catch (Exception)
        {
            return new(false, "Google sign-in could not finish. Please try again.");
        }
    }

    // Legacy compatibility only; native UI uses SignInWithBrowserAsync.
    public async Task<CloudResult> SignInAsync(
        string clientId, string? clientSecret, CancellationToken cancellationToken = default,
        GoogleSignInBrowser browser = GoogleSignInBrowser.Default)
    {
        var previous = _credentials;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!GoogleAuthService.IsValidClientId(clientId)) return new CloudResult(false, GoogleAuthService.InvalidClientIdMessage);
            clientId = clientId.Trim();
            if (!Enum.IsDefined(browser)) return new CloudResult(false, "Choose System default, Microsoft Edge or Google Chrome for Google sign-in.");
            var (verifier, challenge) = GoogleAuthService.CreatePkce();
            var state = GoogleAuthService.CreateState();
            var authorization = await GoogleLoopbackListener.RequestAuthorizationCodeAsync(clientId, challenge, state, cancellationToken, browser).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (authorization.ErrorMessage is not null) return new CloudResult(false, authorization.ErrorMessage);
            if (authorization.Code is null) return new CloudResult(false, "Sign-in was cancelled.");

            var google = await _googleAuthService.ExchangeCodeAsync(clientId, clientSecret, authorization.Code, verifier, authorization.RedirectUri, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var firebase = await _firebaseAuthService.SignInWithGoogleIdTokenAsync(google.IdToken, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var sameAccountAndClient = string.Equals(previous.FirebaseUid, firebase.LocalId, StringComparison.Ordinal)
                && string.Equals(previous.GoogleClientId?.Trim(), clientId, StringComparison.Ordinal);
            var credentials = new CloudCredentials
            {
                GoogleClientId = clientId,
                GoogleClientSecret = clientSecret,
                GoogleBrowser = browser,
                // A missing token may only reuse a grant belonging to the same account and OAuth client.
                GoogleRefreshToken = google.RefreshToken ?? (sameAccountAndClient ? previous.GoogleRefreshToken : null),
                FirebaseRefreshToken = firebase.RefreshToken,
                FirebaseUid = firebase.LocalId,
                Email = firebase.Email,
                DisplayName = firebase.DisplayName,
            };
            cancellationToken.ThrowIfCancellationRequested();
            var saved = await CommitCredentialsAsync(previous, credentials, cancellationToken).ConfigureAwait(false);
            return new CloudResult(saved, saved ? null : AccountChangedMessage);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new CloudResult(false, "Sign-in was cancelled.");
        }
        catch (OperationCanceledException)
        {
            return new CloudResult(false, "The sign-in request was interrupted or timed out. Check your connection and try again.");
        }
        catch (Exception ex)
        {
            return new CloudResult(false, ex.Message);
        }
    }

    // Sign-out removes account identity and tokens, not OAuth/browser settings or cloud data.
    public Task SignOutAsync() => ClearSessionAsync();

    public async Task<CloudResult> SaveToCloudAsync(UserSettings settings, ProgressState progress)
    {
        if (!IsConnected) return new CloudResult(false, "Not connected to a Google account.");
        try
        {
            var session = await RefreshFirebaseSessionAsync().ConfigureAwait(false);
            var backupJson = Encoding.UTF8.GetString(AppStorage.EncodeBackup(settings, progress));
            await _firestoreSyncService.SaveBackupAsync(session.LocalId, session.IdToken, backupJson).ConfigureAwait(false);
            return new CloudResult(true, null);
        }
        catch (Exception ex)
        {
            return new CloudResult(false, ex.Message);
        }
    }

    /// <summary>A null cloud backup (nothing saved yet) and a corrupt/tampered cloud backup are
    /// both reported as CloudResult(false, ...) with null Settings/Progress -- never applied silently.</summary>
    public async Task<(CloudResult Result, UserSettings? Settings, ProgressState? Progress)> LoadFromCloudAsync()
    {
        if (!IsConnected) return (new CloudResult(false, "Not connected to a Google account."), null, null);
        try
        {
            var session = await RefreshFirebaseSessionAsync().ConfigureAwait(false);
            var backupJson = await _firestoreSyncService.LoadBackupAsync(session.LocalId, session.IdToken).ConfigureAwait(false);
            if (backupJson is null) return (new CloudResult(false, "No cloud backup was found yet."), null, null);
            var (settings, progress) = AppStorage.DecodeBackup(Encoding.UTF8.GetBytes(backupJson));
            return (new CloudResult(true, null), settings, progress);
        }
        catch (Exception ex)
        {
            return (new CloudResult(false, ex.Message), null, null);
        }
    }

    /// <summary>Deletes only the cloud document; does not sign out or clear local credentials.</summary>
    public async Task<CloudResult> DeleteCloudProfileAsync()
    {
        if (!IsConnected) return new CloudResult(false, "Not connected to a Google account.");
        try
        {
            var session = await RefreshFirebaseSessionAsync().ConfigureAwait(false);
            await _firestoreSyncService.DeleteBackupAsync(session.LocalId, session.IdToken).ConfigureAwait(false);
            return new CloudResult(true, null);
        }
        catch (Exception ex)
        {
            return new CloudResult(false, ex.Message);
        }
    }

    // Refresh Firebase, not Google. Only authoritative revocation clears the session.
    private async Task<FirebaseSession> RefreshFirebaseSessionAsync()
    {
        var previous = _credentials;
        FirebaseSession session;
        try
        {
            session = await _firebaseAuthService.RefreshAsync(previous.FirebaseRefreshToken!).ConfigureAwait(false);
        }
        catch (FirebaseAuthException ex) when (ex.IsSessionRevoked)
        {
            await ClearSessionAsync(previous).ConfigureAwait(false);
            throw;
        }
        if (!string.Equals(previous.FirebaseUid, session.LocalId, StringComparison.Ordinal))
        {
            await ClearSessionAsync(previous).ConfigureAwait(false);
            throw new InvalidOperationException(FirebaseAuthException.ReauthenticationMessage);
        }
        var credentials = new CloudCredentials
        {
            GoogleClientId = previous.GoogleClientId,
            GoogleClientSecret = previous.GoogleClientSecret,
            GoogleBrowser = previous.GoogleBrowser,
            GoogleRefreshToken = previous.GoogleRefreshToken,
            FirebaseRefreshToken = session.RefreshToken,
            FirebaseUid = previous.FirebaseUid,
            Email = previous.Email,
            DisplayName = previous.DisplayName,
        };
        if (!await CommitCredentialsAsync(previous, credentials).ConfigureAwait(false))
            throw new InvalidOperationException(AccountChangedMessage);
        return session;
    }

    // Serialize only disk commits, never the browser or network wait.
    private async Task<bool> CommitCredentialsAsync(
        CloudCredentials previous, CloudCredentials credentials, CancellationToken cancellationToken = default)
    {
        await _credentialGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ReferenceEquals(previous, _credentials)) return false;
            await _credentialStore.SaveAsync(credentials, cancellationToken).ConfigureAwait(false);
            _credentials = credentials;
            return true;
        }
        finally { _credentialGate.Release(); }
    }

    private async Task ClearSessionAsync(CloudCredentials? previous = null)
    {
        await _credentialGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (previous is not null && !ReferenceEquals(previous, _credentials)) return;
            var preserved = new CloudCredentials
            {
                GoogleClientId = _credentials.GoogleClientId,
                GoogleClientSecret = _credentials.GoogleClientSecret,
                GoogleBrowser = _credentials.GoogleBrowser,
            };
            _credentials = preserved;
            await _credentialStore.SaveAsync(preserved).ConfigureAwait(false);
        }
        finally { _credentialGate.Release(); }
    }
}
