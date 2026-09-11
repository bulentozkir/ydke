using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace YDKE_Windows;

internal enum GoogleSignInBrowser
{
    Default,
    Edge,
    Chrome,
}

internal static class GoogleBrowserLauncher
{
    private static readonly Uri AllowedEndpoint = new(CloudConfig.GoogleAuthEndpoint);

    public static void LaunchSignInPage(Uri callbackPage, GoogleSignInBrowser browser = GoogleSignInBrowser.Default)
    {
        var startInfo = CreateLocalSignInStartInfo(callbackPage, browser, ResolveInstalledExecutable);
        try
        {
            using var process = Process.Start(startInfo);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            // Do not include the attempt URL (or OS error text) in a surfaced exception.
            throw new InvalidOperationException(
                $"Could not open {BrowserName(browser)} for Google sign-in. Check your browser settings or choose another available browser.");
        }
    }

    internal static ProcessStartInfo CreateLocalSignInStartInfo(
        Uri callbackPage, GoogleSignInBrowser browser, Func<GoogleSignInBrowser, string?> resolveExecutable)
    {
        ValidateLocalSignInPage(callbackPage);
        if (browser == GoogleSignInBrowser.Default)
            return new ProcessStartInfo(callbackPage.AbsoluteUri) { UseShellExecute = true };

        var executableName = ExecutableName(browser);
        ArgumentNullException.ThrowIfNull(resolveExecutable);
        var executable = NormalizeExecutablePath(resolveExecutable(browser), executableName);
        if (executable is null)
        {
            throw new InvalidOperationException(
                $"{BrowserName(browser)} was not found in Windows app registration or standard installation folders. Choose System default or another available browser.");
        }

        var startInfo = new ProcessStartInfo(executable) { UseShellExecute = false };
        startInfo.ArgumentList.Add(callbackPage.AbsoluteUri);
        return startInfo;
    }

    internal static void ValidateLocalSignInPage(Uri callbackPage)
    {
        ArgumentNullException.ThrowIfNull(callbackPage);
        if (!callbackPage.IsAbsoluteUri
            || callbackPage.Scheme != Uri.UriSchemeHttp
            || callbackPage.Host != "localhost"
            || callbackPage.Port <= 0 || callbackPage.IsDefaultPort
            || callbackPage.UserInfo.Length != 0 || callbackPage.Query.Length != 0 || callbackPage.Fragment.Length != 0)
            throw new ArgumentException("Only a local YDKE sign-in page can be opened.", nameof(callbackPage));

        var path = callbackPage.AbsolutePath;
        if (path.Length != 50 || !path.StartsWith("/auth/", StringComparison.Ordinal) || path[^1] != '/'
            || !path[6..^1].All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
            // Reject URI normalization tricks, escaped paths, extra slashes and leading-zero ports.
            || !string.Equals(callbackPage.OriginalString,
                $"http://localhost:{callbackPage.Port.ToString(System.Globalization.CultureInfo.InvariantCulture)}{path}", StringComparison.Ordinal))
            throw new ArgumentException("Only a local YDKE sign-in page can be opened.", nameof(callbackPage));
    }

    public static void Launch(string authorizationUrl, GoogleSignInBrowser browser = GoogleSignInBrowser.Default)
    {
        var startInfo = CreateStartInfo(authorizationUrl, browser, ResolveInstalledExecutable);
        try
        {
            using var process = Process.Start(startInfo);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            throw new InvalidOperationException(
                $"Could not open {BrowserName(browser)} for Google sign-in. Check your browser settings or choose another available browser.", ex);
        }
    }

