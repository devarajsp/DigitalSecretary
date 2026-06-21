using System.Text.Json;

namespace DigitalSecretary.Features.EmailDownloader;

/// <summary>Remembered, non-sensitive settings. The password is never stored.</summary>
public sealed class EmailSettings
{
    public string Email { get; set; } = "";
    public string DownloadDir { get; set; } = "";
}

/// <summary>Loads/saves <see cref="EmailSettings"/> in the feature's own data folder.</summary>
public sealed class EmailSettingsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly string _file;

    public EmailSettingsStore(string dataDirectory) => _file = Path.Combine(dataDirectory, "email_settings.json");

    public EmailSettings Load()
    {
        try
        {
            if (File.Exists(_file))
                return JsonSerializer.Deserialize<EmailSettings>(File.ReadAllText(_file)) ?? new();
        }
        catch
        {
            // Ignore corrupt settings.
        }
        return new();
    }

    public void Save(EmailSettings settings)
        => File.WriteAllText(_file, JsonSerializer.Serialize(settings, JsonOpts));
}
