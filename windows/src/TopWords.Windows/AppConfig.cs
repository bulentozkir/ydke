using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace TopWords.Windows;

/// <summary>
/// Single place for every value that differs between the web app and the packaged
/// Windows build. See <c>windows/03-blockers-and-fixes.md</c> for the blocker IDs
/// referenced in the comments.
/// </summary>
internal static class AppConfig
{
    /// <summary>
    /// Virtual hostname used by the C# packaged-content responder. The RFC 6761
    /// reserved <c>.example</c> TLD can never collide with a real network host.
    /// </summary>
    public const string VirtualHostName = "ydke.example";

    /// <summary>
    /// Origin the app runs against.
    /// <para>
    /// The web app ships inside the package (see <see cref="WebAppFolder"/>) and is
    /// served by the native host at this origin. The former udsp.vercel.app source
    /// is retired; the app no longer depends on any live remote origin.
    /// </para>
    /// <para>
    /// Changing <see cref="VirtualHostName"/> resets every user's local progress,
    /// because <c>localStorage</c> is partitioned per origin.
    /// </para>
    /// </summary>
    public const string AppOrigin = "https://" + VirtualHostName;

    public const string StartUrl = AppOrigin + "/";

    /// <summary>Folder containing the bundled web app served by <see cref="PackagedContent"/>.</summary>
    public static string WebAppFolder => Path.Combine(AppContext.BaseDirectory, "webapp");

    /// <summary>Appended to the WebView2 user agent so the site can detect the packaged build.</summary>
    public const string UserAgentSuffix = "TopWordsWin/1.0";

    public const string WindowTitle = "YDKE";

    // W10 — declare the window deliberately instead of accepting the packager default.
    public const double InitialWidth = 1000;
    public const double InitialHeight = 800;
    public const double MinWidth = 360;
    public const double MinHeight = 640;

    public const double PopupWidth = 520;
    public const double PopupHeight = 680;

    // Links pointing outside the app open in a restricted viewer rather than the
    // user's browser. It is fixed-size and has no minimise or maximise affordances.
    public const double ExternalViewerWidth = 900;
    public const double ExternalViewerHeight = 700;

    /// <summary>DevTools stay on in Debug builds only.</summary>
    public static bool DevToolsEnabled =>
#if DEBUG
        true;
#else
        false;
#endif

    /// <summary>
    /// WebView2 profile location.
    /// <para>
    /// <b>Measured, not assumed:</b> MSIX does <i>not</i> redirect
    /// <see cref="Environment.SpecialFolder.LocalApplicationData"/> for a
    /// <c>runFullTrust</c> Win32 app. A packaged run writes straight to the real
    /// <c>%LOCALAPPDATA%</c>, so leaving this alone would orphan the entire profile —
    /// cookies, IndexedDB, and Firebase auth tokens — when the Store app is
    /// uninstalled. The packaged build therefore targets the package-local path that
    /// MSIX genuinely removes on uninstall.
    /// </para>
    /// <para>
    /// <c>LocalCache</c> rather than <c>LocalState</c> is deliberate: it is excluded
    /// from backup and roaming, which is correct for a browser cache profile.
    /// </para>
    /// <para>
    /// Both values are <see cref="Lazy{T}"/> rather than plain initialisers because
    /// static initialisers run in textual order: a directly-initialised
    /// <c>UserDataFolder</c> declared above <c>PackageFamilyName</c> reads it as
    /// <c>null</c> and silently picks the unpackaged path. Deferring to first access
    /// removes the ordering dependency entirely.
    /// </para>
    /// </summary>
    public static string UserDataFolder => Path.Combine(StateFolderPath.Value, "WebView2");

    /// <summary>
    /// Root for everything the app writes about itself — currently the WebView2
    /// profile and the saved window placement. Shares the packaged/unpackaged
    /// resolution described on <see cref="UserDataFolder"/> so that uninstalling the
    /// MSIX build removes the whole tree rather than part of it.
    /// </summary>
    public static string StateFolder => StateFolderPath.Value;