    internal static ProcessStartInfo CreateStartInfo(
        string authorizationUrl, GoogleSignInBrowser browser, Func<GoogleSignInBrowser, string?> resolveExecutable)
    {
        if (string.IsNullOrWhiteSpace(authorizationUrl)
            || authorizationUrl.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c == '\\')
            || !Uri.IsWellFormedUriString(authorizationUrl, UriKind.Absolute)
            || !Uri.TryCreate(authorizationUrl, UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(uri.Host, AllowedEndpoint.Host, StringComparison.OrdinalIgnoreCase)
            || uri.Port != AllowedEndpoint.Port
            || uri.AbsolutePath != AllowedEndpoint.AbsolutePath
            || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0)
        {
            throw new ArgumentException("Only the Google HTTPS authorization endpoint can be opened.", nameof(authorizationUrl));
        }

        if (browser == GoogleSignInBrowser.Default)
            return new ProcessStartInfo(authorizationUrl) { UseShellExecute = true };

        var executableName = ExecutableName(browser);
        ArgumentNullException.ThrowIfNull(resolveExecutable);
        var executable = NormalizeExecutablePath(resolveExecutable(browser), executableName);
        if (executable is null)
        {
            throw new InvalidOperationException(
                $"{BrowserName(browser)} was not found in Windows app registration or standard installation folders. Choose System default or another available browser.");
        }

        var startInfo = new ProcessStartInfo(executable) { UseShellExecute = false };
        startInfo.ArgumentList.Add(authorizationUrl);
        return startInfo;
    }

    internal static string? FindExecutable(
        GoogleSignInBrowser browser, IEnumerable<string?> candidates, Func<string, bool> fileExists)
    {
        var executableName = ExecutableName(browser);
        foreach (var candidate in candidates)
        {
            var path = NormalizeExecutablePath(candidate, executableName);
            if (path is not null && fileExists(path)) return path;
        }
        return null;
    }

    private static string? ResolveInstalledExecutable(GoogleSignInBrowser browser)
    {
        if (!OperatingSystem.IsWindows()) return null;
        return FindExecutable(browser, WindowsExecutableCandidates(browser), File.Exists);
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<string?> WindowsExecutableCandidates(GoogleSignInBrowser browser)
    {
        var executableName = ExecutableName(browser);
        var views = Environment.Is64BitOperatingSystem
            ? new[] { RegistryView.Default, RegistryView.Registry64, RegistryView.Registry32 }
            : new[] { RegistryView.Default, RegistryView.Registry32 };
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        {
            foreach (var view in views) yield return ReadAppPath(hive, view, executableName);
        }

        var relativePath = browser == GoogleSignInBrowser.Edge
            ? Path.Combine("Microsoft", "Edge", "Application", executableName)
            : Path.Combine("Google", "Chrome", "Application", executableName);
        // ProgramW6432 covers the native installation when this process is x86 on x64/ARM64 Windows.
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetEnvironmentVariable("ProgramW6432"),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        };
        foreach (var root in roots.Where(root => !string.IsNullOrWhiteSpace(root)).Distinct(StringComparer.OrdinalIgnoreCase))
            yield return Path.Combine(root!, relativePath);
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadAppPath(RegistryHive hive, RegistryView view, string executableName)
    {
        try
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            using var key = root.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{executableName}");
            return key?.GetValue(null) as string;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return null;
        }
    }

    private static string? NormalizeExecutablePath(string? candidate, string executableName)
    {
        var path = candidate?.Trim();
        if (path is { Length: >= 2 } && path[0] == '"' && path[^1] == '"') path = path[1..^1];
        if (string.IsNullOrWhiteSpace(path) || path.Any(c => char.IsControl(c) || c == '"')
            || !Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal)
            || !string.Equals(Path.GetFileName(path), executableName, StringComparison.OrdinalIgnoreCase))
            return null;
        return path;
    }

    private static string ExecutableName(GoogleSignInBrowser browser) => browser switch
    {
        GoogleSignInBrowser.Edge => "msedge.exe",
        GoogleSignInBrowser.Chrome => "chrome.exe",
        _ => throw new ArgumentOutOfRangeException(nameof(browser), "Choose System default, Microsoft Edge or Google Chrome for Google sign-in."),
    };

    private static string BrowserName(GoogleSignInBrowser browser) => browser switch
    {
        GoogleSignInBrowser.Edge => "Microsoft Edge",
        GoogleSignInBrowser.Chrome => "Google Chrome",
        _ => "your default browser",
    };
}