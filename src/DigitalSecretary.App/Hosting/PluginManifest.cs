namespace DigitalSecretary.App.Hosting;

/// <summary>
/// The metadata read from a feature's <c>plugin.json</c>. This is intentionally lightweight:
/// the host reads it to build menus and the dashboard WITHOUT loading the feature's DLL.
/// </summary>
public sealed class PluginManifest
{
    /// <summary>Unique, stable id (kebab-case), e.g. "email-downloader".</summary>
    public string Id { get; set; } = "";

    /// <summary>Display name shown in menus and on the dashboard.</summary>
    public string Title { get; set; } = "";

    /// <summary>Menu group this feature appears under, e.g. "Tools".</summary>
    public string Category { get; set; } = "General";

    /// <summary>One-line description shown as a tooltip and on the dashboard tile.</summary>
    public string Description { get; set; } = "";

    /// <summary>Sort order within a menu/dashboard (lower comes first).</summary>
    public int Order { get; set; } = 100;

    /// <summary>The DLL that contains the entry type, e.g. "DigitalSecretary.Features.Launcher.dll".</summary>
    public string EntryAssembly { get; set; } = "";

    /// <summary>Full type name implementing IFeatureModule, e.g. "DigitalSecretary.Features.Launcher.LauncherModule".</summary>
    public string EntryType { get; set; } = "";

    /// <summary>Absolute path of the folder this manifest was loaded from (set by the catalog).</summary>
    public string Directory { get; set; } = "";
}
