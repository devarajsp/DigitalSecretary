using System.Collections.Concurrent;

namespace DigitalSecretary.Features.SimilarVideos;

/// <summary>How closely a video matches the one kept in its group.</summary>
public enum VideoMatchKind
{
    /// <summary>Byte-for-byte the same file.</summary>
    Exact,

    /// <summary>The same video re-encoded, resized, re-compressed, turned or mirrored.</summary>
    SameVideo,

    /// <summary>The same scene with differences (another take, a light edit). Never pre-selected: the user decides.</summary>
    Similar
}

/// <summary>How strict matching is. Changing it regroups instantly from the cached fingerprints.</summary>
public enum VideoSensitivity
{
    Strict,
    Balanced,
    Loose
}

/// <summary>
/// Limits for one sensitivity. A frame "matches" when its pHash distance (out of 64 bits) is within the
/// per-frame limit; the share of compared frames that must match, and the most the <i>mean</i> dHash
/// distance over those frames may be, decide the pair. Measured on real H.264 round trips (resize to half,
/// 4x lower bit rate, coded padding): the same video stays at pHash 0 - 2 per frame and mean dHash 12 - 14
/// (dHash jumps on smooth gradients under compression, so it is judged on the mean, never per frame);
/// different videos sit at pHash 20 - 36 and mean dHash 24+.
/// </summary>
public sealed record VideoThresholds(int SameP, int SameMeanD, double SameShare, int SimilarP, int SimilarMeanD, double SimilarShare)
{
    /// <summary>At least this many frames must be comparable (decoded and not flat in both videos) to call two videos alike.</summary>
    public const int MinComparableFrames = 4;

    public static VideoThresholds For(VideoSensitivity sensitivity) => sensitivity switch
    {
        VideoSensitivity.Strict => new(6, 14, 0.9, 10, 18, 0.7),
        VideoSensitivity.Loose => new(10, 18, 0.7, 16, 22, 0.5),
        _ => new(8, 16, 0.8, 12, 20, 0.6)
    };

    /// <summary>
    /// Two videos can only be the same when their lengths agree within 2% of the longer one or one
    /// second, whichever is more (containers round lengths differently; re-encodes add or drop a frame).
    /// </summary>
    public static long DurationToleranceMs(long longerMs) => Math.Max(1000, longerMs / 50);
}

/// <summary>What to look for.</summary>
public sealed record VideoFindOptions
{
    public VideoSensitivity Sensitivity { get; init; } = VideoSensitivity.Balanced;

    /// <summary>Also match a video with its turned (90/180/270) or mirrored version.</summary>
    public bool IncludeRotated { get; init; } = true;

    /// <summary>Also group "same scene, some differences" videos for manual review.</summary>
    public bool IncludeSimilar { get; init; } = true;
}

/// <summary>How <c>b</c> matches <c>a</c>: the kind, the orientation that brings <c>b</c> closest, the frame tally and the mean pHash distance.</summary>
public readonly record struct VideoMatch(VideoMatchKind Kind, PictureOrientation Orientation, int MatchedFrames, int ComparedFrames, double MeanDistance);

/// <summary>One video in a group, compared with the group's keeper.</summary>
/// <param name="Suggested">Pre-marked to move: true only for exact copies and the same video re-encoded.</param>
public sealed record VideoMember(VideoRecord Video, VideoMatch Match, bool Suggested)
{
    public VideoMatchKind Kind => Match.Kind;
}

/// <summary>A set of matching videos: the one to keep and the others, best match first.</summary>
public sealed record VideoGroup(int Id, VideoRecord Keeper, IReadOnlyList<VideoMember> Members)
{
    /// <summary>The loosest match in the group - what the group is labelled with.</summary>
    public VideoMatchKind Kind => Members.Max(m => m.Kind);

    public int Count => Members.Count + 1;

    /// <summary>Bytes freed by moving every pre-marked member.</summary>
    public long SuggestedBytes => Members.Where(m => m.Suggested).Sum(m => m.Video.Size);

    /// <summary>Bytes freed if every video but the keeper went.</summary>
    public long AllOthersBytes => Members.Sum(m => m.Video.Size);
}

