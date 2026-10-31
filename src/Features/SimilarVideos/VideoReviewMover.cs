namespace DigitalSecretary.Features.SimilarVideos;

/// <summary>Everything one removal pass needs.</summary>
public sealed class VideoRemoveRequest
{
    public required string Root { get; init; }
    public required IReadOnlyList<VideoGroup> Groups { get; init; }

    /// <summary>The paths the user marked. Keepers in this set are refused - one video of every group always stays.</summary>
    public required IReadOnlySet<string> Marked { get; init; }

    public RemoveAction Action { get; init; } = RemoveAction.ReviewFolder;

    /// <summary>Sends a file to the Recycle Bin; replaceable so tests never touch the real one.</summary>
    public Action<string> Recycle { get; init; } = ReviewFolder.SendToRecycleBin;

    public IProgress<string>? Log { get; init; }
    public PauseToken? Pause { get; init; }
    public CancellationToken Cancel { get; init; }
}

/// <summary>
/// Moves the videos the user marked out of the way and can undo the last move. The rule that is this
/// feature's own - a group's keeper is never removed - lives in <see cref="Plan"/>; the moving, the
/// change-since-scan guard, name clashes and undo are the kit's <see cref="ReviewFolder"/>.
/// </summary>
public static class VideoReviewMover
{
    /// <summary>Name of the review folder created inside the scanned folder.</summary>
    public const string ReviewFolderName = "_Similar videos - review";

    public static string ReviewFolderFor(string root) => ReviewFolder.For(root, ReviewFolderName);

    /// <summary>
    /// The members to remove: marked, not a keeper, de-duplicated, in group order. Pure - this is the rule
    /// that guarantees at least one video of every group survives.
    /// </summary>
    public static IReadOnlyList<VideoRecord> Plan(IReadOnlyList<VideoGroup> groups, IReadOnlySet<string> marked)
    {
        var keepers = groups.Select(g => g.Keeper.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return [.. groups.SelectMany(g => g.Members).Select(m => m.Video)
            .Where(v => marked.Contains(v.Path) && !keepers.Contains(v.Path) && seen.Add(v.Path))];
    }

    /// <summary>Runs the removal. Never throws for a single file; failures are counted and logged.</summary>
    public static RemoveOutcome Run(VideoRemoveRequest request) => ReviewFolder.Run(new MoveJob
    {
        Root = request.Root,
        ReviewFolderName = ReviewFolderName,
        Candidates = [.. Plan(request.Groups, request.Marked).Select(v => new MoveCandidate(v.Path, v.Size, v.ModifiedUtc))],
        Action = request.Action,
        Recycle = request.Recycle,
        Log = request.Log,
        Pause = request.Pause,
        Cancel = request.Cancel
    });
}

/// <summary>Pure edits of a <see cref="VideoGroup"/> made from the review screen.</summary>
public static class VideoGroupEdits
{
    /// <summary>
    /// The group with <paramref name="path"/> (a member) as its keeper. The previous keeper becomes a member
    /// with the same match, turned the other way, and is not suggested for moving - the user picked the
    /// keeper by hand, so nothing else is pre-decided for them.
    /// </summary>
    public static VideoGroup WithKeeper(VideoGroup group, string path)
    {
        var chosen = group.Members.FirstOrDefault(m => string.Equals(m.Video.Path, path, StringComparison.OrdinalIgnoreCase));
        if (chosen is null)
        {
            return group;
        }
        var oldKeeper = new VideoMember(group.Keeper, chosen.Match with { Orientation = VideoMatcher.Inverse(chosen.Match.Orientation) }, false);
        var members = group.Members.Where(m => m != chosen).Prepend(oldKeeper).ToList();
        return group with { Keeper = chosen.Video, Members = members };
    }
}
