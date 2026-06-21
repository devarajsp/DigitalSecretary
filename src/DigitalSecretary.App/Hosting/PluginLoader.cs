using DigitalSecretary.Abstractions;

namespace DigitalSecretary.App.Hosting;

/// <summary>Loads a feature's assembly on demand and instantiates its <see cref="IFeatureModule"/>.</summary>
public sealed class PluginLoader
{
    public IFeatureModule CreateModule(PluginManifest manifest)
    {
        var assemblyPath = Path.Combine(manifest.Directory, manifest.EntryAssembly);
        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException($"Plugin assembly not found: {assemblyPath}");

        // The assembly is loaded HERE — i.e. only when the feature is first opened.
        var context = new PluginLoadContext(assemblyPath);
        var assembly = context.LoadFromAssemblyPath(assemblyPath);

        var type = assembly.GetType(manifest.EntryType)
            ?? throw new TypeLoadException($"Entry type '{manifest.EntryType}' was not found in '{manifest.EntryAssembly}'.");

        if (Activator.CreateInstance(type) is not IFeatureModule module)
            throw new InvalidOperationException($"'{manifest.EntryType}' does not implement IFeatureModule.");

        return module;
    }
}
