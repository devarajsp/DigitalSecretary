using System.Windows.Forms;

namespace DigitalSecretary.Abstractions;

/// <summary>
/// The single entry point every feature plugin must implement. The host discovers the
/// implementing type from the feature's plugin.json and calls <see cref="CreateView"/>
/// the first time the user opens the feature (lazy loading).
/// </summary>
public interface IFeatureModule
{
    /// <summary>
    /// Builds the feature's UI. Called at most once per app run (the host caches the result).
    /// Return any WinForms control — it will be docked to fill the host's content area.
    /// </summary>
    Control CreateView(IFeatureContext context);
}
