using System.Windows.Forms;
using DigitalSecretary.Abstractions;

namespace DigitalSecretary.Features.ReduceImages;

/// <summary>Entry point for the Reduce Size - Images feature.</summary>
public sealed class ReduceImagesModule : IFeatureModule
{
    public Control CreateView(IFeatureContext context) => new ReduceImagesControl(context);
}
