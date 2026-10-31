namespace DigitalSecretary.Features.SimilarVideos;

/// <summary>
/// Orders videos best-first to decide which one of a group is kept. Pure and deterministic. In order:
/// <list type="number">
/// <item>more pixels (when they differ by over 5%) - a resized copy is worse than the original;</item>
/// <item>higher bit rate (when over 10% higher) - less compression at the same size;</item>
/// <item>longer (when over 2% longer) - a copy with a clipped end is worse;</item>
/// <item>bigger file;</item>
/// <item>a name that does not look like a copy ("(1)", "- Copy", "_copy");</item>
/// <item>earlier modified time - usually the original;</item>
/// <item>shorter path, then alphabetical - so ties are always broken the same way.</item>
/// </list>
/// </summary>
public sealed class VideoKeeperRanking : IComparer<VideoRecord>
{
    public static readonly VideoKeeperRanking Instance = new();

    private VideoKeeperRanking()
    {
    }

    public int Compare(VideoRecord? x, VideoRecord? y)
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
            () => Ranking.PreferLarger(x.Pixels, y.Pixels, 0.05),
            () => Ranking.PreferLarger(x.BitrateKbps, y.BitrateKbps, 0.10),
            () => Ranking.PreferLarger(x.DurationMs, y.DurationMs, 0.02),
            () => y.Size.CompareTo(x.Size),
            () => FileNames.LooksLikeCopy(x.Path).CompareTo(FileNames.LooksLikeCopy(y.Path)),
            () => x.ModifiedUtc.CompareTo(y.ModifiedUtc),
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
}
