using System.Text.Json;

namespace DigitalSecretary.App.Hosting;

/// <summary>
/// Discovers installed features by scanning the plugins folder for <c>plugin.json</c> files.
/// Only the JSON is read here — no feature assemblies are loaded, so startup stays cheap.
/// </summary>
public sealed class PluginCatalog
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private readonly string _pluginsRoot;

    public PluginCatalog(string pluginsRoot) => _pluginsRoot = pluginsRoot;

    public IReadOnlyList<PluginManifest> Discover()
    {
        var manifests = new List<PluginManifest>();
        if (!Directory.Exists(_pluginsRoot))
            return manifests;

        foreach (var dir in Directory.GetDirectories(_pluginsRoot))
        {
            var jsonPath = Path.Combine(dir, "plugin.json");
            if (!File.Exists(jsonPath))
                continue;

            try
            {
                var manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(jsonPath), JsonOpts);
                if (manifest is null || string.IsNullOrWhiteSpace(manifest.Id) || string.IsNullOrWhiteSpace(manifest.EntryType))
                    continue;

                manifest.Directory = dir;
                manifests.Add(manifest);
            }
            catch
            {
                // Ignore a malformed manifest rather than failing the whole app.
            }
        }

        return manifests
            .OrderBy(m => m.Order)
            .ThenBy(m => m.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
