using System.Text.Json;

namespace YDKE_Windows;

internal sealed class AppStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YDKE");

    public Task<UserSettings> LoadSettingsAsync() =>
        LoadAsync("settings.json", new UserSettings());

    public Task SaveSettingsAsync(UserSettings settings) =>
        SaveAsync("settings.json", settings);

    public Task<ProgressState> LoadProgressAsync() =>
        LoadAsync("progress.json", new ProgressState());

    public Task SaveProgressAsync(ProgressState progress) =>
        SaveAsync("progress.json", progress);

    private async Task<T> LoadAsync<T>(string fileName, T fallback)
    {
        try
        {
            var path = Path.Combine(_folder, fileName);
            if (!File.Exists(path))
            {
                return fallback;
            }

            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions) ?? fallback;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return fallback;
        }
    }

    private async Task SaveAsync<T>(string fileName, T value)
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, fileName);
        var temporaryPath = path + ".tmp";

        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, value, JsonOptions);
        }

        File.Move(temporaryPath, path, overwrite: true);
    }
}