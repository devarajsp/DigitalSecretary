using System.Collections.Concurrent;

namespace DigitalSecretary.Features.SimilarPictures;

/// <summary>How closely a picture matches the one kept in its group.</summary>
public enum MatchKind
{
    /// <summary>Byte-for-byte the same file.</summary>
    Exact,

    /// <summary>The same picture re-saved, resized, re-compressed, lightly edited, turned or mirrored.</summary>
    SamePicture,

    /// <summary>Almost the same scene - a burst or retake. Never pre-selected: the user decides.</summary>
    Similar
}

/// <summary>How strict matching is. Changing it regroups instantly from the cached fingerprints.</summary>
public enum Sensitivity
{
    Strict,
    Balanced,
    Loose
}

/// <summary>Hamming-distance limits (out of 64 bits) for one sensitivity.</summary>
public sealed record Thresholds(int SamePHash, int SameDHash, int SimilarPHash, int SimilarDHash, int BurstBonus)
{
    /// <summary>Two shots whose EXIF capture times are this close get <see cref="BurstBonus"/> extra bits of tolerance for "Similar".</summary>
    public static readonly TimeSpan BurstWindow = TimeSpan.FromSeconds(30);

    public static Thresholds For(Sensitivity sensitivity) => sensitivity switch
    {
        Sensitivity.Strict => new(6, 8, 9, 11, 3),
        Sensitivity.Loose => new(10, 14, 16, 18, 6),
        _ => new(8, 10, 12, 14, 4)
    };
}

/// <summary>What to look for.</summary>
public sealed record FindOptions
{
    public Sensitivity Sensitivity { get; init; } = Sensitivity.Balanced;

    /// <summary>Also match a picture with its turned (90/180/270) or mirrored version.</summary>
    public bool IncludeRotated { get; init; } = true;

    /// <summary>Also group "almost the same scene" shots (bursts, retakes) for manual review.</summary>
    public bool IncludeSimilar { get; init; } = true;
}

/// <summary>One picture in a group, compared with the group's keeper.</summary>
/// <param name="Suggested">Pre-marked to move: true only for exact copies and the same picture re-saved.</param>
public sealed record GroupMember(PictureRecord Picture, MatchKind Kind, PictureOrientation Orientation, int PDistance, int DDistance, bool Suggested);

/// <summary>A set of look-alike pictures: the one to keep and the others, best match first.</summary>
public sealed record SimilarGroup(int Id, PictureRecord Keeper, IReadOnlyList<GroupMember> Members)
{
    /// <summary>The loosest match in the group - what the group is labelled with.</summary>
    public MatchKind Kind => Members.Max(m => m.Kind);

    public int Count => Members.Count + 1;

    /// <summary>Bytes freed by moving every pre-marked member.</summary>
    public long SuggestedBytes => Members.Where(m => m.Suggested).Sum(m => m.Picture.Size);

    /// <summary>Bytes freed if every picture but the keeper went.</summary>
    public long AllOthersBytes => Members.Sum(m => m.Picture.Size);
}

/// <summary>
/// Groups look-alike pictures from their fingerprints. Pure apart from the optional byte comparison
/// used to confirm exact copies. Three steps:
/// <list type="number">
/// <item><b>Pairs.</b> Every pair is compared (in parallel); a pair matches when <i>both</i> hashes are
/// within the limits, trying all eight orientations when turned/mirrored copies are wanted.</item>
/// <item><b>Keeper first.</b> Pictures are ranked by <see cref="KeeperRanking"/>; the best unassigned
/// picture becomes a keeper and its unassigned matches join it ("star" grouping). Every member is
/// therefore compared with the keeper itself - a chain A~B~C cannot drag in a C that is unlike A.</item>
/// <item><b>Suggest.</b> Exact copies and the same picture re-saved are pre-marked to move; "Similar"
/// shots never are.</item>
/// </list>
/// </summary>
public static class SimilarityFinder
{
    private readonly record struct Edge(int Other, MatchKind Kind, PictureOrientation Orientation, int P, int D);

    /// <summary>
    /// Finds the groups among <paramref name="pictures"/>. <paramref name="areIdentical"/>, when given,
    /// confirms exact copies byte-for-byte; without it, a zero-distance pair of equal size is "Same picture".
    /// Groups come back with the most space to gain first.
    /// </summary>
    public static IReadOnlyList<SimilarGroup> Find(
        IReadOnlyList<PictureRecord> pictures, FindOptions options, Func<string, string, bool>? areIdentical = null,
        CancellationToken cancel = default)
    {
        var limits = Thresholds.For(options.Sensitivity);
        var edges = FindEdges(pictures, options, limits, areIdentical, cancel);
        var order = Enumerable.Range(0, pictures.Count)
            .OrderBy(i => pictures[i], KeeperRanking.Instance).ToList();

        var assigned = new bool[pictures.Count];
        var groups = new List<SimilarGroup>();
        foreach (var seed in order)
        {
            if (assigned[seed] || !edges.TryGetValue(seed, out var list))
            {
                continue;
            }
            var members = list.Where(e => !assigned[e.Other])
                .OrderBy(e => e.Kind).ThenBy(e => e.P + e.D).ThenBy(e => pictures[e.Other].Path, StringComparer.OrdinalIgnoreCase)
                .Select(e => new GroupMember(pictures[e.Other], e.Kind, e.Orientation, e.P, e.D, e.Kind != MatchKind.Similar))
                .ToList();
            if (members.Count == 0)
            {
                continue;
            }
            assigned[seed] = true;
            foreach (var m in list.Where(e => !assigned[e.Other]))
            {
                assigned[m.Other] = true;
            }
            groups.Add(new SimilarGroup(0, pictures[seed], members));
        }

        return [.. groups
            .OrderByDescending(g => g.SuggestedBytes).ThenByDescending(g => g.AllOthersBytes)
            .ThenBy(g => g.Keeper.Path, StringComparer.OrdinalIgnoreCase)
            .Select((g, i) => g with { Id = i + 1 })];
    }

