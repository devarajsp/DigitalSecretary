namespace DigitalSecretary.Features.EmailIntelligence;

/// <summary>
/// Finds the email files to analyse under an archive folder. When a <c>.txt</c> and a <c>.eml</c>
/// share the same base path (the pair the downloaders write), only the higher-fidelity <c>.eml</c>
/// is kept, so the same message is not counted twice.
/// </summary>
public sealed class ArchiveScanner
{
    private static readonly HashSet<string> Extensions =
        new(new[] { ".eml", ".txt" }, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Scan(string rootDir)
    {
        if (string.IsNullOrWhiteSpace(rootDir) || !Directory.Exists(rootDir))
            return Array.Empty<string>();

        var all = Directory
            .EnumerateFiles(rootDir, "*.*", SearchOption.AllDirectories)
            .Where(f => Extensions.Contains(Path.GetExtension(f)))
            .ToList();

        return Filter(all);
    }

    /// <summary>Applies the .eml-preferred-over-.txt-sibling rule (separated for unit testing).</summary>
    public static IReadOnlyList<string> Filter(IEnumerable<string> files)
    {
        var list = files.ToList();
        var emlBases = new HashSet<string>(
            list.Where(IsEml).Select(BaseKey), StringComparer.OrdinalIgnoreCase);

        return list
            .Where(f => IsEml(f) || !emlBases.Contains(BaseKey(f)))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsEml(string path) =>
        Path.GetExtension(path).Equals(".eml", StringComparison.OrdinalIgnoreCase);

    private static string BaseKey(string path) =>
        Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path));
}
