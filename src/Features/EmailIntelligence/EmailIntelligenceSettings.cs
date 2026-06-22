using System.Text.Json;

namespace DigitalSecretary.Features.EmailIntelligence;

/// <summary>What to do with results from a previous run on a different archive.</summary>
public enum AnalysisMode
{
    /// <summary>Replace previous results using only the current input.</summary>
    Overwrite,

    /// <summary>Merge the new archive with previously-analysed messages and re-de-duplicate.</summary>
    Append,
}

/// <summary>Inputs for one analysis run.</summary>
public sealed class EmailIntelligenceOptions
{
    public string InputDir { get; set; } = "";
    public string OutputDir { get; set; } = "";

    /// <summary>The owner's address. Blank => auto-detect from the most frequent sender.</summary>
    public string OwnerAddress { get; set; } = "";

    public int DormantMonths { get; set; } = 12;

    /// <summary>Overwrite (current input only) or Append (consolidate with prior runs).</summary>
    public AnalysisMode Mode { get; set; } = AnalysisMode.Overwrite;

    /// <summary>Test seam for "now"; null uses the wall clock.</summary>
    public DateTimeOffset? Now { get; set; }
}

/// <summary>Remembered, non-sensitive settings for the feature.</summary>
public sealed class EmailIntelligenceSettings
{
    public string InputDir { get; set; } = "";
    public string OutputDir { get; set; } = "";
    public string OwnerAddress { get; set; } = "";

    /// <summary>Remember whether the user last chose to append (consolidate) rather than overwrite.</summary>
    public bool Append { get; set; }
}

/// <summary>Loads/saves <see cref="EmailIntelligenceSettings"/> in the feature's own data folder.</summary>
public sealed class EmailIntelligenceSettingsStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly string _file;

    public EmailIntelligenceSettingsStore(string dataDirectory) =>
        _file = Path.Combine(dataDirectory, "email_intelligence_settings.json");

    public EmailIntelligenceSettings Load()
    {
        try
        {
            if (File.Exists(_file))
                return JsonSerializer.Deserialize<EmailIntelligenceSettings>(File.ReadAllText(_file)) ?? new();
        }
        catch
        {
            // Ignore corrupt settings.
        }
        return new();
    }

    public void Save(EmailIntelligenceSettings settings) =>
        File.WriteAllText(_file, JsonSerializer.Serialize(settings, JsonOpts));
}
