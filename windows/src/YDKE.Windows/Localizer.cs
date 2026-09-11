namespace YDKE_Windows;

internal static class Localizer
{
    public static readonly IReadOnlyList<LanguageOption> UiLanguages = Array.AsReadOnly<LanguageOption>(
    [
        new("tr", "Türkçe"),
        new("en", "English"),
        new("de", "Deutsch"),
        new("fr", "Français"),
        new("es", "Español"),
        new("pt", "Português"),
        new("nl", "Nederlands"),
    ]);

    // Explicit resources: no English overlays or initialization-order coupling.
    public static string Get(string languageCode, string key) =>
        ExperienceStrings.Get(languageCode, key, key, key);

    public static IReadOnlyList<string> MissingKeys(string language) =>
        ExperienceStrings.MissingKeys(language);

    public static IReadOnlyList<string> MissingKeys(string language, IEnumerable<string> expectedKeys) =>
        ExperienceStrings.MissingKeys(language, expectedKeys);
}