    /// <summary>Whether the process is running with MSIX package identity.</summary>
    public static bool IsPackaged => PackageFamilyName.Value is not null;

    private static readonly Lazy<string?> PackageFamilyName = new(ResolvePackageFamilyName);

    private static readonly Lazy<string> StateFolderPath = new(BuildStateFolder);

    private static string BuildStateFolder()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return PackageFamilyName.Value is { } family
            ? Path.Combine(localAppData, "Packages", family, "LocalCache", "Local", "TopWords")
            : Path.Combine(localAppData, "TopWords");
    }

    private const int ErrorSuccess = 0;
    private const int ErrorInsufficientBuffer = 122;

    private static string? ResolvePackageFamilyName()
    {
        uint length = 0;

        // Unpackaged processes return APPMODEL_ERROR_NO_PACKAGE here rather than
        // reporting the buffer size, which is how the two cases are told apart.
        if (GetCurrentPackageFamilyName(ref length, null) != ErrorInsufficientBuffer)
        {
            return null;
        }

        var buffer = new StringBuilder((int)length);

        return GetCurrentPackageFamilyName(ref length, buffer) == ErrorSuccess
            ? buffer.ToString()
            : null;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetCurrentPackageFamilyName(ref uint packageFamilyNameLength, StringBuilder? packageFamilyName);

    /// <summary>
    /// Top-level navigations allowed to stay inside the app window. Anything else
    /// opens in the restricted <see cref="ExternalViewerWindow"/>. Subresources and
    /// iframes are unaffected.
    /// </summary>
    private static readonly string[] InAppHosts =
    {
        VirtualHostName,
        "accounts.google.com",
        "apis.google.com",
        "*.firebaseapp.com",
        "ssl.gstatic.com",
        "www.gstatic.com",
    };

    /// <summary>
    /// Hosts permitted to open a real popup window with <c>window.opener</c> intact.
    /// <para><b>W3:</b> this is what makes Firebase's <c>signInWithPopup</c> work in WebView2.</para>
    /// </summary>
    private static readonly string[] AuthHosts =
    {
        "accounts.google.com",
        "accounts.youtube.com",
        "apis.google.com",
        "*.firebaseapp.com",
    };

    /// <summary>
    /// <b>W4:</b> ad networks blocked at the network layer. AdSense inside a packaged
    /// app is an account-level policy risk, so the request never leaves the machine.
    /// <para>
    /// Deliberately excludes <c>accounts.google.com</c>, <c>apis.google.com</c>,
    /// <c>*.gstatic.com</c> and <c>*.googleapis.com</c>, which sign-in and Firestore need.
    /// </para>
    /// </summary>
    public static readonly string[] BlockedResourcePatterns =
    {
        "https://pagead2.googlesyndication.com/*",
        "https://*.googlesyndication.com/*",
        "https://googleads.g.doubleclick.net/*",
        "https://*.doubleclick.net/*",
        "https://adservice.google.com/*",
        "https://*.adtrafficquality.google/*",
    };

    public static bool IsAllowedInApp(string uri) => MatchesAny(uri, InAppHosts);

    public static bool IsAuthUrl(string uri) => MatchesAny(uri, AuthHosts);

    private static bool MatchesAny(string uri, string[] patterns)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        // Non-web schemes (about:blank, data:) are produced by the host itself.
        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
        {
            return true;
        }

        foreach (var pattern in patterns)
        {
            if (HostMatches(parsed.Host, pattern))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HostMatches(string host, string pattern)
    {
        if (pattern.StartsWith("*.", StringComparison.Ordinal))
        {
            // "*.firebaseapp.com" -> host must end with ".firebaseapp.com",
            // so "evil-firebaseapp.com" is correctly rejected.
            return host.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(host, pattern, StringComparison.OrdinalIgnoreCase);
    }
}
