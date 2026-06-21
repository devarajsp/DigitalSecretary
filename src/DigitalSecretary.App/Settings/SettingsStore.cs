using System.Text.Json;

namespace DigitalSecretary.App.Settings;

/// <summary>Loads/saves <see cref="AppSettings"/> from %APPDATA%\DigitalSecretary\app-settings.json.</summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static AppSettings Load() => Load(AppPaths.SettingsFile);

    public static void Save(AppSettings settings) => Save(settings, AppPaths.SettingsFile);

    /// <summary>Loads settings from a specific file (used by tests; falls back to defaults).</summary>
    public static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new();
        }
        catch
        {
            // Corrupt settings — fall back to defaults.
        }
        return new();
    }

    /// <summary>Saves settings to a specific file (used by tests).</summary>
    public static void Save(AppSettings settings, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, JsonOpts));
    }
}
