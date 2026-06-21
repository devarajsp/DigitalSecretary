using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.App.Hosting;

/// <summary>
/// A discovered feature plus its lazily-created view. The manifest is known up front (from
/// plugin.json); the assembly and the UI are created only the first time <see cref="GetView"/>
/// is called, and cached thereafter.
/// </summary>
public sealed class LoadedFeature
{
    private readonly PluginLoader _loader;
    private readonly Func<string, IFeatureContext> _contextFactory;
    private Control? _view;

    public LoadedFeature(PluginManifest manifest, PluginLoader loader, Func<string, IFeatureContext> contextFactory)
    {
        Manifest = manifest;
        _loader = loader;
        _contextFactory = contextFactory;
    }

    public PluginManifest Manifest { get; }

    /// <summary>True once the feature has been opened at least once (its DLL is loaded).</summary>
    public bool IsActivated => _view is not null;

    public Control GetView()
    {
        if (_view is null)
        {
            var module = _loader.CreateModule(Manifest);
            var context = _contextFactory(Manifest.Id);
            _view = module.CreateView(context);
        }
        return _view;
    }
}
