namespace DigitalSecretary.Kit;

/// <summary>The advice for one file (video or picture) in the Reduce Size features.</summary>
public enum Verdict
{
    /// <summary>A big, safe saving: pre-selected.</summary>
    Recommended,

    /// <summary>A real saving, but smaller or with a trade-off: the user decides (not pre-selected).</summary>
    Optional,

    /// <summary>Not worth it or not safe; never selected.</summary>
    Skip
}

/// <summary>What happened to one file in a Reduce Size run.</summary>
public enum FileOutcomeKind
{
    Reduced,
    Skipped,
    Failed
}

/// <summary>One file's result: sizes before / after and a short note (why skipped or failed, or a quality score).</summary>
public sealed record FileOutcome(string Path, FileOutcomeKind Kind, long Before, long After, string Note);

/// <summary>An <see cref="IProgress{T}"/> that calls straight through (no synchronisation context capture).</summary>
public sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}
