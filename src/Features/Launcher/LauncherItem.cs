namespace DigitalSecretary.Features.Launcher;

/// <summary>A saved launcher entry: an app, file, folder, or URL.</summary>
public sealed class LauncherItem
{
    public string Name { get; set; } = "";
    public string Target { get; set; } = "";
    public string? Arguments { get; set; }
}
