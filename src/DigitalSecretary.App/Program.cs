using System.Diagnostics;
using System.Windows.Forms;
using DigitalSecretary.Abstractions;
using DigitalSecretary.App.Hosting;
using DigitalSecretary.App.Settings;

namespace DigitalSecretary.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);

        Directory.CreateDirectory(AppPaths.DataRoot);

        var settings = SettingsStore.Load();

        // Discover features from plugin.json manifests (no feature DLLs loaded yet).
        var catalog = new PluginCatalog(AppPaths.PluginsRoot);
        var loader = new PluginLoader();

        void Log(string message) => Debug.WriteLine(message);
        IFeatureContext ContextFactory(string featureId) => new FeatureContext(featureId, AppPaths.DataRoot, Log);

        var features = catalog.Discover()
            .Select(manifest => new LoadedFeature(manifest, loader, ContextFactory))
            .ToList();

        Application.Run(new MainForm(features, settings));
    }
}
