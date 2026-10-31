namespace DigitalSecretary.Features.SimilarPictures;

/// <summary>
/// Orders pictures best-first to decide which one of a group is kept. Pure and deterministic. In order:
/// <list type="number">
/// <item>more pixels (when they differ by over 5%) - a resized copy is worse than the original;</item>
/// <item>sharper (when over 15% sharper) - in a burst, the shot that is not blurred;</item>
/// <item>bigger file - usually less compression;</item>
/// <item>a name that does not look like a copy ("(1)", "- Copy", "_copy");</item>
/// <item>earlier capture/modified time - usually the original;</item>
/// <item>shorter path, then alphabetical - so ties are always broken the same way.</item>
/// </list>
/// </summary>
public sealed class KeeperRanking : IComparer<PictureRecord>
{
    public static readonly KeeperRanking Instance = new();

    private KeeperRanking()
    {
    }

    public int Compare(PictureRecord? x, PictureRecord? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }
        if (x is null || y is null)
        {
            return x is null ? 1 : -1;
        }

        var steps = new Func<int>[]
        {
            () => Better(x.Pixels, y.Pixels, 0.05),
            () => Better(x.Sharpness, y.Sharpness, 0.15),
            () => y.Size.CompareTo(x.Size),
            () => LooksLikeCopy(x.Path).CompareTo(LooksLikeCopy(y.Path)),
            () => x.BestTimeUtc.CompareTo(y.BestTimeUtc),
            () => x.Path.Length.CompareTo(y.Path.Length),
            () => string.Compare(x.Path, y.Path, StringComparison.OrdinalIgnoreCase)
        };
        foreach (var step in steps)
        {
            var result = step();
            if (result != 0)
            {
                return result;
            }
        }
        return 0;
    }

    /// <summary>True when a file name looks like a duplicate made by copying ("IMG_1 (2).jpg", "x - Copy.jpg").</summary>
    public static bool LooksLikeCopy(string path) => FileNames.LooksLikeCopy(path);

    private static int Better(double a, double b, double margin) => Ranking.PreferLarger(a, b, margin);
}
