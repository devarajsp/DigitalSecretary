namespace DigitalSecretary.Abstractions;

/// <summary>
/// Services the host provides to a feature when its view is created.
/// A feature should never reach outside this contract — that keeps features
/// independent of the host and of each other.
/// </summary>
public interface IFeatureContext
{
    /// <summary>The feature's unique id (matches its plugin.json "id").</summary>
    string FeatureId { get; }

    /// <summary>
    /// A private, already-created folder where this feature may store its own data
    /// (settings, caches, downloads, …). Each feature gets its own folder, so features
    /// never collide.
    /// </summary>
    string DataDirectory { get; }

    /// <summary>Writes a diagnostic line to the host's log.</summary>
    void Log(string message);
}
