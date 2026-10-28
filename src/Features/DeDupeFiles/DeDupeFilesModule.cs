using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.Features.DeDupeFiles;

/// <summary>Entry point for the DeDupe Files feature.</summary>
public sealed class DeDupeFilesModule : IFeatureModule
{
    public Control CreateView(IFeatureContext context) => new DeDupeFilesControl(context);
}
