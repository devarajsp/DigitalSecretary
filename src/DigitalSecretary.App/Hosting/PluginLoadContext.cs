using System.Reflection;
using System.Runtime.Loader;

namespace DigitalSecretary.App.Hosting;

/// <summary>
/// An isolated load context for one feature. Each feature is loaded into its own context so
/// features can carry their own private dependencies (e.g. the email feature ships MailKit)
/// without clashing with the host or with each other.
///
/// The contract assembly (DigitalSecretary.Abstractions) and all framework assemblies are
/// deliberately resolved from the host's default context so that types such as IFeatureModule
/// are shared and unify across the boundary.
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private static readonly string SharedContract = typeof(Abstractions.IFeatureModule).Assembly.GetName().Name!;

    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginAssemblyPath) : base(isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Share the contract with the host (return null => fall back to the default context).
        if (assemblyName.Name == SharedContract)
            return null;

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }
}
