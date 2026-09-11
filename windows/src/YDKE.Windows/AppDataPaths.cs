namespace YDKE_Windows;

internal static class AppDataPaths
{
    public static string CurrentFolder { get; } = ResolveFolder(Environment.GetCommandLineArgs());

    public static string ResolveFolder(IEnumerable<string> arguments)
    {
        var args = arguments.ToArray();
        var testMode = args.Contains("--test-mode", StringComparer.Ordinal);
        var overrides = args.Where(arg => arg.StartsWith("--data-dir=", StringComparison.Ordinal)).ToArray();
        if (!testMode && overrides.Length == 0)
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YDKE");
        if (!testMode || overrides.Length != 1)
            throw new ArgumentException("An isolated profile requires --test-mode and exactly one --data-dir=<absolute directory>.");
        var folder = overrides[0]["--data-dir=".Length..];
        if (string.IsNullOrWhiteSpace(folder) || !Path.IsPathFullyQualified(folder))
            throw new ArgumentException("The isolated profile directory must be an absolute path.");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
    }
}