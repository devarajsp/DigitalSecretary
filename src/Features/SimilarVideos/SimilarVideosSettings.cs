using System.Text.Json;
using System.Text.Json.Serialization;

namespace DigitalSecretary.Features.SimilarVideos;

/// <summary>Remembered, non-sensitive settings: the folder and the matching / removal options.</summary>
public sealed class SimilarVideosSettings
{
    public string Root { get; set; } = "";
    public VideoSensitivity Sensitivity { get; set; } = VideoSensitivity.Balanced;

    /// <summary>Match turned and mirrored copies. On by default.</summary>
    public bool IncludeRotated { get; set; } = true;

    /// <summary>Show "same scene, some differences" groups for manual review. On by default.</summary>
    public bool IncludeSimilar { get; set; } = true;

    /// <summary>Leave out the motion part of iPhone Live Photos. On by default, so a clean-up never breaks one.</summary>
    public bool SkipLivePhotos { get; set; } = true;

    /// <summary>Where removed videos go. The review folder by default, so every move can be undone.</summary>
    public RemoveAction Action { get; set; } = RemoveAction.ReviewFolder;

    public VideoFindOptions ToFindOptions() => new()
    {
        Sensitivity = Sensitivity,
        IncludeRotated = IncludeRotated,
        IncludeSimilar = IncludeSimilar
    };
}

/// <summary>Loads and saves <see cref="SimilarVideosSettings"/> in the feature's own data folder.</summary>
public sealed class SimilarVideosSettingsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _file;

    public SimilarVideosSettingsStore(string dataDirectory)
        => _file = Path.Combine(dataDirectory, "similar_videos_settings.json");

    public SimilarVideosSettings Load()
    {
        try
        {
            if (File.Exists(_file))
            {
                return JsonSerializer.Deserialize<SimilarVideosSettings>(File.ReadAllText(_file), JsonOpts) ?? new();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or NotSupportedException)
        {
            // Corrupt or unreadable settings - fall back to the safe defaults.
        }
        return new();
    }

    public void Save(SimilarVideosSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file, JsonSerializer.Serialize(settings, JsonOpts));
    }
}
