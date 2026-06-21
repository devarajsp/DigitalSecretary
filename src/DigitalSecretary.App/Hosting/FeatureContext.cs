using DigitalSecretary.Abstractions;

namespace DigitalSecretary.App.Hosting;

/// <summary>The host's implementation of <see cref="IFeatureContext"/> handed to each feature.</summary>
public sealed class FeatureContext : IFeatureContext
{
    private readonly Action<string> _log;

    public FeatureContext(string featureId, string dataRoot, Action<string> log)
    {
        FeatureId = featureId;
        DataDirectory = Path.Combine(dataRoot, featureId);
        Directory.CreateDirectory(DataDirectory);
        _log = log;
    }

    public string FeatureId { get; }

    public string DataDirectory { get; }

    public void Log(string message) => _log($"[{FeatureId}] {message}");
}
