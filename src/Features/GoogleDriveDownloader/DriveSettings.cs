using System.Text.Json;

namespace DigitalSecretary.Features.GoogleDriveDownloader;

/// <summary>
/// Remembered, non-sensitive settings. No OAuth tokens are stored here — the Google client caches
/// them separately under the feature's <c>token</c> folder.
/// </summary>
public sealed class DriveSettings
{
    public string CredentialsPath { get; set; } = "";
    public string DownloadDir { get; set; } = "";
}

/// <summary>Loads/saves <see cref="DriveSettings"/> in the feature's own data folder.</summary>
public sealed class DriveSettingsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly string _file;

    public DriveSettingsStore(string dataDirectory) => _file = Path.Combine(dataDirectory, "drive_settings.json");

    public DriveSettings Load()
    {
        try
        {
            if (File.Exists(_file))
                return JsonSerializer.Deserialize<DriveSettings>(File.ReadAllText(_file)) ?? new();
        }
        catch
        {
            // Ignore corrupt settings.
        }
        return new();
    }

    public void Save(DriveSettings settings)
        => File.WriteAllText(_file, JsonSerializer.Serialize(settings, JsonOpts));
}