/// <summary>
/// Groups matching videos from their fingerprints. Pure apart from the optional byte comparison used to
/// confirm exact copies. Steps:
/// <list type="number">
/// <item><b>Length first.</b> Videos are sorted by length and only pairs whose lengths agree
/// (<see cref="VideoThresholds.DurationToleranceMs"/>) are compared - most pairs are never looked at.</item>
/// <item><b>Frames in order.</b> Frame <i>i</i> of one video is compared with frame <i>i</i> of the other
/// (both sampled at the same fraction of the length). A pair is "Same video" when enough frames match at the
/// tight limits in one orientation, "Similar - check" when enough match as-is at the looser limits.</item>
/// <item><b>Keeper first.</b> Star grouping by <see cref="VideoKeeperRanking"/>, as in Similar Pictures:
/// every member is compared with the keeper itself, so A~B~C cannot drag in a C that is unlike A.</item>
/// </list>
/// </summary>
public static class VideoMatcher
{
    private readonly record struct Edge(int Other, VideoMatch Match);

    /// <summary>
    /// Finds the groups among <paramref name="videos"/>. <paramref name="areIdentical"/>, when given, confirms
    /// exact copies byte-for-byte (only asked for pairs of equal size). Groups come back with the most space to gain first.
    /// </summary>
    public static IReadOnlyList<VideoGroup> Find(
        IReadOnlyList<VideoRecord> videos, VideoFindOptions options, Func<string, string, bool>? areIdentical = null,
        CancellationToken cancel = default)
    {
        var edges = FindEdges(videos, options, areIdentical, cancel);
        var order = Enumerable.Range(0, videos.Count).OrderBy(i => videos[i], VideoKeeperRanking.Instance).ToList();

        var assigned = new bool[videos.Count];
        var groups = new List<VideoGroup>();
        foreach (var seed in order)
        {
            if (assigned[seed] || !edges.TryGetValue(seed, out var list))
            {
                continue;
            }
            var open = list.Where(e => !assigned[e.Other]).ToList();
            if (open.Count == 0)
            {
                continue;
            }
            assigned[seed] = true;
            open.ForEach(e => assigned[e.Other] = true);
            var members = open
                .OrderBy(e => e.Match.Kind).ThenBy(e => e.Match.MeanDistance).ThenBy(e => videos[e.Other].Path, StringComparer.OrdinalIgnoreCase)
                .Select(e => new VideoMember(videos[e.Other], e.Match, e.Match.Kind != VideoMatchKind.Similar))
                .ToList();
            groups.Add(new VideoGroup(0, videos[seed], members));
        }

        return [.. groups
            .OrderByDescending(g => g.SuggestedBytes).ThenByDescending(g => g.AllOthersBytes)
            .ThenBy(g => g.Keeper.Path, StringComparer.OrdinalIgnoreCase)
            .Select((g, i) => g with { Id = i + 1 })];
    }

    /// <summary>True when the two lengths are close enough for the videos to possibly be the same.</summary>
    public static bool DurationsAgree(VideoRecord a, VideoRecord b)
    {
        var longer = Math.Max(a.DurationMs, b.DurationMs);
        return Math.Abs(a.DurationMs - b.DurationMs) <= VideoThresholds.DurationToleranceMs(longer);
    }

    /// <summary>
    /// How <paramref name="b"/> matches <paramref name="a"/> by their frames, or null. Does not look at the
    /// lengths (see <see cref="DurationsAgree"/>) or the bytes (exact copies are found in <see cref="Find"/>).
    /// </summary>
    public static VideoMatch? Compare(VideoRecord a, VideoRecord b, VideoFindOptions options, VideoThresholds limits)
    {
        var comparable = Enumerable.Range(0, Math.Min(a.Frames.Length, b.Frames.Length))
            .Where(i => a.Frames[i] is not null && b.Frames[i] is not null).ToList();
        if (comparable.Count < VideoThresholds.MinComparableFrames)
        {
            return null;
        }

        var best = (T: new FrameTally(-1, double.MaxValue, double.MaxValue), O: PictureOrientation.AsIs);
        var variants = options.IncludeRotated ? 8 : 1;
        for (var k = 0; k < variants; k++)
        {
            var t = Tally(a, b, comparable, k, limits.SameP);
            if (t.Matched > best.T.Matched || (t.Matched == best.T.Matched && t.MeanP < best.T.MeanP))
            {
                best = (t, (PictureOrientation)k);
            }
        }
        if (Passes(best.T, comparable.Count, limits.SameShare, limits.SameMeanD))
        {
            return new VideoMatch(VideoMatchKind.SameVideo, best.O, best.T.Matched, comparable.Count, best.T.MeanP);
        }
        if (!options.IncludeSimilar)
        {
            return null;
        }

        // "Similar" is judged as-is only: turning is for copies, and searching eight orientations at the
        // looser limits would multiply false matches by eight (the same rule as Similar Pictures).
        var loose = Tally(a, b, comparable, 0, limits.SimilarP);
        return Passes(loose, comparable.Count, limits.SimilarShare, limits.SimilarMeanD)
            ? new VideoMatch(VideoMatchKind.Similar, PictureOrientation.AsIs, loose.Matched, comparable.Count, loose.MeanP)
            : null;
    }

