namespace YDKE_Windows;

/// <summary>Public, non-secret Google/Firebase endpoint and identifier constants. Access
/// control is Firestore Security Rules + Firebase Auth, not secrecy of these values.</summary>
internal static class CloudConfig
{
    public const string FirebaseApiKey = "AIzaSyDnAC7uD3oh-q8egu_cDLFrUcB09C9_n_g";
    public const string FirebaseProjectId = "udsp-9fedc";
    public const string FirebaseAuthDomain = "udsp-9fedc.firebaseapp.com";

    // Distinct from the retired webapp's "users" collection on purpose: the native app's
    // document shape is not the same, and must never collide with a web-app user's document.
    public const string FirestoreCollection = "ydke_users";

    public const string GoogleAuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    public const string GoogleTokenEndpoint = "https://oauth2.googleapis.com/token";
    public const string FirebaseSignInWithIdpEndpoint = "https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp";
    public const string FirebaseRefreshEndpoint = "https://securetoken.googleapis.com/v1/token";

    // Format args: projectId, collection, docId.
    public const string FirestoreBaseUrl = "https://firestore.googleapis.com/v1/projects/{0}/databases/(default)/documents/{1}/{2}";

    // Firebase's signInWithIdp "requestUri" field is not validated against the real OAuth
    // redirect (which is now a dynamic per-attempt loopback port); any stable placeholder works.
    public const string FirebaseRequestUriPlaceholder = "http://localhost";
}
