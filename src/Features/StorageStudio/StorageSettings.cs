using System.Text.Json;

namespace DigitalSecretary.Features.StorageStudio;

/// <summary>
/// Remembered, non-sensitive settings for the feature: the last folder mapped and how deep the
/// size map nests. Nothing here is a secret.
/// </summary>
public sealed class StorageSettings
{
    /// <summary>The folder that was last scanned. Empty until the user picks one.</summary>
    public string Root { get; set; } = "";

    /// <summary>How many folder levels the size map draws inside each other (1 - 3).</summary>
    public int MapDepth { get; set; } = 2;

    /// <summary>Clamps values loaded from an edited or older settings file into range.</summary>
    public StorageSettings Normalized()
    {
        MapDepth = Math.Clamp(MapDepth, 1, 3);
        Root ??= "";
        return this;
    }
}

/// <summary>Loads and saves <see cref="StorageSettings"/> in the feature's own data folder.</summary>
public sealed class StorageSettingsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly string _file;

    public StorageSettingsStore(string dataDirectory)
        => _file = Path.Combine(dataDirectory, "storage_studio_settings.json");

    public StorageSettings Load()
    {
        try
        {
            if (File.Exists(_file))
            {
                return (JsonSerializer.Deserialize<StorageSettings>(File.ReadAllText(_file), JsonOpts) ?? new()).Normalized();
            }
        }
        catch
        {
            // Ignore corrupt settings - fall back to the defaults.
        }
        return new();
    }

    public void Save(StorageSettings settings)
        => File.WriteAllText(_file, JsonSerializer.Serialize(settings.Normalized(), JsonOpts));
}
