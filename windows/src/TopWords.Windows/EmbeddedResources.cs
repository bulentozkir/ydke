using System.IO;
using System.Reflection;

namespace TopWords.Windows;

internal static class EmbeddedResources
{
    /// <summary>
    /// The bootstrap is shared byte-for-byte with the other platform hosts, so the
    /// per-platform values arrive as tokens rather than as forks of the file.
    /// </summary>
    public static readonly Lazy<string> Bootstrap = new(() =>
        Read("Scripts.bootstrap.js")
            .Replace("__TW_PLATFORM__", "windows", StringComparison.Ordinal)
            .Replace("__TW_IS_MSIX__", AppConfig.IsPackaged ? "true" : "false", StringComparison.Ordinal));

    public static readonly Lazy<string> OfflineHtml = new(() =>
        Read("Assets.offline.html").Replace("__START_URL__", AppConfig.StartUrl, StringComparison.Ordinal));

    private static string Read(string suffix)
    {
        var assembly = Assembly.GetExecutingAssembly();

        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Embedded resource '{suffix}' was not found.");

        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded resource '{name}' could not be opened.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
