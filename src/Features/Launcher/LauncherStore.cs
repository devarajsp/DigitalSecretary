using System.Text.Json;

namespace DigitalSecretary.Features.Launcher;

/// <summary>Loads/saves launcher items as JSON inside the feature's own data folder.</summary>
public sealed class LauncherStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly string _file;

    public LauncherStore(string dataDirectory) => _file = Path.Combine(dataDirectory, "shortcuts.json");

    public List<LauncherItem> Load()
    {
        try
        {
            if (File.Exists(_file))
                return JsonSerializer.Deserialize<List<LauncherItem>>(File.ReadAllText(_file)) ?? new();
        }
        catch
        {
            // Corrupt file — start fresh.
        }
        return new();
    }

    public void Save(List<LauncherItem> items)
        => File.WriteAllText(_file, JsonSerializer.Serialize(items, JsonOpts));
}
