namespace DigitalSecretary.App.Settings;

/// <summary>Host-level preferences, persisted as JSON.</summary>
public sealed class AppSettings
{
    /// <summary>
    /// Feature ids the user has hidden from the dashboard. A feature is visible on the
    /// dashboard unless its id appears here (so newly added features show up by default).
    /// </summary>
    public List<string> HiddenOnDashboard { get; set; } = new();

    /// <summary>
    /// Feature ids the user has hidden from the Features menu. A feature is listed in the
    /// menu unless its id appears here (so newly added features show up by default).
    /// </summary>
    public List<string> HiddenOnMenu { get; set; } = new();
}
