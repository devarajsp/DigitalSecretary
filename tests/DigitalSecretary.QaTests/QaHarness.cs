using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.QaTests;

/// <summary>Loads a feature exactly like the host does (isolated context + shared contract).</summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private static readonly string Shared = typeof(IFeatureModule).Assembly.GetName().Name!;
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string path) : base(isCollectible: false)
        => _resolver = new AssemblyDependencyResolver(path);

    protected override Assembly? Load(AssemblyName name)
    {
        if (name.Name == Shared) return null;
        var p = _resolver.ResolveAssemblyToPath(name);
        return p is null ? null : LoadFromAssemblyPath(p);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var p = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return p is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(p);
    }
}

internal sealed record QaManifest(
    string Id, string Title, string Category, string EntryAssembly, string EntryType, string Directory);

internal sealed class QaContext : IFeatureContext
{
    public QaContext(string id)
    {
        FeatureId = id;
        DataDirectory = Path.Combine(Path.GetTempPath(), "ds_qa", id);
        Directory.CreateDirectory(DataDirectory);
    }
    public string FeatureId { get; }
    public string DataDirectory { get; }
    public void Log(string message) { }
}

/// <summary>Locates the built plugins folder and reads its manifests.</summary>
internal static class QaEnvironment
{
    public static string PluginsRoot { get; } = Locate();

    public static IReadOnlyList<QaManifest> Manifests()
    {
        var result = new List<QaManifest>();
        if (!Directory.Exists(PluginsRoot))
            return result;

        foreach (var dir in Directory.GetDirectories(PluginsRoot))
        {
            var json = Path.Combine(dir, "plugin.json");
            if (!File.Exists(json))
                continue;

            using var doc = JsonDocument.Parse(File.ReadAllText(json));
            var r = doc.RootElement;
            result.Add(new QaManifest(
                Get(r, "id"), Get(r, "title"), Get(r, "category"),
                Get(r, "entryAssembly"), Get(r, "entryType"), dir));
        }
        return result;
    }

    private static string Get(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) ? v.GetString() ?? "" : "";

    private static string Locate()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DigitalSecretary.sln")))
            dir = dir.Parent;
        if (dir is null)
            return "";

        var config = AppContext.BaseDirectory.Contains(
            $"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            ? "Release" : "Debug";

        return Path.Combine(dir.FullName, "src", "DigitalSecretary.App", "bin", config, "net9.0-windows", "plugins");
    }
}

/// <summary>Runs an action on a dedicated STA thread (required to construct WinForms controls).</summary>
internal static class Sta
{
    public static void Run(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null)
            throw error;
    }
}
