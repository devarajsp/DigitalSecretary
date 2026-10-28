namespace DigitalSecretary.Features.DeDupeFiles;

/// <summary>
/// The checksum list held <b>in memory</b> for the whole run. The list file is read once at the
/// start and written once at the end (plus periodic autosaves), and every lookup, update and
/// duplicate grouping in between happens here - so a re-run costs one file read instead of one
/// read per file.
/// </summary>
public sealed class ChecksumIndex
{
    private readonly Dictionary<string, ChecksumEntry> _byPath = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>How many files the index currently knows about.</summary>
    public int Count => _byPath.Count;

    /// <summary>Every entry, in no particular order.</summary>
    public IReadOnlyCollection<ChecksumEntry> Entries => _byPath.Values;

    /// <summary>Total size of every file in the index.</summary>
    public long TotalBytes => _byPath.Values.Sum(e => e.BillableLength);

    /// <summary>Builds an index from list-file entries; a later duplicate path wins.</summary>
    public static ChecksumIndex FromEntries(IEnumerable<ChecksumEntry> entries)
    {
        var index = new ChecksumIndex();
        foreach (var entry in entries)
        {
            index.Put(entry);
        }
        return index;
    }

    /// <summary>The entry for a path, or <c>null</c> when the file has not been hashed yet.</summary>
    public ChecksumEntry? Find(string fullPath)
        => _byPath.TryGetValue(fullPath, out var entry) ? entry : null;

    /// <summary>Adds or replaces the entry for its path.</summary>
    public void Put(ChecksumEntry entry) => _byPath[entry.FullPath] = entry;

    /// <summary>Drops a path from the index. True when something was removed.</summary>
    public bool Remove(string fullPath) => _byPath.Remove(fullPath);

    /// <summary>
    /// Prunes entries whose file was not seen during the scan - files the user has since deleted,
    /// moved or renamed - and returns the paths that were dropped.
    /// </summary>
    public IReadOnlyList<string> RetainOnly(IReadOnlySet<string> livePaths)
    {
        var stale = _byPath.Keys.Where(p => !livePaths.Contains(p)).ToList();
        foreach (var path in stale)
        {
            _byPath.Remove(path);
        }
        return stale;
    }

    /// <summary>
    /// Groups the entries by checksum - the basis of duplicate detection. Each group's files are
    /// ordered by path so results are stable from run to run.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<ChecksumEntry>> GroupByHash()
        => _byPath.Values
            .GroupBy(e => e.Md5, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ChecksumEntry>)g
                    .OrderBy(e => e.FullPath, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                StringComparer.Ordinal);
}
