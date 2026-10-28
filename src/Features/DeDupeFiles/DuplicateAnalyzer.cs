namespace DigitalSecretary.Features.DeDupeFiles;

/// <summary>One copy of a file inside a duplicate group.</summary>
public sealed record DuplicateFile(string Path, long Length, DateTime ModifiedUtc);

/// <summary>
/// A set of files that share a checksum: the one copy that will be <see cref="Keeper"/> and the
/// <see cref="Duplicates"/> that can be removed.
/// </summary>
public sealed record DuplicateGroup(
    string Md5, long Length, DuplicateFile Keeper, IReadOnlyList<DuplicateFile> Duplicates)
{
    /// <summary>How many copies of this content exist, the kept one included.</summary>
    public int Copies => Duplicates.Count + 1;

    /// <summary>Space freed by deleting the duplicates and keeping one copy.</summary>
    public long Reclaimable => Duplicates.Sum(d => d.Length);

    /// <summary>Space all copies of this content currently occupy.</summary>
    public long OccupiedBytes => Reclaimable + Keeper.Length;
}

/// <summary>The outcome of an analysis - what is duplicated and what deleting would save.</summary>
/// <param name="Groups">Duplicate groups, largest saving first.</param>
/// <param name="TotalFiles">Files in the checksum index.</param>
/// <param name="TotalBytes">Size of every file in the index.</param>
/// <param name="DuplicateFiles">Removable copies (every group's copies bar one).</param>
/// <param name="DuplicateBytes">Space occupied by all copies in duplicate groups.</param>
/// <param name="ReclaimableBytes">Space that deleting the duplicates would free.</param>
/// <param name="IgnoredEmptyFiles">Zero-byte files left alone.</param>
public sealed record DuplicateReport(
    IReadOnlyList<DuplicateGroup> Groups, int TotalFiles, long TotalBytes,
    int DuplicateFiles, long DuplicateBytes, long ReclaimableBytes, int IgnoredEmptyFiles)
{
    /// <summary>How many sets of duplicates were found.</summary>
    public int GroupCount => Groups.Count;

    /// <summary>True when there is nothing to clean up.</summary>
    public bool IsClean => Groups.Count == 0;
}

/// <summary>
/// Turns the checksum index into duplicate groups. Pure: it reads no files and deletes nothing, so
/// the numbers shown to the user can be checked by a test rather than by running on real data.
/// </summary>
public static class DuplicateAnalyzer
{
    /// <summary>Groups files by checksum and works out what could be reclaimed.</summary>
    public static DuplicateReport Analyze(ChecksumIndex index, KeeperPolicy policy, bool ignoreEmptyFiles)
    {
        var groups = new List<DuplicateGroup>();
        var ignoredEmpty = 0;

        foreach (var pair in index.GroupByHash())
        {
            var copies = pair.Value;
            if (copies.Count < 2)
            {
                continue;
            }
            if (ignoreEmptyFiles && copies.All(c => c.BillableLength == 0))
            {
                ignoredEmpty += copies.Count;
                continue;
            }

            var files = copies
                .Select(c => new DuplicateFile(c.FullPath, c.BillableLength, c.ModifiedUtc))
                .ToList();
            var keeper = PickKeeper(files, policy);
            var duplicates = files
                .Where(f => !string.Equals(f.Path, keeper.Path, StringComparison.OrdinalIgnoreCase))
                .ToList();

            groups.Add(new DuplicateGroup(pair.Key, keeper.Length, keeper, duplicates));
        }

        groups.Sort(static (a, b) => b.Reclaimable != a.Reclaimable
            ? b.Reclaimable.CompareTo(a.Reclaimable)
            : string.CompareOrdinal(a.Md5, b.Md5));

        return new DuplicateReport(
            groups,
            index.Count,
            index.TotalBytes,
            groups.Sum(g => g.Duplicates.Count),
            groups.Sum(g => g.OccupiedBytes),
            groups.Sum(g => g.Reclaimable),
            ignoredEmpty);
    }

    /// <summary>Convenience overload for callers holding plain list entries.</summary>
    public static DuplicateReport Analyze(
        IEnumerable<ChecksumEntry> entries, KeeperPolicy policy, bool ignoreEmptyFiles)
        => Analyze(ChecksumIndex.FromEntries(entries), policy, ignoreEmptyFiles);

    /// <summary>
    /// Chooses the copy to keep. Every policy ends with a path tie-break, so the same folder always
    /// produces the same answer - a user who re-runs a scan must not get a different survivor.
    /// </summary>
    public static DuplicateFile PickKeeper(IReadOnlyList<DuplicateFile> files, KeeperPolicy policy)
    {
        if (files.Count == 0)
        {
            throw new ArgumentException("A duplicate group needs at least one file.", nameof(files));
        }

        var ordered = policy switch
        {
            KeeperPolicy.OldestModified => files
                .OrderBy(f => f.ModifiedUtc)
                .ThenBy(f => f.Path, StringComparer.OrdinalIgnoreCase),
            KeeperPolicy.NewestModified => files
                .OrderByDescending(f => f.ModifiedUtc)
                .ThenBy(f => f.Path, StringComparer.OrdinalIgnoreCase),
            KeeperPolicy.FirstAlphabetical => files
                .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                .ThenBy(f => f.Path.Length),
            _ => files
                .OrderBy(Depth)
                .ThenBy(f => f.Path.Length)
                .ThenBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
        };

        return ordered.First();
    }

    private static int Depth(DuplicateFile file)
        => file.Path.Count(c => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar);
}
