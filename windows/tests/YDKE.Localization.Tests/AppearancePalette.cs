namespace YDKE_Windows;

// Models only needs default color strings. Do not load WinUI, user settings,
// storage or the real application to test localization.
internal static class AppearancePalette
{
    public const string DefaultBackground = "#FFFFFF";
    public const string DefaultButton = "#000000";
    public const string DefaultBox = "#FFFFFF";
}