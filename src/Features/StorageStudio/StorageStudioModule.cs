using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.Features.StorageStudio;

/// <summary>Entry point for the Storage Studio feature.</summary>
public sealed class StorageStudioModule : IFeatureModule
{
    public Control CreateView(IFeatureContext context) => new StorageStudioControl(context);
}
