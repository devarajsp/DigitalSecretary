using System.Text.Json;
using System.Text.Json.Serialization;

namespace DigitalSecretary.Features.DeDupeFiles;

/// <summary>What to do when the root folder already holds a checksum list.</summary>
public enum ExistingListAction
{
    /// <summary>Ask each time (the default).</summary>
    Ask,

    /// <summary>Rescan, reusing checksums for files that have not changed.</summary>
    Update,

    /// <summary>Trust the list as it stands and skip scanning entirely.</summary>
    UseAsIs
}

/// <summary>Which copy of a duplicate group is kept.</summary>
public enum KeeperPolicy
{
    /// <summary>The copy nearest the root (fewest folders, then shortest path).</summary>
    ShortestPath,

    /// <summary>The copy with the earliest modified time - usually the original.</summary>
    OldestModified,

    /// <summary>The copy with the latest modified time.</summary>
    NewestModified,

    /// <summary>The first copy in alphabetical path order.</summary>
    FirstAlphabetical
}

/// <summary>
/// Remembered, non-sensitive settings for the feature. Nothing here is a secret: a folder path and
/// the user's choice of options.
/// </summary>
public sealed class DeDupeSettings
{
    /// <summary>The folder that was last scanned. Empty until the user picks one.</summary>
    public string Root { get; set; } = "";

    /// <summary>What to do when a checksum list already exists in the root.</summary>
    public ExistingListAction OnExistingList { get; set; } = ExistingListAction.Ask;

    /// <summary>Which copy of each duplicate group to keep.</summary>
    public KeeperPolicy Keeper { get; set; } = KeeperPolicy.ShortestPath;

    /// <summary>Leave zero-byte files alone (they all share one checksum). On by default.</summary>
    public bool IgnoreEmptyFiles { get; set; } = true;

    /// <summary>Compare byte-for-byte against the kept copy before deleting. On by default.</summary>
    public bool VerifyBeforeDelete { get; set; } = true;

    /// <summary>Send duplicates to the Recycle Bin instead of deleting them outright. On by default.</summary>
    public bool RecycleBin { get; set; } = true;

    /// <summary>Report only - delete nothing. On by default, so the first run is always safe.</summary>
    public bool DryRun { get; set; } = true;
}

/// <summary>Loads and saves <see cref="DeDupeSettings"/> in the feature's own data folder.</summary>
public sealed class DeDupeSettingsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _file;

    public DeDupeSettingsStore(string dataDirectory)
        => _file = Path.Combine(dataDirectory, "dedupe_settings.json");

    public DeDupeSettings Load()
    {
        try
        {
            if (File.Exists(_file))
            {
                return JsonSerializer.Deserialize<DeDupeSettings>(File.ReadAllText(_file), JsonOpts) ?? new();
            }
        }
        catch
        {
            // Ignore corrupt settings - fall back to the safe defaults.
        }
        return new();
    }

    public void Save(DeDupeSettings settings)
        => File.WriteAllText(_file, JsonSerializer.Serialize(settings, JsonOpts));
}