    /// <summary>The orientation that undoes <paramref name="o"/>.</summary>
    public static PictureOrientation Inverse(PictureOrientation o) => Orientations.Inverse(o);

    /// <summary>Frames whose pHash is within the limit, with the mean pHash and dHash distances over all compared frames.</summary>
    private readonly record struct FrameTally(int Matched, double MeanP, double MeanD);

    private static bool Passes(FrameTally t, int compared, double share, int maxMeanD)
        => t.Matched >= Math.Ceiling(share * compared) && t.MeanD <= maxMeanD;

    /// <summary>Compares frame i of <paramref name="a"/> as it is with frame i of <paramref name="b"/> at orientation <paramref name="k"/>.</summary>
    private static FrameTally Tally(VideoRecord a, VideoRecord b, List<int> frames, int k, int maxP)
    {
        var matched = 0;
        double totalP = 0, totalD = 0;
        foreach (var i in frames)
        {
            var fa = a.Frames[i]!;
            var fb = b.Frames[i]!;
            var p = ImageHasher.Distance(fa.P[0], fb.P[k]);
            totalP += p;
            totalD += ImageHasher.Distance(fa.D[0], fb.D[k]);
            if (p <= maxP)
            {
                matched++;
            }
        }
        return new FrameTally(matched, totalP / frames.Count, totalD / frames.Count);
    }

    private static Dictionary<int, List<Edge>> FindEdges(
        IReadOnlyList<VideoRecord> videos, VideoFindOptions options, Func<string, string, bool>? areIdentical, CancellationToken cancel)
    {
        var limits = VideoThresholds.For(options.Sensitivity);
        var byLength = Enumerable.Range(0, videos.Count).OrderBy(i => videos[i].DurationMs).ToArray();
        var found = new ConcurrentBag<(int A, Edge Edge)>();
        Parallel.For(0, byLength.Length, new ParallelOptions { CancellationToken = cancel }, n =>
        {
            var i = byLength[n];
            for (var m = n + 1; m < byLength.Length && DurationsAgree(videos[i], videos[byLength[m]]); m++)
            {
                var j = byLength[m];
                if (Pair(videos[i], videos[j], options, limits, areIdentical) is { } match)
                {
                    found.Add((i, new Edge(j, match)));
                    found.Add((j, new Edge(i, match with { Orientation = Inverse(match.Orientation) })));
                }
            }
        });

        var edges = new Dictionary<int, List<Edge>>();
        foreach (var (a, edge) in found)
        {
            (edges.TryGetValue(a, out var list) ? list : edges[a] = []).Add(edge);
        }
        return edges;
    }

    /// <summary>One pair: an exact copy (same size, same bytes) wins; otherwise the frames decide.</summary>
    private static VideoMatch? Pair(VideoRecord a, VideoRecord b, VideoFindOptions options, VideoThresholds limits, Func<string, string, bool>? areIdentical)
    {
        if (a.Size == b.Size && areIdentical is not null && areIdentical(a.Path, b.Path))
        {
            var frames = Math.Min(a.UsableFrames, b.UsableFrames);
            return new VideoMatch(VideoMatchKind.Exact, PictureOrientation.AsIs, frames, frames, 0);
        }
        return Compare(a, b, options, limits);
    }
}
