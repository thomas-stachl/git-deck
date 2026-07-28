using System.Text.Json;

namespace GitDeck.Core.Settings;

public class SettingsService : ISettingsService
{
    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GitDeck",
        "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public AppSettings Settings { get; } = Load();

    public void Save()
    {
        var directory = Path.GetDirectoryName(SettingsFilePath)!;
        Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(Settings, JsonOptions);
        File.WriteAllText(SettingsFilePath, json);
    }

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
        }

        return new AppSettings();
    }
}
