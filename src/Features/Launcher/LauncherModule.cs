using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.Features.Launcher;

/// <summary>Entry point for the Launcher feature.</summary>
public sealed class LauncherModule : IFeatureModule
{
    public Control CreateView(IFeatureContext context) => new LauncherControl(context);
}
