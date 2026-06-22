using System.Text.Json;

namespace DigitalSecretary.Features.GmailDownloader;

/// <summary>Remembered, non-sensitive settings. The password is never stored.</summary>
public sealed class GmailSettings
{
    public string Email { get; set; } = "";
    public string DownloadDir { get; set; } = "";
}

/// <summary>Loads/saves <see cref="GmailSettings"/> in the feature's own data folder.</summary>
public sealed class GmailSettingsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly string _file;

    public GmailSettingsStore(string dataDirectory) => _file = Path.Combine(dataDirectory, "gmail_settings.json");

    public GmailSettings Load()
    {
        try
        {
            if (File.Exists(_file))
                return JsonSerializer.Deserialize<GmailSettings>(File.ReadAllText(_file)) ?? new();
        }
        catch
        {
            // Ignore corrupt settings.
        }
        return new();
    }

    public void Save(GmailSettings settings)
        => File.WriteAllText(_file, JsonSerializer.Serialize(settings, JsonOpts));
}
