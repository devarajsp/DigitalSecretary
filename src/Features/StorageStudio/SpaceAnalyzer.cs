namespace DigitalSecretary.Features.StorageStudio;

/// <summary>One bar of a breakdown chart: a label, its bytes, file count and share of the total.</summary>
public sealed record BreakdownItem(string Label, long Bytes, int Files, double Share);

/// <summary>How long ago files were last changed, bucketed for the "by age" chart.</summary>
public enum AgeBucket
{
    UnderOneYear,
    OneToThreeYears,
    ThreeToFiveYears,
    OverFiveYears
}

/// <summary>
/// Pure read-only questions over a <see cref="SpaceNode"/> tree: what kinds of file use the
/// space, how old it is, and which files and folders are the heaviest. Every method works on any
/// folder of the tree, so the view can re-ask them as the user drills in.
/// </summary>
public static class SpaceAnalyzer
{
    /// <summary>Label shown for an age bucket.</summary>
    public static string Label(AgeBucket bucket) => bucket switch
    {
        AgeBucket.UnderOneYear => "Changed in the last year",
        AgeBucket.OneToThreeYears => "1 - 3 years untouched",
        AgeBucket.ThreeToFiveYears => "3 - 5 years untouched",
        _ => "Over 5 years untouched"
    };

    /// <summary>Which bucket a file modified at <paramref name="lastWriteUtc"/> falls into.</summary>
    public static AgeBucket BucketFor(DateTime lastWriteUtc, DateTime nowUtc)
    {
        var age = nowUtc - lastWriteUtc;
        if (age < TimeSpan.FromDays(365))
        {
            return AgeBucket.UnderOneYear;
        }
        if (age < TimeSpan.FromDays(365 * 3))
        {
            return AgeBucket.OneToThreeYears;
        }
        return age < TimeSpan.FromDays(365 * 5) ? AgeBucket.ThreeToFiveYears : AgeBucket.OverFiveYears;
    }

    /// <summary>
    /// Space per <see cref="FileCategory"/> under <paramref name="folder"/>, largest first, with
    /// empty categories left out. Sizes come from the folder's running totals; file counts need one
    /// walk of the subtree.
    /// </summary>
    public static IReadOnlyList<BreakdownItem> ByType(SpaceNode folder)
    {
        var counts = new int[FileTypeClassifier.CategoryCount];
        foreach (var node in Files(folder))
        {
            counts[(int)node.Category]++;
        }

        var items = new List<BreakdownItem>();
        for (var i = 0; i < FileTypeClassifier.CategoryCount; i++)
        {
            var bytes = folder.IsFolder ? folder.CategorySizes[i] : (i == (int)folder.Category ? folder.Size : 0);
            if (bytes > 0 || counts[i] > 0)
            {
                items.Add(new BreakdownItem(
                    FileTypeClassifier.Label((FileCategory)i), bytes, counts[i], Share(bytes, folder.Size)));
            }
        }
        return [.. items.OrderByDescending(i => i.Bytes).ThenBy(i => i.Label, StringComparer.Ordinal)];
    }

    /// <summary>Space per <see cref="AgeBucket"/> under <paramref name="folder"/>, in age order.</summary>
    public static IReadOnlyList<BreakdownItem> ByAge(SpaceNode folder, DateTime nowUtc)
    {
        var bytes = new long[4];
        var counts = new int[4];
        foreach (var file in Files(folder))
        {
            var bucket = (int)BucketFor(file.LastWriteUtc, nowUtc);
            bytes[bucket] += file.Size;
            counts[bucket]++;
        }

        var items = new List<BreakdownItem>();
        for (var i = 0; i < 4; i++)
        {
            items.Add(new BreakdownItem(Label((AgeBucket)i), bytes[i], counts[i], Share(bytes[i], folder.Size)));
        }
        return items;
    }

    /// <summary>The <paramref name="count"/> largest files under <paramref name="folder"/>, largest first.</summary>
    public static IReadOnlyList<SpaceNode> LargestFiles(SpaceNode folder, int count)
        => TopBySize(Files(folder), count);

    /// <summary>
    /// The <paramref name="count"/> largest folders under <paramref name="folder"/> (not counting
    /// itself), largest first. A parent and its child can both appear - each is a real place to look.
    /// </summary>
    public static IReadOnlyList<SpaceNode> LargestFolders(SpaceNode folder, int count)
        => TopBySize(folder.Descendants().Where(n => n.IsFolder), count);

    /// <summary>Every file beneath <paramref name="folder"/>, or the node itself when it is a file.</summary>
    public static IEnumerable<SpaceNode> Files(SpaceNode folder)
        => folder.IsFolder ? folder.Descendants().Where(n => !n.IsFolder) : [folder];

    /// <summary><paramref name="part"/> as a fraction of <paramref name="whole"/>; 0 when the whole is empty.</summary>
    public static double Share(long part, long whole) => whole <= 0 ? 0d : (double)part / whole;

    private static List<SpaceNode> TopBySize(IEnumerable<SpaceNode> nodes, int count)
    {
        if (count <= 0)
        {
            return [];
        }
        // A bounded min-heap keeps this O(n log count) on trees with millions of files.
        var heap = new PriorityQueue<SpaceNode, long>();
        foreach (var node in nodes)
        {
            if (heap.Count < count)
            {
                heap.Enqueue(node, node.Size);
            }
            else if (heap.TryPeek(out _, out var smallest) && node.Size > smallest)
            {
                heap.EnqueueDequeue(node, node.Size);
            }
        }

        var result = new List<SpaceNode>(heap.Count);
        while (heap.Count > 0)
        {
            result.Add(heap.Dequeue());
        }
        // Largest first; equal sizes by name so the lists do not reshuffle between runs.
        result.Sort(static (a, b) =>
        {
            var bySize = b.Size.CompareTo(a.Size);
            return bySize != 0 ? bySize : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
        return result;
    }
}
