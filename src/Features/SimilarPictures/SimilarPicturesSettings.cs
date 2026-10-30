using System.Text.Json;
using System.Text.Json.Serialization;

namespace DigitalSecretary.Features.SimilarPictures;

/// <summary>Remembered, non-sensitive settings: the folder and the matching / removal options.</summary>
public sealed class SimilarPicturesSettings
{
    public string Root { get; set; } = "";
    public Sensitivity Sensitivity { get; set; } = Sensitivity.Balanced;

    /// <summary>Match turned and mirrored copies. On by default.</summary>
    public bool IncludeRotated { get; set; } = true;

    /// <summary>Show "almost the same scene" groups for manual review. On by default.</summary>
    public bool IncludeSimilar { get; set; } = true;

    /// <summary>Where removed pictures go. The review folder by default, so every move can be undone.</summary>
    public RemoveAction Action { get; set; } = RemoveAction.ReviewFolder;

    public FindOptions ToFindOptions() => new()
    {
        Sensitivity = Sensitivity,
        IncludeRotated = IncludeRotated,
        IncludeSimilar = IncludeSimilar
    };
}

/// <summary>Loads and saves <see cref="SimilarPicturesSettings"/> in the feature's own data folder.</summary>
public sealed class SimilarPicturesSettingsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _file;

    public SimilarPicturesSettingsStore(string dataDirectory)
        => _file = Path.Combine(dataDirectory, "similar_pictures_settings.json");

    public SimilarPicturesSettings Load()
    {
        try
        {
            if (File.Exists(_file))
            {
                return JsonSerializer.Deserialize<SimilarPicturesSettings>(File.ReadAllText(_file), JsonOpts) ?? new();
            }
        }
        catch
        {
            // Ignore corrupt settings - fall back to the safe defaults.
        }
        return new();
    }

    public void Save(SimilarPicturesSettings settings)
        => File.WriteAllText(_file, JsonSerializer.Serialize(settings, JsonOpts));
}
