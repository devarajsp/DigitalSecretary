namespace DigitalSecretary.App;

/// <summary>Well-known file-system locations for the app.</summary>
public static class AppPaths
{
    /// <summary>%APPDATA%\DigitalSecretary — root for all user data and settings.</summary>
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DigitalSecretary");

    /// <summary>Per-feature data folders live under here (one subfolder per feature id).</summary>
    public static string DataRoot { get; } = Path.Combine(Root, "data");

    /// <summary>The host's own settings file (dashboard show/hide, etc.).</summary>
    public static string SettingsFile { get; } = Path.Combine(Root, "app-settings.json");

    /// <summary>Folder next to the executable that holds the discoverable plugins.</summary>
    public static string PluginsRoot { get; } = Path.Combine(AppContext.BaseDirectory, "plugins");
}
