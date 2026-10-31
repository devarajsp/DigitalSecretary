namespace DigitalSecretary.Features.SimilarPictures;

/// <summary>Everything one removal pass needs.</summary>
public sealed class RemoveRequest
{
    public required string Root { get; init; }
    public required IReadOnlyList<SimilarGroup> Groups { get; init; }

    /// <summary>The paths the user marked. Keepers in this set are refused - one picture of every group always stays.</summary>
    public required IReadOnlySet<string> Marked { get; init; }

    public RemoveAction Action { get; init; } = RemoveAction.ReviewFolder;

    /// <summary>Sends a file to the Recycle Bin; replaceable so tests never touch the real one.</summary>
    public Action<string> Recycle { get; init; } = ReviewFolder.SendToRecycleBin;

    public IProgress<string>? Log { get; init; }
    public PauseToken? Pause { get; init; }
    public CancellationToken Cancel { get; init; }
}

/// <summary>
/// Moves the pictures the user marked out of the way and can undo the last move. The rule that is
/// Similar Pictures' own - a group's keeper is never removed - lives in <see cref="Plan"/>; the moving,
/// the change-since-scan guard, name clashes and undo are the shared <see cref="ReviewFolder"/> (kit).
/// </summary>
public static class ReviewMover
{
    /// <summary>Name of the review folder created inside the scanned folder.</summary>
    public const string ReviewFolderName = "_Similar pictures - review";

    public static string ReviewFolderFor(string root) => ReviewFolder.For(root, ReviewFolderName);

    /// <summary>Sends one file to the Recycle Bin.</summary>
    public static void SendToRecycleBin(string path) => ReviewFolder.SendToRecycleBin(path);

    /// <summary>
    /// The members to remove: marked, not a keeper, de-duplicated, in group order. Pure - this is the
    /// rule that guarantees at least one picture of every group survives.
    /// </summary>
    public static IReadOnlyList<PictureRecord> Plan(IReadOnlyList<SimilarGroup> groups, IReadOnlySet<string> marked)
    {
        var keepers = groups.Select(g => g.Keeper.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return [.. groups.SelectMany(g => g.Members).Select(m => m.Picture)
            .Where(p => marked.Contains(p.Path) && !keepers.Contains(p.Path) && seen.Add(p.Path))];
    }

    /// <summary>Where <paramref name="path"/> lands in the review folder, avoiding existing names.</summary>
    public static string TargetFor(string root, string path) => ReviewFolder.TargetFor(root, ReviewFolderName, path);

    /// <summary>Runs the removal. Never throws for a single file; failures are counted and logged.</summary>
    public static RemoveOutcome Run(RemoveRequest request) => ReviewFolder.Run(new MoveJob
    {
        Root = request.Root,
        ReviewFolderName = ReviewFolderName,
        Candidates = [.. Plan(request.Groups, request.Marked).Select(p => new MoveCandidate(p.Path, p.Size, p.ModifiedUtc))],
        Action = request.Action,
        Recycle = request.Recycle,
        Log = request.Log,
        Pause = request.Pause,
        Cancel = request.Cancel
    });

    /// <summary>Moves every file of <paramref name="batch"/> back. Returns (restored, skipped).</summary>
    public static (int Restored, int Skipped) Undo(MoveBatch batch, IProgress<string>? log = null) => ReviewFolder.Undo(batch, log);

    /// <summary>Saves the last batch so Undo still works after the app is closed.</summary>
    public static void SaveLastBatch(string file, MoveBatch batch) => ReviewFolder.SaveLastBatch(file, batch);

    /// <summary>The saved last batch, or null when there is none or it is unreadable.</summary>
    public static MoveBatch? LoadLastBatch(string file) => ReviewFolder.LoadLastBatch(file);
}