    /// <summary>
    /// How <paramref name="b"/> matches <paramref name="a"/>, or null. Distances are taken at the
    /// orientation of <paramref name="b"/> that brings it closest to <paramref name="a"/> as it is.
    /// </summary>
    public static (MatchKind Kind, PictureOrientation Orientation, int P, int D)? Compare(
        PictureRecord a, PictureRecord b, FindOptions options, Thresholds limits)
    {
        // Start above the largest possible distance (64 + 64) so the first orientation always wins.
        var best = (P: 65, D: 65, O: PictureOrientation.AsIs);
        var variants = options.IncludeRotated ? b.PHashes.Length : 1;
        for (var k = 0; k < variants; k++)
        {
            var p = ImageHasher.Distance(a.PHash, b.PHashes[k]);
            var d = ImageHasher.Distance(a.DHash, b.DHashes[k]);
            if (p + d < best.P + best.D)
            {
                best = (p, d, (PictureOrientation)k);
            }
        }

        // Burst frames can be as close as a re-saved copy. If the capture times say they were taken at
        // different moments they are different shots, so they are only ever "Similar - check".
        if (best.P <= limits.SamePHash && best.D <= limits.SameDHash && !DifferentMoments(a, b))
        {
            return (MatchKind.SamePicture, best.O, best.P, best.D);
        }
        if (!options.IncludeSimilar)
        {
            return null;
        }

        // "Similar" is judged as-is only. Turning is for copies (a turned copy matches at ~0 bits);
        // searching eight orientations at the looser limits would multiply false matches by eight.
        var p0 = ImageHasher.Distance(a.PHash, b.PHash);
        var d0 = ImageHasher.Distance(a.DHash, b.DHash);
        var bonus = a.TakenUtc is { } ta && b.TakenUtc is { } tb && (ta - tb).Duration() <= Thresholds.BurstWindow ? limits.BurstBonus : 0;
        return p0 <= limits.SimilarPHash + bonus && d0 <= limits.SimilarDHash + bonus
            ? (MatchKind.Similar, PictureOrientation.AsIs, p0, d0)
            : null;
    }

    /// <summary>
    /// Both pictures carry an EXIF capture time (with sub-seconds when the camera records them) and the
    /// times differ - two shots, not one picture saved twice. Copies keep the original's EXIF time;
    /// apps that strip EXIF leave no time, which never counts as "different".
    /// </summary>
    public static bool DifferentMoments(PictureRecord a, PictureRecord b)
        => a.TakenUtc is { } ta && b.TakenUtc is { } tb && (ta - tb).Duration() > TimeSpan.FromMilliseconds(5);

    private static Dictionary<int, List<Edge>> FindEdges(
        IReadOnlyList<PictureRecord> pictures, FindOptions options, Thresholds limits,
        Func<string, string, bool>? areIdentical, CancellationToken cancel)
    {
        var found = new ConcurrentBag<(int A, Edge Edge)>();
        Parallel.For(0, pictures.Count, new ParallelOptions { CancellationToken = cancel }, i =>
        {
            for (var j = i + 1; j < pictures.Count; j++)
            {
                if (Compare(pictures[i], pictures[j], options, limits) is not { } m)
                {
                    continue;
                }
                var kind = m.Kind;
                if (kind == MatchKind.SamePicture && m.P == 0 && m.D == 0 && m.Orientation == PictureOrientation.AsIs
                    && pictures[i].Size == pictures[j].Size && areIdentical is not null
                    && areIdentical(pictures[i].Path, pictures[j].Path))
                {
                    kind = MatchKind.Exact;
                }
                found.Add((i, new Edge(j, kind, m.Orientation, m.P, m.D)));
                found.Add((j, new Edge(i, kind, Inverse(m.Orientation), m.P, m.D)));
            }
        });

        var edges = new Dictionary<int, List<Edge>>();
        foreach (var (a, edge) in found)
        {
            (edges.TryGetValue(a, out var list) ? list : edges[a] = []).Add(edge);
        }
        return edges;
    }

    /// <summary>The orientation that undoes <paramref name="o"/> (only the quarter turns differ).</summary>
    public static PictureOrientation Inverse(PictureOrientation o) => o switch
    {
        PictureOrientation.Rotate90 => PictureOrientation.Rotate270,
        PictureOrientation.Rotate270 => PictureOrientation.Rotate90,
        _ => o
    };
}
